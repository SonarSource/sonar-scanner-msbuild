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

namespace SonarScanner.MSBuild.Shim;

public class SonarScannerWrapper
{
    /// <summary>
    /// Name of the command line argument used to specify the generated project settings file to use.
    /// </summary>
    public const string ProjectSettingsFileArgName = "project.settings";

    private const string ScanAllFiles = "-Dsonar.scanAllFiles=true";

    private const string CmdLineArgPrefix = "-D";

    // This version needs to be in sync with version in scripts\variables.ps1.
    private const string SonarScannerVersion = "5.0.2.4997";

    private readonly ILogger logger;
    private readonly OperatingSystemProvider operatingSystemProvider;

    public SonarScannerWrapper(IRuntime runtime)
    {
        logger = runtime.Logger;
        operatingSystemProvider = runtime.OperatingSystem;
    }

    public virtual bool Execute(AnalysisConfig config, IAnalysisPropertyProvider userCmdLineArguments, string propertiesFilePath)
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

    public virtual /* for test purposes */ bool ExecuteJavaRunner(AnalysisConfig config,
                                                                  IAnalysisPropertyProvider userCmdLineArguments,
                                                                  string exeFileName,
                                                                  string propertiesFileName,
                                                                  IProcessRunner runner)
    {
        Debug.Assert(File.Exists(exeFileName), "The specified exe file does not exist: " + exeFileName);
        Debug.Assert(File.Exists(propertiesFileName), "The specified properties file does not exist: " + propertiesFileName);

        IgnoreSonarScannerHome(logger);

        var stringArgs = userCmdLineArguments.GetAllProperties().Select(x => x.AsSonarScannerArg()).ToArray();
        var allCmdLineArgs = AllCmdLineArgs(propertiesFileName, stringArgs, config, logger);
        var envVarsDictionary = AdditionalEnvVariables(config, userCmdLineArguments, logger, operatingSystemProvider);
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
        var result = runner.Execute(scannerArgs);
        if (result.Succeeded)
        {
            logger.LogInfo(Resources.MSG_SonarScannerCompleted);
        }
        else
        {
            logger.LogError(Resources.ERR_SonarScannerExecutionFailed);
        }
        return result.Succeeded;
    }

    internal /* for testing */ string FindScannerExe()
    {
        var binFolder = Path.GetDirectoryName(typeof(SonarScannerWrapper).Assembly.Location);
        var fileExtension = operatingSystemProvider.OperatingSystem() == PlatformOS.Windows ? ".bat" : string.Empty;
        return Path.Combine(binFolder, $"sonar-scanner-{SonarScannerVersion}", "bin", $"sonar-scanner{fileExtension}");
    }

