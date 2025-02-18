/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

using System.Globalization;
using SonarScanner.MSBuild.Shim.Interfaces;

namespace SonarScanner.MSBuild.Shim;

public class SonarScannerWrapper(ILogger logger, IOperatingSystemProvider operatingSystemProvider) : ISonarScanner
{
    /// <summary>
    /// Env variable that locates the sonar-scanner
    /// </summary>
    /// <remarks>Existing values set by the user might cause failures.</remarks>
    public const string SonarScannerHomeVariableName = "SONAR_SCANNER_HOME";

    /// <summary>
    /// Name of the command line argument used to specify the generated project settings file to use
    /// </summary>
    public const string ProjectSettingsFileArgName = "project.settings";

    /// <summary>
    /// Env variable that controls the amount of memory the JVM can use for the sonar-scanner.
    /// </summary>
    /// <remarks>Large projects error out with OutOfMemoryException if not set.</remarks>
    private const string SonarScannerOptsVariableName = "SONAR_SCANNER_OPTS";

    private const string JavaHomeVariableName = "JAVA_HOME";

    private const string CmdLineArgPrefix = "-D";

    // This version needs to be in sync with version in scripts\variables.ps1.
    private const string SonarScannerVersion = "5.0.1.3006";

    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IOperatingSystemProvider operatingSystemProvider = operatingSystemProvider ?? throw new ArgumentNullException(nameof(operatingSystemProvider));

