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

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SonarScanner.MSBuild.Common.TFS;

namespace SonarScanner.MSBuild.PreProcessor;

/// <summary>
/// Data class to hold validated command line arguments required by the pre-processor.
/// </summary>
public class ProcessedArgs
{
    /// <summary>
    /// Regular expression to validate a project key.
    /// See http://docs.sonarqube.org/display/SONAR/Project+Administration#ProjectAdministration-AddingaProject.
    /// </summary>
    /// <remarks>Should match the java regex here: https://github.com/SonarSource/sonarqube/blob/5.1.1/sonar-core/src/main/java/org/sonar/core/component/ComponentKeys.java#L36
    /// "Allowed characters are alphanumeric, '-', '_', '.' and ':', with at least one non-digit".
    /// </remarks>
    private static readonly Regex ProjectKeyRegEx = new(@"^[a-zA-Z0-9:\-_\.]*[a-zA-Z:\-_\.]+[a-zA-Z0-9:\-_\.]*$", RegexOptions.Compiled | RegexOptions.Singleline, RegexConstants.DefaultTimeout);

    private readonly IAnalysisPropertyProvider globalFileProperties;
    private readonly IRuntime runtime;

    public /* for testing */ virtual string ProjectKey { get; }

    public string ProjectName { get; }

    public string ProjectVersion { get; }

    public TimeSpan HttpTimeout { get; }

    public /* for testing */ virtual string Organization { get; }

    public HostInfo ServerInfo { get; }

    /// <summary>
    /// Returns the operating system used to run the scanner.
    /// Supported values are windows|linux|macos|alpine but more can be added later
    /// https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001395/Scanner+Bootstrappers+implementation+guidelines.
    public virtual string OperatingSystem { get; }

    /// <summary>
    /// Returns the platform architecture.
    /// https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001395/Scanner+Bootstrappers+implementation+guidelines.
    /// </summary>
    public virtual string Architecture { get; }

    /// <summary>
    /// If true the preprocessor should copy the loader targets to a user location where MSBuild will pick them up.
    /// </summary>
    public bool InstallLoaderTargets { get; }

    /// <summary>
    /// Path to the Java executable.
    /// </summary>
    public virtual string JavaExePath { get; }

    /// <summary>
    /// Skip JRE provisioning (default false).
    /// </summary>
    public virtual bool SkipJreProvisioning { get; }

    /// <summary>
    /// Path to the scanner-engine.jar.
    /// </summary>
    public virtual string EngineJarPath { get; }

    /// <summary>
    /// Force the usage of the SonarScanner CLI even if the engine jar is available.
    /// </summary>
    public virtual bool UseSonarScannerCli { get; }

    /// <summary>
    /// The sonar.userHome base directory for caching. Default value: ~/.sonar.
    /// </summary>
    public string UserHome { get; private set; }

    /// <summary>
    /// Enable or disable multi-language analysis (default true, enabled).
    /// </summary>
    public bool ScanAllAnalysis { get; }

    /// <summary>
    /// The path to the p12/pfx truststore file with certificates that the scanner trusts when connecting to a server.
    /// </summary>
    public string TruststorePath { get; private set; }

    /// <summary>
    /// The password to unlock the <see cref="TruststorePath"/>.
    /// </summary>
    public string TruststorePassword { get; private set; }

    /// <summary>
    /// Returns the combined command line and file analysis settings.
    /// </summary>
    public AggregatePropertiesProvider AggregateProperties { get; }

    public IAnalysisPropertyProvider CmdLineProperties { get; }

    public IAnalysisPropertyProvider ScannerEnvProperties { get; }

    /// <summary>
    /// Returns the name of property settings file or null if there is not one.
    /// </summary>
    public string PropertiesFileName
    {
        get
        {
            if (globalFileProperties is FilePropertyProvider fileProvider)
            {
                Debug.Assert(fileProvider.PropertiesFile is not null, "File properties should not be null");
                Debug.Assert(!string.IsNullOrWhiteSpace(fileProvider.PropertiesFile.FilePath), "Settings file name should not be null");
                return fileProvider.PropertiesFile.FilePath;
            }
            return null;
        }
    }