    private bool InternalExecute(AnalysisConfig config, IAnalysisPropertyProvider userCmdLineArguments, string fullPropertiesFilePath)
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
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariables.SonarScannerHomeVariableName)))
        {
            logger.LogInfo(Resources.MSG_SonarScannerHomeIsSet);
            Environment.SetEnvironmentVariable(EnvironmentVariables.SonarScannerHomeVariableName, string.Empty);
        }
    }

    /// <summary>
    /// Returns any additional environment variables that need to be passed to the sonar-scanner.
    /// </summary>
    private static IDictionary<string, string> AdditionalEnvVariables(
        AnalysisConfig config,
        IAnalysisPropertyProvider userCmdLineArguments,
        ILogger logger,
        OperatingSystemProvider operatingSystemProvider)
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
                envVarsDictionary.Add(EnvironmentVariables.JavaHomeVariableName, javaHome);
                logger.LogDebug(Resources.MSG_SettingJavaHomeEnvironmentVariable, javaHome);
            }
            catch (Exception exception)
            {
                logger.LogWarning(Resources.MSG_SettingJavaHomeEnvironmentVariableFailed, config.JavaExePath, exception.Message);
            }
        }

        // If there is a value for SONAR_SCANNER_OPTS then pass it through explicitly just in case it is
        // set at process-level (which wouldn't otherwise be inherited by the child sonar-scanner process)
        var sonarScannerOptsValue = Environment.GetEnvironmentVariable(EnvironmentVariables.SonarScannerOptsVariableName);
        if (sonarScannerOptsValue is not null)
        {
            envVarsDictionary.Add(EnvironmentVariables.SonarScannerOptsVariableName, sonarScannerOptsValue);
            logger.LogInfo(Resources.MSG_UsingSuppliedSonarScannerOptsValue, EnvironmentVariables.SonarScannerOptsVariableName, sonarScannerOptsValue.RedactSensitiveData());
        }

        var scannerOptsEnvValue = new StringBuilder();
        if (envVarsDictionary.TryGetValue(EnvironmentVariables.SonarScannerOptsVariableName, out var sonarScannerOptsOldValue))
        {
            scannerOptsEnvValue.Append(sonarScannerOptsOldValue);
        }

        if (config.ScannerOptsSettings?.Any() is true)
        {
            // If there are any duplicates properties, the last one will be used.
            // As of today, properties coming from ScannerOptsSettings are set
            // via the command line, so they should take precedence over the ones
            // set via the environment variable.
            foreach (var property in config.ScannerOptsSettings)
            {
                scannerOptsEnvValue.Append($" {property.AsSonarScannerArg()}");
            }
        }

        // If the truststore password is set, we need to pass it to the sonar-scanner through the SONAR_SCANNER_OPTS environment variable.
        // And map the value to the javax.net.ssl.trustStorePassword property.
        // If it is not set, we will use the default value, unless it was set already in the SONAR_SCANNER_OPTS.
        if (!userCmdLineArguments.TryGetValue(SonarProperties.TruststorePassword, out var truststorePassword))
        {
            var truststorePath = config.ScannerOptsSettings.FirstOrDefault(x => x.Id == SonarProperties.JavaxNetSslTrustStore);
            truststorePassword = TruststoreUtils.TruststoreDefaultPassword(truststorePath?.Value, logger);
        }

        if (!SonarPropertiesDefault.TruststorePasswords.Contains(truststorePassword)
            || sonarScannerOptsOldValue is null
            || !sonarScannerOptsOldValue.Contains($"-D{SonarProperties.JavaxNetSslTrustStorePassword}="))
        {
            scannerOptsEnvValue.Append(operatingSystemProvider.IsUnix()
                ? $" -D{SonarProperties.JavaxNetSslTrustStorePassword}={truststorePassword}"
                : $" -D{SonarProperties.JavaxNetSslTrustStorePassword}=\"{truststorePassword}\"");
        }

        if (scannerOptsEnvValue.Length > 0)
        {
            envVarsDictionary[EnvironmentVariables.SonarScannerOptsVariableName] = scannerOptsEnvValue.ToString().Trim();
        }

        return envVarsDictionary;
    }

    /// <summary>
    /// Returns all the command line arguments to pass to sonar-scanner.
    /// </summary>
    private static IEnumerable<string> AllCmdLineArgs(string projectSettingsFilePath, IEnumerable<string> userCmdLineArguments, AnalysisConfig config, ILogger logger)
    {
        // We don't know what all the valid command line arguments are so we'll
        // just pass them on for the sonar-scanner to validate.
        var args = new List<string>(userCmdLineArguments);

        // Add any sensitive arguments supplied in the config should be passed on the command line
        args.AddRange(SensitiveFileSettings(config, userCmdLineArguments, logger));

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

        // Probably legacy as it does not seems to be used anymore by the scanner cli.
        // Moved from PostProcessor
        // https://github.com/SonarSource/sonar-scanner-msbuild/blob/ceee8614067e8b0ce43caa04c89673b55de78d85/src/SonarScanner.MSBuild.PostProcessor/PostProcessor.cs#L264-L267
        if (!userCmdLineArguments.Contains(ScanAllFiles))
        {
            args.Add(ScanAllFiles);
        }

        return args;
    }

    private static IEnumerable<string> SensitiveFileSettings(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger) =>
        config.AnalysisSettings(false, logger)
            .GetAllProperties()
            .Where(x => x.ContainsSensitiveData() && !UserSettingExists(x, userCmdLineArguments))
            .Select(x => x.AsSonarScannerArg());

    private static bool UserSettingExists(Property fileProperty, IEnumerable<string> userArgs) =>
        userArgs.Any(x => x.IndexOf(CmdLineArgPrefix + fileProperty.Id, StringComparison.Ordinal) == 0);
}
