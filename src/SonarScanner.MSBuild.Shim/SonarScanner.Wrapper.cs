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

    private readonly IRuntime runtime;

    public SonarScannerWrapper(IRuntime runtime) =>
        this.runtime = runtime;

    public virtual bool Execute(AnalysisConfig config, IAnalysisPropertyProvider userCmdLineArguments, string propertiesFilePath)
    {
        _ = config ?? throw new ArgumentNullException(nameof(config));
        _ = userCmdLineArguments ?? throw new ArgumentNullException(nameof(userCmdLineArguments));

        return InternalExecute(config, userCmdLineArguments, propertiesFilePath);
    }

    internal virtual bool ExecuteJavaRunner(AnalysisConfig config,
                                            IAnalysisPropertyProvider userCmdLineArguments,
                                            string exeFileName,
                                            string propertiesFileName,
                                            IProcessRunner runner)
    {
        Debug.Assert(runtime.File.Exists(exeFileName), "The specified exe file does not exist: " + exeFileName);
        Debug.Assert(runtime.File.Exists(propertiesFileName), "The specified properties file does not exist: " + propertiesFileName);

        IgnoreSonarScannerHome();

        var stringArgs = userCmdLineArguments.GetAllProperties().Select(x => x.AsSonarScannerArg()).ToArray();
        var allCmdLineArgs = AllCmdLineArgs(propertiesFileName, stringArgs, config);
        var envVarsDictionary = AdditionalEnvVariables(config, userCmdLineArguments);
        Debug.Assert(envVarsDictionary is not null, "Unable to retrieve additional environment variables");

        runtime.LogInfo(Resources.MSG_SonarScannerCalling);

        Debug.Assert(!string.IsNullOrWhiteSpace(config.SonarScannerWorkingDirectory), "The working dir should have been set in the analysis config");
        Debug.Assert(Directory.Exists(config.SonarScannerWorkingDirectory), "The working dir should exist");

        var scannerArgs = new ProcessRunnerArguments(exeFileName, runtime.OperatingSystem.IsWindows())
        {
            CmdLineArgs = allCmdLineArgs.Select(x => new ProcessRunnerArguments.Argument(x)).ToArray(),
            WorkingDirectory = config.SonarScannerWorkingDirectory,
            EnvironmentVariables = envVarsDictionary
        };

        // Note that the Sonar Scanner may write warnings to stderr so
        // we should only rely on the exit code when deciding if it ran successfully.
        var result = runner.Execute(scannerArgs);
        if (result.Succeeded)
        {
            runtime.LogInfo(Resources.MSG_SonarScannerCompleted);
        }
        else
        {
            runtime.LogError(Resources.ERR_SonarScannerExecutionFailed);
        }
        return result.Succeeded;
    }

    internal string FindScannerExe(AnalysisConfig config)
    {
        var bashScript = config.SonarScannerCliPath; // Full path to the bash script sonar-scanner-5.0.2.4997/bin/sonar-scanner in the downloaded SonarScanner CLI
        if (string.IsNullOrWhiteSpace(bashScript))
        {
            runtime.LogError(Resources.ERR_SonarScannerCliNotFound, Resources.MSG_SonarScannerCliPath_Missing);
            return null;
        }
        var executable = runtime.OperatingSystem.IsWindows()
            ? Path.ChangeExtension(bashScript, "bat")
            : bashScript;
        if (!runtime.File.Exists(executable))
        {
            runtime.LogError(Resources.ERR_SonarScannerCliNotFound, string.Format(Resources.MSG_SonarCliPath_FileNotFound, executable));
            return null;
        }
        return executable;
    }

    private bool InternalExecute(AnalysisConfig config, IAnalysisPropertyProvider userCmdLineArguments, string fullPropertiesFilePath)
    {
        if (fullPropertiesFilePath is null)
        {
            // We expect a detailed error message to have been logged explaining
            // why the properties file generation could not be performed
            runtime.LogInfo(Resources.MSG_PropertiesGenerationFailed);
            return false;
        }

        return FindScannerExe(config) is { } exeFileName && ExecuteJavaRunner(config, userCmdLineArguments, exeFileName, fullPropertiesFilePath, new ProcessRunner(runtime.Logger));
    }

    private void IgnoreSonarScannerHome()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariables.SonarScannerHomeVariableName)))
        {
            runtime.LogInfo(Resources.MSG_SonarScannerHomeIsSet);
            Environment.SetEnvironmentVariable(EnvironmentVariables.SonarScannerHomeVariableName, string.Empty);
        }
    }

    /// <summary>
    /// Returns any additional environment variables that need to be passed to the sonar-scanner.
    /// </summary>
    private IDictionary<string, string> AdditionalEnvVariables(AnalysisConfig config, IAnalysisPropertyProvider userCmdLineArguments)
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
                runtime.LogDebug(Resources.MSG_SettingJavaHomeEnvironmentVariable, javaHome);
            }
            catch (Exception exception)
            {
                runtime.LogWarning(Resources.MSG_SettingJavaHomeEnvironmentVariableFailed, config.JavaExePath, exception.Message);
            }
        }

        var scannerOptsEnvValue = new StringBuilder();

        foreach (var scannerOpt in SonarEngineWrapper.JavaParams(config, userCmdLineArguments, runtime))
        {
            scannerOptsEnvValue.Append($" {scannerOpt}");
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
    private IEnumerable<string> AllCmdLineArgs(string projectSettingsFilePath, IEnumerable<string> userCmdLineArguments, AnalysisConfig config)
    {
        // We don't know what all the valid command line arguments are so we'll
        // just pass them on for the sonar-scanner to validate.
        var args = new List<string>(userCmdLineArguments);

        // Add any sensitive arguments supplied in the config should be passed on the command line
        args.AddRange(SensitiveFileSettings(config, userCmdLineArguments));

        // Add the project settings file and the standard options.
        // Experimentation suggests that the sonar-scanner won't error if duplicate arguments
        // are supplied - it will just use the last argument.
        // So we'll set our additional properties last to make sure they take precedence.
        args.Add(string.Format(CultureInfo.InvariantCulture, "{0}{1}={2}", CmdLineArgPrefix, ProjectSettingsFileArgName, projectSettingsFilePath));

        // Let the scanner cli know it has been ran from this MSBuild Scanner. (allows to tweak the behavior)
        // See https://jira.sonarsource.com/browse/SQSCANNER-65
        args.Add($"--from={ScannerEngineInput.SonarScannerAppValue}/{Utilities.ScannerVersion}");

        // For debug mode, we need to pass the debug option to the scanner cli in order to see correctly stack traces.
        // Note that in addition to this change, the sonar.verbose=true was removed from the config file.
        // See: https://github.com/SonarSource/sonar-scanner-msbuild/issues/543
        if (runtime.Logger.Verbosity == LoggerVerbosity.Debug)
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

    private IEnumerable<string> SensitiveFileSettings(AnalysisConfig config, IEnumerable<string> userCmdLineArguments) =>
        config.AnalysisSettings(false, runtime.Logger)
            .GetAllProperties()
            .Where(x => x.ContainsSensitiveData() && !UserSettingExists(x, userCmdLineArguments))
            .Select(x => x.AsSonarScannerArg());

    private static bool UserSettingExists(Property fileProperty, IEnumerable<string> userArgs) =>
        userArgs.Any(x => x.IndexOf(CmdLineArgPrefix + fileProperty.Id, StringComparison.Ordinal) == 0);
}