    internal bool IsValid { get; }

    public ProcessedArgs(
        string key,
        string name,
        string version,
        string organization,
        bool installLoaderTargets,
        IAnalysisPropertyProvider cmdLineProperties,
        IAnalysisPropertyProvider globalFileProperties,
        IAnalysisPropertyProvider scannerEnvProperties,
        BuildSettings buildSettings,
        IRuntime runtime)
    {
        IsValid = true;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key));
        }
        this.runtime = runtime;

        ProjectKey = key;
        IsValid &= CheckProjectKeyValidity(key);

        ProjectName = name;
        ProjectVersion = version;
        Organization = organization;

        CmdLineProperties = cmdLineProperties ?? throw new ArgumentNullException(nameof(cmdLineProperties));
        this.globalFileProperties = globalFileProperties ?? throw new ArgumentNullException(nameof(globalFileProperties));
        ScannerEnvProperties = scannerEnvProperties ?? throw new ArgumentNullException(nameof(scannerEnvProperties));
        InstallLoaderTargets = installLoaderTargets;

        IsValid &= CheckOrganizationValidity();
        AggregateProperties = new AggregatePropertiesProvider(cmdLineProperties, globalFileProperties, ScannerEnvProperties);
        TelemetryUtils.AddTelemetry(runtime.Telemetry, AggregateProperties);

        AggregateProperties.TryGetValue(SonarProperties.HostUrl, out var sonarHostUrl); // Used for SQ and may also be set to https://SonarCloud.io
        AggregateProperties.TryGetValue(SonarProperties.SonarcloudUrl, out var sonarcloudUrl);
        AggregateProperties.TryGetValue(SonarProperties.Region, out var region);
        AggregateProperties.TryGetValue(SonarProperties.ApiBaseUrl, out var apiBaseUrl);

        ServerInfo = HostInfo.FromProperties(runtime.Logger, sonarHostUrl, sonarcloudUrl, apiBaseUrl, region);
        IsValid &= ServerInfo is not null;
        TelemetryUtils.AddTelemetry(runtime.Telemetry, ServerInfo);

        OperatingSystem = OperatingSystemString(AggregateProperties);
        Architecture = AggregateProperties.TryGetProperty(SonarProperties.Architecture, out var architecture)
            ? architecture.Value
            : RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

        if (AggregateProperties.TryGetProperty(SonarProperties.JavaExePath, out var javaExePath))
        {
            if (!runtime.File.Exists(javaExePath.Value))
            {
                IsValid = false;
                runtime.LogError(Resources.ERROR_InvalidJavaExePath);
            }
            JavaExePath = javaExePath.Value;
        }
        if (AggregateProperties.TryGetProperty(SonarProperties.SkipJreProvisioning, out var skipJreProvisioningString))
        {
            if (!bool.TryParse(skipJreProvisioningString.Value, out var result))
            {
                IsValid = false;
                runtime.LogError(Resources.ERROR_InvalidSkipJreProvisioning);
            }
            SkipJreProvisioning = result;
        }
        if (AggregateProperties.TryGetProperty(SonarProperties.EngineJarPath, out var engineJarPath))
        {
            if (!runtime.File.Exists(engineJarPath.Value))
            {
                IsValid = false;
                runtime.LogError(Resources.ERROR_InvalidEngineJarPath);
            }
            EngineJarPath = engineJarPath.Value;
        }
        if (AggregateProperties.TryGetProperty(SonarProperties.ScanAllAnalysis, out var scanAllAnalysisString))
        {
            if (!bool.TryParse(scanAllAnalysisString.Value, out var result))
            {
                IsValid = false;
                runtime.LogError(Resources.ERROR_InvalidScanAllAnalysis);
            }
            ScanAllAnalysis = result;
        }
        else
        {
            ScanAllAnalysis = true;
        }

        if (AggregateProperties.TryGetProperty(SonarProperties.UseSonarScannerCLI, out var useSonarScannerCli))
        {
            if (!bool.TryParse(useSonarScannerCli.Value, out var result))
            {
                IsValid = false;
                runtime.LogError(Resources.ERROR_InvalidUseSonarScannerCli);
            }
            UseSonarScannerCli = result;
        }
        else
        {
            UseSonarScannerCli = false;
        }
