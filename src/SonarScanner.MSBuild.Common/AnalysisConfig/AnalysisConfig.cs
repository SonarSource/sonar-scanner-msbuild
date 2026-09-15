/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Data class to describe the analysis settings for a single SonarQube project.
/// </summary>
/// <remarks>The class is XML-serializable.</remarks>
[XmlRoot(Namespace = ProjectInfo.XmlNamespace)]
public class AnalysisConfig
{
    private const string SettingsFileKey = "settings.file.path";
    private const string BuildUriSettingId = "BuildUri";

    public string SonarConfigDir { get; set; }

    public string SonarOutputDir { get; set; }

    public string SonarBinDir { get; set; }

    /// <summary>
    /// The working directory as perceived by the user, i.e. the current directory for command line builds.
    /// </summary>
    /// <remarks>Users expect to specify paths relative to the working directory and not to the location of the sonar-scanner program.
    ///  See https://jira.sonarsource.com/browse/SONARMSBRU-100 for details.</remarks>
    public string SonarScannerWorkingDirectory { get; set; }

    /// <summary>
    /// Parent directory of the source files.
    /// </summary>
    /// <remarks>SCM plugins like Git or TFVC expect to find .git or $tf subdirectories directly under the sources directory
    /// in order to and provide annotations. </remarks>
    public string SourcesDirectory { get; set; }

    /// <summary>
    /// The Java exe path to be used by the end step to call the scanner cli or engine.
    /// </summary>
    public string JavaExePath { get; set; }

    /// <summary>
    /// The path to the Scanner Engine jar.
    /// </summary>
    public string EngineJarPath { get; set; }

    /// <summary>
    /// The path to the SonarScanner Cli.
    /// </summary>
    public string SonarScannerCliPath { get; set; }

    /// <summary>
    /// Use the SonarScanner CLI instead of the engine jar.
    /// </summary>
    public bool UseSonarScannerCli { get; set; }

    /// <summary>
    /// The option that enables or disables multi-language analysis.
    /// </summary>
    public bool ScanAllAnalysis { get; set; }

    /// <summary>
    /// Indicates whether credentials were passed as command line argument during the begin step.
    /// </summary>
    public bool HasBeginStepCommandLineCredentials { get; set; }

    /// <summary>
    /// Indicates whether a certifcate store file password was passed as command line argument during the begin step.
    /// </summary>
    public bool HasBeginStepCommandLineTruststorePassword { get; set; }

    public string SonarQubeHostUrl { get; set; }

    public string SonarQubeVersion { get; set; }

    public string SonarProjectKey { get; set; }

    public string SonarProjectVersion { get; set; }

    public string SonarProjectName { get; set; }

    /// <summary>
    /// List of additional configuration-related settings e.g. the build system identifier, if appropriate.
    /// </summary>
    /// <remarks>These settings will not be supplied to the sonar-scanner.</remarks>
    public List<ConfigSetting> AdditionalConfig { get; set; }

    /// <summary>
    /// List of analysis settings inherited from the SonarQube instance.
    /// </summary>
    public AnalysisProperties ServerSettings { get; set; }

    /// <summary>
    /// List of analysis settings supplied locally (either on the command line, in a file or through the scanner environment variable).
    /// </summary>
    public AnalysisProperties LocalSettings { get; set; }

    /// <summary>
    /// List of analysis settings supplied locally (on the command line) that has to be passed to the scanner through the SONAR_SCANNER_OPTS environment variable
    /// <see href="https://github.com/SonarSource/sonar-scanner-cli/blob/7d791c2465384b71465a6c05d23174fefdbfa213/src/main/assembly/bin/sonar-scanner.bat#L72C65-L72C73">sonar-scanner.bat</see>.
    /// </summary>
    public AnalysisProperties ScannerOptsSettings { get; } = [];

    /// <summary>
    /// Configuration for Roslyn analyzers.
    /// </summary>
    public List<AnalyzerSettings> AnalyzersSettings { get; set; }

