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

namespace SonarScanner.MSBuild.PreProcessor;

/// <summary>
/// Data class to hold validated command line arguments required by the pre-processor.
/// </summary>
public class ProcessedArgs
{
    /// <summary>
    /// Regular expression to validate a project key.
    /// See http://docs.sonarqube.org/display/SONAR/Project+Administration#ProjectAdministration-AddingaProject
    /// </summary>
    /// <remarks>Should match the java regex here: https://github.com/SonarSource/sonarqube/blob/5.1.1/sonar-core/src/main/java/org/sonar/core/component/ComponentKeys.java#L36
    /// "Allowed characters are alphanumeric, '-', '_', '.' and ':', with at least one non-digit".
    /// </remarks>
    private static readonly Regex ProjectKeyRegEx = new(@"^[a-zA-Z0-9:\-_\.]*[a-zA-Z:\-_\.]+[a-zA-Z0-9:\-_\.]*$", RegexOptions.Compiled | RegexOptions.Singleline, RegexConstants.DefaultTimeout);

    private readonly IAnalysisPropertyProvider globalFileProperties;
    private readonly OperatingSystemProvider operatingSystemProvider;

    public /* for testing */ virtual string ProjectKey { get; }

    public string ProjectName { get; }

    public string ProjectVersion { get; }

    public TimeSpan HttpTimeout { get; }

    public /* for testing */ virtual string Organization { get; }

    public HostInfo ServerInfo { get; }

    /// <summary>
    /// Returns the operating system used to run the scanner.
    /// Supported values are windows|linux|macos|alpine but more can be added later
    /// https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001395/Scanner+Bootstrappers+implementation+guidelines
    public virtual string OperatingSystem { get; }

    /// <summary>
    /// Returns the platform architecture.
    /// https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001395/Scanner+Bootstrappers+implementation+guidelines
    /// </summary>
    public virtual string Architecture { get; }

    /// <summary>
    /// If true the preprocessor should copy the loader targets to a user location where MSBuild will pick them up.
    /// </summary>
    public bool InstallLoaderTargets { get; private set; }

    /// <summary>
    /// Path to the Java executable.
    /// </summary>
    public virtual string JavaExePath { get; }

    /// <summary>
    /// Skip JRE provisioning (default false).
    /// </summary>
    public virtual bool SkipJreProvisioning { get; }

    /// <summary>
    /// Path to the Java executable.
    /// </summary>
    public virtual string EngineJarPath { get; }

    /// <summary>
    /// The sonar.userHome base directory for caching. Default value: ~/.sonar
    /// </summary>
    public string UserHome { get; }

    /// <summary>
    /// Enable or disable multi-language analysis (default true, enabled).
    /// </summary>
    public bool ScanAllAnalysis { get; }

    /// <summary>
    /// The path to the p12/pfx truststore file with certificates that the scanner trusts when connecting to a server.
    /// </summary>
    public string TruststorePath { get; }