    public bool Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, string propertiesFilePath)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        if (userCmdLineArguments is null)
        {
            throw new ArgumentNullException(nameof(userCmdLineArguments));
        }

        return InternalExecute(config, userCmdLineArguments, propertiesFilePath);
    }

    public /* for test purposes */ bool ExecuteJavaRunner(AnalysisConfig config,
                                                          IEnumerable<string> userCmdLineArguments,
                                                          string exeFileName,
                                                          string propertiesFileName,
                                                          IProcessRunner runner)
    {
        Debug.Assert(File.Exists(exeFileName), "The specified exe file does not exist: " + exeFileName);
        Debug.Assert(File.Exists(propertiesFileName), "The specified properties file does not exist: " + propertiesFileName);

        IgnoreSonarScannerHome(logger);

        var allCmdLineArgs = GetAllCmdLineArgs(propertiesFileName, userCmdLineArguments, config, logger);
        var envVarsDictionary = GetAdditionalEnvVariables(config, logger);
        Debug.Assert(envVarsDictionary is not null, "Unable to retrieve additional environment variables");

        logger.LogInfo(Resources.MSG_SonarScannerCalling);

        Debug.Assert(!string.IsNullOrWhiteSpace(config.SonarScannerWorkingDirectory), "The working dir should have been set in the analysis config");
        Debug.Assert(Directory.Exists(config.SonarScannerWorkingDirectory), "The working dir should exist");

        var scannerArgs = new ProcessRunnerArguments(exeFileName, operatingSystemProvider.OperatingSystem() == PlatformOS.Windows)
        {
            CmdLineArgs = allCmdLineArgs,
            WorkingDirectory = config.SonarScannerWorkingDirectory,
            EnvironmentVariables = envVarsDictionary
        };

        // Note that the Sonar Scanner may write warnings to stderr so
        // we should only rely on the exit code when deciding if it ran successfully.
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

    internal /* for testing */ string FindScannerExe()
    {
        var binFolder = Path.GetDirectoryName(typeof(SonarScannerWrapper).Assembly.Location);
        var fileExtension = operatingSystemProvider.OperatingSystem() == PlatformOS.Windows ? ".bat" : string.Empty;
        return Path.Combine(binFolder, $"sonar-scanner-{SonarScannerVersion}", "bin", $"sonar-scanner{fileExtension}");
    }

    private bool InternalExecute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, string fullPropertiesFilePath)
    {
        if (fullPropertiesFilePath is null)
        {
            // We expect a detailed error message to have been logged explaining
            // why the properties file generation could not be performed
            logger.LogInfo(Resources.MSG_PropertiesGenerationFailed);
            return false;
        }

        var exeFileName = FindScannerExe();
        return ExecuteJavaRunner(config, userCmdLineArguments, exeFileName, fullPropertiesFilePath, new ProcessRunner(logger));
    }

    private static void IgnoreSonarScannerHome(ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SonarScannerHomeVariableName)))
        {
            logger.LogInfo(Resources.MSG_SonarScannerHomeIsSet);
            Environment.SetEnvironmentVariable(SonarScannerHomeVariableName, string.Empty);
        }
    }

    /// <summary>
    /// Returns any additional environment variables that need to be passed to the sonar-scanner.
    /// </summary>
    private static IDictionary<string, string> GetAdditionalEnvVariables(AnalysisConfig config, ILogger logger)
    {
        IDictionary<string, string> envVarsDictionary = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(config.JavaExePath))
        {
            // The java exe path points to the java.exe file while the JAVA_HOME needs to point to the installation directory that contains the bin/ directory where the java executable
            // physically resides.
            // e.g. C:\Program Files\Java\jdk-17\bin\java.exe -> C:\Program Files\Java\jdk-17\
            try
            {
                var exeDirectory = Path.GetDirectoryName(config.JavaExePath);
                var javaHome = Directory.GetParent(exeDirectory).ToString();
                envVarsDictionary.Add(JavaHomeVariableName, javaHome);
                logger.LogDebug(Resources.MSG_SettingJavaHomeEnvironmentVariable, javaHome);
            }
            catch (Exception exception)
            {
                logger.LogWarning(Resources.MSG_SettingJavaHomeEnvironmentVariableFailed, config.JavaExePath, exception.Message);
            }
        }

        // If there is a value for SONAR_SCANNER_OPTS then pass it through explicitly just in case it is
        // set at process-level (which wouldn't otherwise be inherited by the child sonar-scanner process)
        var sonarScannerOptsValue = Environment.GetEnvironmentVariable(SonarScannerOptsVariableName);
        if (sonarScannerOptsValue is not null)
        {
            envVarsDictionary.Add(SonarScannerOptsVariableName, sonarScannerOptsValue);
            logger.LogInfo(Resources.MSG_UsingSuppliedSonarScannerOptsValue, SonarScannerOptsVariableName, sonarScannerOptsValue);
        }

        if (config.ScannerOptsSettings?.Any() is true)
        {
            var envValueBuilder = new StringBuilder();
            if (envVarsDictionary.TryGetValue(SonarScannerOptsVariableName, out var existingValue))
            {
                envValueBuilder.Append(existingValue);
            }

            // If there are any duplicates properties, the last one will be used.
            // As of today, properties coming from ScannerOptsSettings are set
            // via the command line, so they should take precedence over the ones
            // set via the environment variable.
            foreach (var property in config.ScannerOptsSettings)
            {
                envValueBuilder.Append($" {property.AsSonarScannerArg()}");
            }

            envVarsDictionary[SonarScannerOptsVariableName] = envValueBuilder.ToString().Trim();
        }

        return envVarsDictionary;
    }

    /// <summary>
    /// Returns all the command line arguments to pass to sonar-scanner
    /// </summary>
    private static IEnumerable<string> GetAllCmdLineArgs(string projectSettingsFilePath, IEnumerable<string> userCmdLineArguments, AnalysisConfig config, ILogger logger)
    {
        // We don't know what all the valid command line arguments are so we'll
        // just pass them on for the sonar-scanner to validate.
        var args = new List<string>(userCmdLineArguments);

        // Add any sensitive arguments supplied in the config should be passed on the command line
        args.AddRange(GetSensitiveFileSettings(config, userCmdLineArguments, logger));

        // Add the project settings file and the standard options.
        // Experimentation suggests that the sonar-scanner won't error if duplicate arguments
        // are supplied - it will just use the last argument.
        // So we'll set our additional properties last to make sure they take precedence.
        args.Add(string.Format(CultureInfo.InvariantCulture, "{0}{1}={2}", CmdLineArgPrefix, ProjectSettingsFileArgName, projectSettingsFilePath));

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

    private static IEnumerable<string> GetSensitiveFileSettings(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger) =>
        config.GetAnalysisSettings(false, logger)
            .GetAllProperties()
            .Where(x => x.ContainsSensitiveData() && !UserSettingExists(x, userCmdLineArguments))
            .Select(x => x.AsSonarScannerArg());

    private static bool UserSettingExists(Property fileProperty, IEnumerable<string> userArgs) =>
        userArgs.Any(x => x.IndexOf(CmdLineArgPrefix + fileProperty.Id, StringComparison.Ordinal) == 0);
}
