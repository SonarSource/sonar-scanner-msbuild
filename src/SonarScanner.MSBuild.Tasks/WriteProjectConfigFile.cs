/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SonarScanner.MSBuild.Tasks;

/// <summary>
/// MSBuild task to write a SonarProjectConfig file to disk in XML format
/// </summary>
/// <remarks>SonarProjectConfig.xml file is used to pass information from Scanner to the Analyzers.</remarks>
public class WriteProjectConfigFile : Task
{
    #region Input properties

    /// <summary>
    /// Project specific config directory where SonarProjectConfig.xml will be saved.
    /// </summary>
    [Required]
    public string ConfigDir { get; set; }

    public string AnalysisConfigPath { get; set; }

    public string ProjectPath { get; set; }

    public string FilesToAnalyzePath { get; set; }

    /// <summary>
    /// Project specific output directory for protobuf files.
    /// </summary>
    public string OutPath { get; set; }

    public bool IsTest { get; set; }

    public string TargetFramework { get; set; }

    public string ProjectAssetsFile { get; set; }

    #endregion Input properties

    [Output]
    public string ProjectConfigFilePath { get; private set; }

    #region Overrides

    public override bool Execute()
    {
        ProjectConfigFilePath = Path.Combine(ConfigDir, FileConstants.ProjectConfigFileName);
        var config = new ProjectConfig
        {
            AnalysisConfigPath = AnalysisConfigPath,
            ProjectPath = ProjectPath,
            FilesToAnalyzePath = FilesToAnalyzePath,
            OutPath = OutPath,
            ProjectType = IsTest ? ProjectType.Test : ProjectType.Product,
            TargetFramework = TargetFramework,
            ProjectAssetsFile = ProjectAssetsFile,
        };
        config.Save(ProjectConfigFilePath);
        return true;
    }

    #endregion Overrides
}