    /// <summary>
    /// The password to unlock the <see cref="TruststorePath"/>.
    /// </summary>
    public string TruststorePassword { get; }

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
                Debug.Assert(!string.IsNullOrWhiteSpace(fileProvider.PropertiesFile.FilePath),
                    "Settings file name should not be null");
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
        IFileWrapper fileWrapper,
        IDirectoryWrapper directoryWrapper,
        OperatingSystemProvider operatingSystemProvider,
        ILogger logger)
    {
        IsValid = true;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        ProjectKey = key;
        IsValid &= CheckProjectKeyValidity(key, logger);

        ProjectName = name;
        ProjectVersion = version;
        Organization = organization;

        CmdLineProperties = cmdLineProperties ?? throw new ArgumentNullException(nameof(cmdLineProperties));
        this.globalFileProperties = globalFileProperties ?? throw new ArgumentNullException(nameof(globalFileProperties));
        this.operatingSystemProvider = operatingSystemProvider;
        ScannerEnvProperties = scannerEnvProperties ?? throw new ArgumentNullException(nameof(scannerEnvProperties));
        InstallLoaderTargets = installLoaderTargets;

        IsValid &= CheckOrganizationValidity(logger);
        AggregateProperties = new AggregatePropertiesProvider(cmdLineProperties, globalFileProperties, ScannerEnvProperties);
        TelemetryUtils.AddTelemetry(logger, AggregateProperties);

        AggregateProperties.TryGetValue(SonarProperties.HostUrl, out var sonarHostUrl); // Used for SQ and may also be set to https://SonarCloud.io
        AggregateProperties.TryGetValue(SonarProperties.SonarcloudUrl, out var sonarcloudUrl);
        AggregateProperties.TryGetValue(SonarProperties.Region, out var region);
        AggregateProperties.TryGetValue(SonarProperties.ApiBaseUrl, out var apiBaseUrl);

        ServerInfo = HostInfo.FromProperties(logger, sonarHostUrl, sonarcloudUrl, apiBaseUrl, region);
        IsValid &= ServerInfo is not null;
        TelemetryUtils.AddTelemetry(logger, ServerInfo);

        OperatingSystem = GetOperatingSystem(AggregateProperties);
        Architecture = AggregateProperties.TryGetProperty(SonarProperties.Architecture, out var architecture)
            ? architecture.Value
            : RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

        if (AggregateProperties.TryGetProperty(SonarProperties.JavaExePath, out var javaExePath))
        {
            if (!fileWrapper.Exists(javaExePath.Value))
            {
                IsValid = false;
                logger.LogError(Resources.ERROR_InvalidJavaExePath);
            }
            JavaExePath = javaExePath.Value;
        }
        if (AggregateProperties.TryGetProperty(SonarProperties.SkipJreProvisioning, out var skipJreProvisioningString))
        {
            if (!bool.TryParse(skipJreProvisioningString.Value, out var result))
            {
                IsValid = false;
                logger.LogError(Resources.ERROR_InvalidSkipJreProvisioning);
            }
            SkipJreProvisioning = result;
        }
        if (AggregateProperties.TryGetProperty(SonarProperties.EngineJarPath, out var engineJarPath))
        {
            if (!fileWrapper.Exists(engineJarPath.Value))
            {
                IsValid = false;
                logger.LogError(Resources.ERROR_InvalidEngineJarPath);
            }
            EngineJarPath = engineJarPath.Value;
        }
        if (AggregateProperties.TryGetProperty(SonarProperties.ScanAllAnalysis, out var scanAllAnalysisString))
        {
            if (!bool.TryParse(scanAllAnalysisString.Value, out var result))
            {
                IsValid = false;
                logger.LogError(Resources.ERROR_InvalidScanAllAnalysis);
            }
            ScanAllAnalysis = result;
        }
        else
        {
            ScanAllAnalysis = true;
        }
        if (AggregateProperties.TryGetProperty(SonarProperties.Sources, out _) || AggregateProperties.TryGetProperty(SonarProperties.Tests, out _))
        {
            logger.LogUIWarning(Resources.WARN_SourcesAndTestsDeprecated);
        }
        IsValid &= TryGetUserHome(logger, directoryWrapper, out var userHome);
        UserHome = userHome;
        IsValid &= CheckTrustStoreProperties(logger, fileWrapper, out var truststorePath, out var truststorePassword);
        TruststorePath = truststorePath;
        TruststorePassword = truststorePassword;
        HttpTimeout = TimeoutProvider.HttpTimeout(AggregateProperties, logger);
    }

    protected /* for testing */ ProcessedArgs() { }

    /// <summary>
    /// Returns the value for the specified setting.
    /// Throws if the setting does not exist.
    /// </summary>
    public string GetSetting(string key)
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
    public string GetSetting(string key, string defaultValue)
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

    private string GetOperatingSystem(IAnalysisPropertyProvider properties) =>
        properties.TryGetProperty(SonarProperties.OperatingSystem, out var operatingSystem)
            ? operatingSystem.Value
            : operatingSystemProvider.OperatingSystem() switch
            {
                PlatformOS.Windows => "windows",
                PlatformOS.MacOSX => "macos",
                PlatformOS.Alpine => "alpine",
                PlatformOS.Linux => "linux",
                _ => null
            };

    private bool CheckOrganizationValidity(ILogger logger)
    {
        if (Organization is null && globalFileProperties.TryGetValue(SonarProperties.Organization, out _))
        {
            logger.LogError(Resources.ERROR_Organization_Provided_In_SonarQubeAnalysis_file);
            return false;
        }
        return true;
    }

    private static bool CheckProjectKeyValidity(string key, ILogger logger)
    {
        if (!ProjectKeyRegEx.SafeIsMatch(key, timeoutFallback: true))
        {
            logger.LogError(Resources.ERROR_InvalidProjectKeyArg);
            return false;
        }
        return true;
    }

    private bool TryGetUserHome(ILogger logger, IDirectoryWrapper directoryWrapper, out string userHome)
    {
        if (AggregateProperties.TryGetProperty(SonarProperties.UserHome, out var userHomeProp))
        {
            if (directoryWrapper.Exists(userHomeProp.Value))
            {
                userHome = userHomeProp.Value;
                return true;
            }
            else
            {
                try
                {
                    directoryWrapper.CreateDirectory(userHomeProp.Value);
                    userHome = userHomeProp.Value;
                    logger.LogDebug(Resources.MSG_UserHomeDirectoryCreated, userHome);
                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogError(Resources.ERR_UserHomeInvalid, userHomeProp.Value, ex.Message);
                    userHome = null;
                    return false;
                }
            }
        }
        var defaultPath = Path.Combine(operatingSystemProvider.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.None), ".sonar");
        if (!directoryWrapper.Exists(defaultPath))
        {
            try
            {
                directoryWrapper.CreateDirectory(defaultPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(Resources.WARN_DefaultUserHomeCreationFailed, defaultPath, ex.Message);
                userHome = null;
                return true;
            }
        }
        userHome = defaultPath;
        return true;
    }

    private bool CheckTrustStoreProperties(ILogger logger, IFileWrapper fileWrapper, out string truststorePath, out string truststorePassword)
    {
        truststorePath = null;
        truststorePassword = null;
        var hasPath = AggregateProperties.TryGetProperty(SonarProperties.TruststorePath, out var truststorePathProperty);
        var hasPassword = AggregateProperties.TryGetProperty(SonarProperties.TruststorePassword, out var truststorePasswordProperty);
        if (hasPassword)
        {
            truststorePassword = truststorePasswordProperty.Value;
        }
        if (hasPath)
        {
            truststorePath = truststorePathProperty.Value;
            truststorePassword ??= TruststoreUtils.TruststoreDefaultPassword(truststorePath, logger);
            return CheckTrustStorePath(logger, fileWrapper, truststorePath, true);
        }
        else
        {
            logger.LogDebug(Resources.MSG_NoTruststoreProvideTryDefault);
            // If the default truststore does not exist, providing the password does not make sense
            if (!DefaultTrustStoreProperties(fileWrapper, out truststorePath, ref truststorePassword, logger) && hasPassword)
            {
                logger.LogError(Resources.ERR_TruststorePasswordWithoutTruststorePath);
                return false;
            }
            // If the default truststore cannot be opened, it should be as if the user did not specify a truststore and password
            // So certificate validation can be done against the system trust store.
            if (truststorePath is null || !CheckTrustStorePath(logger, fileWrapper, truststorePath, false))
            {
                logger.LogDebug(Resources.MSG_NoTruststoreProceedWithoutTruststore);
                truststorePath = null;
                truststorePassword = null;
                return true;
            }
            logger.LogDebug(Resources.MSG_FallbackTruststoreDefaultPath, truststorePath);
        }
        return true;
    }

    private bool DefaultTrustStoreProperties(IFileWrapper fileWrapper, out string truststorePath, ref string truststorePassword, ILogger logger)
    {
        var sonarUserHome = Environment.GetEnvironmentVariable(EnvironmentVariables.SonarUserHome) ?? UserHome;

        if (sonarUserHome is null)
        {
            truststorePath = null;
            return false;
        }

        var sonarUserHomeCertPath = Path.Combine(sonarUserHome.Trim('"', '\''), SonarPropertiesDefault.TruststorePath);
        truststorePath = !string.IsNullOrWhiteSpace(sonarUserHome) && fileWrapper.Exists(sonarUserHomeCertPath) ? sonarUserHomeCertPath : null;

        truststorePassword ??= TruststoreUtils.TruststoreDefaultPassword(truststorePath, logger);

        return truststorePath is not null;
    }

    private static bool CheckTrustStorePath(ILogger logger, IFileWrapper fileWrapper, string truststorePath, bool logAsError)
    {
        if (!fileWrapper.Exists(truststorePath))
        {
            logger.LogError(Resources.ERR_TruststorePathDoesNotExist, truststorePath);
            return false;
        }
        try
        {
            using var stream = fileWrapper.Open(truststorePath);
            return true;
        }
        catch (Exception ex) when (ex is
            ArgumentException or
            ArgumentNullException or
            PathTooLongException or
            DirectoryNotFoundException or
            IOException or
            FileNotFoundException or
            UnauthorizedAccessException or
            ArgumentOutOfRangeException or
            FileNotFoundException or
            NotSupportedException)
        {
            Action<string, string[]> log = logAsError ? logger.LogError : logger.LogDebug;
            log(Resources.ERR_TruststorePathCannotOpen, [truststorePath, $"{ex.GetType()}: {ex.Message}"]);
            return false;
        }
    }
}