    [XmlIgnore]
    public string FileName { get; private set; }

    public void Save(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        Serializer.SaveModel(this, fileName);
        FileName = fileName;
    }

    public static AnalysisConfig Load(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        var model = Serializer.LoadModel<AnalysisConfig>(fileName);
        model.FileName = fileName;
        return model;
    }

    public string GetBuildUri() =>
        GetConfigValue(BuildUriSettingId, null);

    public void SetBuildUri(string uri) =>
        SetConfigValue(BuildUriSettingId, uri);

    public string GetConfigValue(string settingId, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(settingId))
        {
            throw new ArgumentNullException(nameof(settingId));
        }

        var result = defaultValue;

        if (TryGetConfigSetting(settingId, out var setting))
        {
            result = setting.Value;
        }

        return result;
    }

    public void SetConfigValue(string settingId, string value)
    {
        SetValue(settingId, value);
    }

    /// <summary>
    /// Returns a provider containing the analysis settings coming from all providers (analysis config file, environment, settings file).
    /// </summary>
    public IAnalysisPropertyProvider AnalysisSettings(bool includeServerSettings, ILogger logger)
    {
        _ = logger ?? throw new ArgumentNullException(nameof(logger));
        var providers = new List<IAnalysisPropertyProvider>();
        // Note: the order in which the providers are added determines the precedence

        // Add local settings
        if (LocalSettings is not null)
        {
            providers.Add(new ListPropertiesProvider(LocalSettings));
        }

        // Add file settings
        var settingsFilePath = GetSettingsFilePath();
        if (settingsFilePath is not null)
        {
            var fileProvider = new ListPropertiesProvider(AnalysisProperties.Load(settingsFilePath));
            providers.Add(fileProvider);
        }

        // Add scanner environment settings
        if (EnvScannerPropertiesProvider.TryCreateProvider(logger, out var envProvider))
        {
            providers.Add(envProvider);
        }

        // Add server settings
        if (includeServerSettings && ServerSettings is not null)
        {
            providers.Add(new ListPropertiesProvider(ServerSettings));
        }

        return providers.Count switch
        {
            0 => EmptyPropertyProvider.Instance,
            1 => providers[0],
            _ => new AggregatePropertiesProvider(providers.ToArray()),
        };
    }

    public void SetSettingsFilePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }
        SetValue(SettingsFileKey, fileName);
    }

    public string GetSettingsFilePath()
    {
        if (TryGetConfigSetting(SettingsFileKey, out var setting))
        {
            return setting.Value;
        }
        return null;
    }

    public string GetSettingOrDefault(string settingName, bool includeServerSettings, string defaultValue, ILogger logger)
    {
        if (settingName == null)
        {
            throw new ArgumentNullException(nameof(settingName));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        if (AnalysisSettings(includeServerSettings, logger).TryGetValue(settingName, out var value))
        {
            return value;
        }
        return defaultValue;
    }

    private bool TryGetConfigSetting(string settingId, out ConfigSetting result)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(settingId), "Setting id should not be null/empty");

        result = null;

        if (AdditionalConfig != null)
        {
            result = AdditionalConfig.FirstOrDefault(ar => ConfigSetting.SettingKeyComparer.Equals(settingId, ar.Id));
        }
        return result != null;
    }

    private void SetValue(string settingId, string value)
    {
        if (string.IsNullOrWhiteSpace(settingId))
        {
            throw new ArgumentNullException(nameof(settingId));
        }

        if (TryGetConfigSetting(settingId, out var setting))
        {
            setting.Value = value;
        }
        else
        {
            setting = new ConfigSetting()
            {
                Id = settingId,
                Value = value
            };
        }

        if (AdditionalConfig == null)
        {
            AdditionalConfig = new System.Collections.Generic.List<ConfigSetting>();
        }
        AdditionalConfig.Add(setting);
    }
}
