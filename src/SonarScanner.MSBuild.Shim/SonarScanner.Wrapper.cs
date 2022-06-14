/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2022 SonarSource SA
 * mailto: info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim
{
    public class SonarScannerWrapper : ISonarScanner
    {
        /// <summary>
        /// Env variable that controls the amount of memory the JVM can use for the sonar-scanner.
        /// </summary>
        /// <remarks>Large projects error out with OutOfMemoryException if not set</remarks>
        private const string SonarScannerOptsVariableName = "SONAR_SCANNER_OPTS";

        /// <summary>
        /// Env variable that locates the sonar-scanner
        /// </summary>
        /// <remarks>Existing values set by the user might cause failures/remarks>
        public const string SonarScannerHomeVariableName = "SONAR_SCANNER_HOME";

        /// <summary>
        /// Name of the command line argument used to specify the generated project settings file to use
        /// </summary>
        public const string ProjectSettingsFileArgName = "project.settings";

        private const string CmdLineArgPrefix = "-D";

        // This version needs to be in sync with version in scripts\variables.ps1.
        private const string SonarScannerVersion = "4.7.0.2747";

        private readonly ILogger logger;

        public SonarScannerWrapper(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region ISonarScanner interface

        public bool Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, string propertiesFilePath)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (userCmdLineArguments == null)
            {
                throw new ArgumentNullException(nameof(userCmdLineArguments));
            }

            return InternalExecute(config, userCmdLineArguments, logger, propertiesFilePath);
        }

        #endregion ISonarScanner interface

        #region Private methods

        private static bool InternalExecute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger, string fullPropertiesFilePath)
        {
            if (fullPropertiesFilePath == null)
            {
                // We expect a detailed error message to have been logged explaining
                // why the properties file generation could not be performed
                logger.LogInfo(Resources.MSG_PropertiesGenerationFailed);
                return false;
            }

            var exeFileName = FindScannerExe(logger, Path.GetDirectoryName(typeof(SonarScannerWrapper).Assembly.Location));
            return ExecuteJavaRunner(config, userCmdLineArguments, logger, exeFileName, fullPropertiesFilePath, new ProcessRunner(logger));
        }

        internal /* for testing */ static string FindScannerExe(ILogger logger, string scannerCliDirectoryLocation, string scannerVersion = SonarScannerVersion)
        {
            var scannerCliFolder = Path.Combine(scannerCliDirectoryLocation, $"sonar-scanner-{scannerVersion}");

            // During packaging of the artifacts (see script https://github.com/SonarSource/sonar-scanner-msbuild/blob/master/scripts/package-artifacts.ps1)
            // the scanner-cli is unzipped for .NET Framework, but not for the .NET and .NET Core - where it's unzipped upon first usage of the scanner.
            // For this reason, the 'if' block below will not be executed for .NET Framework.
            if (!Directory.Exists(scannerCliFolder))
            {
                // We unzip the scanner-cli-{version}.zip while in the user's machine so that the Unix file permissions are not lost.
                // The unzipping happens only once, during the first scanner usage.
                var scannerCliZipFolderName = $"sonar-scanner-cli-{scannerVersion}.zip";
                var zipPath = Path.Combine(scannerCliDirectoryLocation, scannerCliZipFolderName);
                logger.LogInfo($"Unzipping {scannerCliZipFolderName}");
                // System.IO.Compression.ZipFile has zipbomb attack protection: https://github.com/dotnet/runtime/issues/15940
                ZipFile.ExtractToDirectory(zipPath, scannerCliDirectoryLocation);
            }

            var fileExtension = PlatformHelper.IsWindows() ? ".bat" : string.Empty;
            var scannerExecutablePath = Path.Combine(scannerCliDirectoryLocation, $"sonar-scanner-{scannerVersion}", "bin", $"sonar-scanner{fileExtension}");
            Debug.Assert(File.Exists(scannerExecutablePath), $"The scanner executable file does not exist:  {scannerExecutablePath}");

            return scannerExecutablePath;
        }

        internal /* for testing */ static bool ExecuteJavaRunner(
            AnalysisConfig config,
            IEnumerable<string> userCmdLineArguments,
            ILogger logger,
            string exeFileName,
            string propertiesFileName,
            IProcessRunner runner)
        {
            Debug.Assert(File.Exists(exeFileName), $"The specified exe file does not exist:  {exeFileName}");
            Debug.Assert(File.Exists(propertiesFileName), $"The specified properties file does not exist: {propertiesFileName}");

            IgnoreSonarScannerHome(logger);

            var allCmdLineArgs = GetAllCmdLineArgs(propertiesFileName, userCmdLineArguments, config, logger);

            var envVarsDictionary = GetAdditionalEnvVariables(logger);
            Debug.Assert(envVarsDictionary != null, $"The additional enviroment variables dictionary is null, {nameof(envVarsDictionary)}");

            logger.LogInfo(Resources.MSG_SonarScannerCalling);

            Debug.Assert(!string.IsNullOrWhiteSpace(config.SonarScannerWorkingDirectory), "The working dir should have been set in the analysis config");
            Debug.Assert(Directory.Exists(config.SonarScannerWorkingDirectory), "The working dir should exist");

            var scannerArgs = new ProcessRunnerArguments(exeFileName, PlatformHelper.IsWindows())
            {
                CmdLineArgs = allCmdLineArgs,
                WorkingDirectory = config.SonarScannerWorkingDirectory,
                EnvironmentVariables = envVarsDictionary
            };

            // SONARMSBRU-202 Note that the Sonar Scanner may write warnings to stderr so
            // we should only rely on the exit code when deciding if it ran successfully
            var success = runner.Execute(scannerArgs);

            if (success)
            {
                logger.LogInfo(Resources.MSG_SonarScannerCompleted);
            }
            else
            {
                logger.LogError(Resources.ERR_SonarScannerExecutionFailed);
            }
            return success;
        }

        private static void IgnoreSonarScannerHome(ILogger logger)
        {
            if (!string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(SonarScannerHomeVariableName)))
            {
                logger.LogInfo(Resources.MSG_SonarScannerHomeIsSet);
                Environment.SetEnvironmentVariable(SonarScannerHomeVariableName, string.Empty);
            }
        }

        /// <summary>
        /// Returns any additional environment variables that need to be passed to
        /// the sonar-scanner
        /// </summary>
        private static IDictionary<string, string> GetAdditionalEnvVariables(ILogger logger)
        {
            IDictionary<string, string> envVarsDictionary = new Dictionary<string, string>();

            // If there is a value for SONAR_SCANNER_OPTS then pass it through explicitly just in case it is
            // set at process-level (which wouldn't otherwise be inherited by the child sonar-scanner process)
            var sonarScannerOptsValue = Environment.GetEnvironmentVariable(SonarScannerOptsVariableName);
            if (sonarScannerOptsValue != null)
            {
                envVarsDictionary.Add(SonarScannerOptsVariableName, sonarScannerOptsValue);
                logger.LogInfo(Resources.MSG_UsingSuppliedSonarScannerOptsValue, SonarScannerOptsVariableName, sonarScannerOptsValue);
            }

            return envVarsDictionary;
        }

        /// <summary>
        /// Returns all of the command line arguments to pass to sonar-scanner
        /// </summary>
        private static IEnumerable<string> GetAllCmdLineArgs(string projectSettingsFilePath,
            IEnumerable<string> userCmdLineArguments, AnalysisConfig config, ILogger logger)
        {
            // We don't know what all of the valid command line arguments are so we'll
            // just pass them on for the sonar-scanner to validate.
            var args = new List<string>(userCmdLineArguments);

            // Add any sensitive arguments supplied in the config should be passed on the command line
            args.AddRange(GetSensitiveFileSettings(config, userCmdLineArguments));

            // Add the project settings file and the standard options.
            // Experimentation suggests that the sonar-scanner won't error if duplicate arguments
            // are supplied - it will just use the last argument.
            // So we'll set our additional properties last to make sure they take precedence.
            args.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}{1}={2}", CmdLineArgPrefix,
                ProjectSettingsFileArgName, projectSettingsFilePath));

            // Let the scanner cli know it has been ran from this MSBuild Scanner. (allows to tweak the behavior)
            // See https://jira.sonarsource.com/browse/SQSCANNER-65
            args.Add("--from=ScannerMSBuild/" + Utilities.ScannerVersion);

            // For debug mode, we need to pass the debug option to the scanner cli in order to see correctly stack traces.
            // Note that in addition to this change, the sonar.verbose=true was removed from the config file.
            // See: https://github.com/SonarSource/sonar-scanner-msbuild/issues/543
            if (logger.Verbosity == LoggerVerbosity.Debug)
            {
                args.Add("--debug");
            }

            return args;
        }

        private static IEnumerable<string> GetSensitiveFileSettings(AnalysisConfig config, IEnumerable<string> userCmdLineArguments)
        {
            var allPropertiesFromConfig = config.GetAnalysisSettings(false).GetAllProperties();

            return allPropertiesFromConfig.Where(p => p.ContainsSensitiveData() && !UserSettingExists(p, userCmdLineArguments))
                .Select(p => p.AsSonarScannerArg());
        }

        private static bool UserSettingExists(Property fileProperty, IEnumerable<string> userArgs)
        {
            return userArgs.Any(userArg => userArg.IndexOf(CmdLineArgPrefix + fileProperty.Id, StringComparison.Ordinal) == 0);
        }

#endregion Private methods
    }
}
