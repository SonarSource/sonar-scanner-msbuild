/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Xml.Serialization;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Data class to describe the analysis settings for a single SonarQube project
/// </summary>
/// <remarks>The class is XML-serializable</remarks>
[XmlRoot(Namespace = XmlNamespace)]
public class AnalysisConfig
{
    public const string XmlNamespace = ProjectInfo.XmlNamespace;

    public string SonarConfigDir { get; set; }

    public string SonarOutputDir { get; set; }

    public string SonarBinDir { get; set; }

    /// <summary>
    /// The working directory as perceived by the user, i.e. the current directory for command line builds
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
    /// The option that enables or disables multi-language analysis.
    /// </summary>
    public bool ScanAllAnalysis { get; set; }

    /// <summary>
    /// Indicates whether or not credentials were passed as command line argument during the begin step.
    /// </summary>
    public bool HasBeginStepCommandLineCredentials { get; set; }

    public string SonarQubeHostUrl { get; set; }

    public string SonarQubeVersion { get; set; }

    public string SonarProjectKey { get; set; }

    public string SonarProjectVersion { get; set; }

    public string SonarProjectName { get; set; }

    /// <summary>
    /// List of additional configuration-related settings
    /// e.g. the build system identifier, if appropriate.
    /// </summary>
    /// <remarks>These settings will not be supplied to the sonar-scanner.</remarks>
    public List<ConfigSetting> AdditionalConfig { get; set; }

    /// <summary>
    /// List of analysis settings inherited from the SonarQube server
    /// </summary>
    public AnalysisProperties ServerSettings { get; set; }

    /// <summary>
    /// List of analysis settings supplied locally (either on the
    /// command line, in a file or through the scanner environment variable)
    /// </summary>
    public AnalysisProperties LocalSettings { get; set; }

    /// <summary>
    /// Configuration for Roslyn analyzers
    /// </summary>
    public IList<AnalyzerSettings> AnalyzersSettings { get; set; }

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
}