#if NETFRAMEWORK
        if (buildSettings?.BuildEnvironment is BuildEnvironment.LegacyTeamBuild && !BuildSettings.SkipLegacyCodeCoverageProcessing)
        {
            UseSonarScannerCli = true;
        }
#endif

        if (AggregateProperties.TryGetProperty(SonarProperties.Sources, out _) || AggregateProperties.TryGetProperty(SonarProperties.Tests, out _))
        {
            runtime.Logger.LogUIWarning(Resources.WARN_SourcesAndTestsDeprecated);
        }
        IsValid &= SetUserHome();
        IsValid &= SetTrustStoreProperties();
        HttpTimeout = TimeoutProvider.HttpTimeout(AggregateProperties, runtime.Logger);
    }

    protected /* for testing */ ProcessedArgs() { }

    /// <summary>
    /// Returns the value for the specified setting.
    /// Throws if the setting does not exist.
    /// </summary>
    public string Setting(string key)
    {
        if (AggregateProperties.TryGetValue(key, out var value))
        {
            return value;
        }

        var message = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.ERROR_MissingSetting, key);
        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Returns the value for the specified setting, or the supplied
    /// default if the setting does not exist.
    /// </summary>
    public string SettingOrDefault(string key, string defaultValue)
    {
        if (!AggregateProperties.TryGetValue(key, out var value))
        {
            value = defaultValue;
        }
        return value;
    }

    public /* for testing */ virtual bool TryGetSetting(string key, out string value) =>
        AggregateProperties.TryGetValue(key, out value);

    public IEnumerable<Property> AllProperties() =>
        AggregateProperties.GetAllProperties();

    private string OperatingSystemString(IAnalysisPropertyProvider properties) =>
        properties.TryGetProperty(SonarProperties.OperatingSystem, out var operatingSystem)
            ? operatingSystem.Value
            : runtime.OperatingSystem.OperatingSystem() switch
            {
                PlatformOS.Windows => "windows",
                PlatformOS.MacOSX => "macos",
                PlatformOS.Alpine => "alpine",
                PlatformOS.Linux => "linux",
                _ => null
            };

    private bool CheckOrganizationValidity()
    {
        if (Organization is null && globalFileProperties.TryGetValue(SonarProperties.Organization, out _))
        {
            runtime.LogError(Resources.ERROR_Organization_Provided_In_SonarQubeAnalysis_file);
            return false;
        }
        return true;
    }

    private bool CheckProjectKeyValidity(string key)
    {
        if (!ProjectKeyRegEx.SafeIsMatch(key, timeoutFallback: true))
        {
            runtime.LogError(Resources.ERROR_InvalidProjectKeyArg);
            return false;
        }
        return true;
    }

    private bool SetUserHome()
    {
        if (AggregateProperties.TryGetProperty(SonarProperties.UserHome, out var userHomeProp))
        {
            if (runtime.Directory.Exists(userHomeProp.Value))
            {
                UserHome = userHomeProp.Value;
                return true;
            }
            else
            {
                try
                {
                    runtime.Directory.CreateDirectory(userHomeProp.Value);
                    UserHome = userHomeProp.Value;
                    runtime.LogDebug(Resources.MSG_UserHomeDirectoryCreated, UserHome);
                    return true;
                }
                catch (Exception ex)
                {
                    runtime.LogError(Resources.ERR_UserHomeInvalid, userHomeProp.Value, ex.Message);
                    UserHome = null;
                    return false;
                }
            }
        }
        var defaultPath = Path.Combine(runtime.OperatingSystem.FolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.None), ".sonar");
        if (!runtime.Directory.Exists(defaultPath))
        {
            try
            {
                runtime.Directory.CreateDirectory(defaultPath);
            }
            catch (Exception ex)
            {
                runtime.LogWarning(Resources.WARN_DefaultUserHomeCreationFailed, defaultPath, ex.Message);
                UserHome = null;
                return true;
            }
        }
        UserHome = defaultPath;
        return true;
    }

    private bool SetTrustStoreProperties()
    {
        TruststorePassword = null;
        var hasPath = AggregateProperties.TryGetProperty(SonarProperties.TruststorePath, out var truststorePathProperty);
        var hasPassword = AggregateProperties.TryGetProperty(SonarProperties.TruststorePassword, out var truststorePasswordProperty);
        if (hasPassword)
        {
            TruststorePassword = truststorePasswordProperty.Value;
        }
        if (hasPath)
        {
            TruststorePath = truststorePathProperty.Value;
            TruststorePassword ??= TruststoreUtils.TruststoreDefaultPassword(TruststorePath, runtime.Logger);
            return CheckTrustStorePath(true);
        }
        else
        {
            runtime.LogDebug(Resources.MSG_NoTruststoreProvideTryDefault);
            // If the default truststore does not exist, providing the password does not make sense
            if (!DefaultTrustStoreProperties() && hasPassword)
            {
                runtime.LogError(Resources.ERR_TruststorePasswordWithoutTruststorePath);
                return false;
            }
            // If the default truststore cannot be opened, it should be as if the user did not specify a truststore and password
            // So certificate validation can be done against the system trust store.
            if (TruststorePath is null || !CheckTrustStorePath(false))
            {
                runtime.LogDebug(Resources.MSG_NoTruststoreProceedWithoutTruststore);
                TruststorePath = null;
                TruststorePassword   = null;
                return true;
            }
            runtime.LogDebug(Resources.MSG_FallbackTruststoreDefaultPath, TruststorePath);
        }
        return true;
    }

    private bool DefaultTrustStoreProperties()
    {
        var sonarUserHome = Environment.GetEnvironmentVariable(EnvironmentVariables.SonarUserHome) ?? UserHome;

        if (sonarUserHome is null)
        {
            TruststorePath = null;
            return false;
        }

        var sonarUserHomeCertPath = Path.Combine(sonarUserHome.Trim('"', '\''), SonarPropertiesDefault.TruststorePath);
        TruststorePath = !string.IsNullOrWhiteSpace(sonarUserHome) && runtime.File.Exists(sonarUserHomeCertPath) ? sonarUserHomeCertPath : null;

        TruststorePassword ??= TruststoreUtils.TruststoreDefaultPassword(TruststorePath, runtime.Logger);

        return TruststorePath is not null;
    }

    private bool CheckTrustStorePath(bool logAsError)
    {
        if (!runtime.File.Exists(TruststorePath))
        {
            runtime.LogError(Resources.ERR_TruststorePathDoesNotExist, TruststorePath);
            return false;
        }
        try
        {
            using var stream = runtime.File.Open(TruststorePath);
            return true;
        }
        catch (Exception ex) when (ex
            is ArgumentException
            or ArgumentNullException
            or PathTooLongException
            or DirectoryNotFoundException
            or IOException
            or FileNotFoundException
            or UnauthorizedAccessException
            or ArgumentOutOfRangeException
            or FileNotFoundException
            or NotSupportedException)
        {
            Action<string, string[]> log = logAsError ? runtime.Logger.LogError : runtime.Logger.LogDebug;
            log(Resources.ERR_TruststorePathCannotOpen, [TruststorePath, $"{ex.GetType()}: {ex.Message}"]);
            return false;
        }
    }
}
