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

using System.Xml.Serialization;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Data class to describe a single project configuration for our analyzers.
/// </summary>
/// <remarks>The class is XML-serializable. Each change in this class has to be propageted to it's counterpart in sonar-dotnet analyzers.</remarks>
[XmlRoot(ElementName = "SonarProjectConfig", Namespace = "http://www.sonarsource.com/msbuild/analyzer/2021/1")]
public class ProjectConfig
{
    /// <summary>
    /// Full path to the SonarQubeAnalysisConfig.xml file.
    /// </summary>
    public string AnalysisConfigPath { get; set; }

    /// <summary>
    /// The full name and path of the project file.
    /// </summary>
    public string ProjectPath { get; set; }

    /// <summary>
    ///The full name and path of the text file containing all files to analyze.
    /// </summary>
    public string FilesToAnalyzePath { get; set; }

    /// <summary>
    /// Root of the project-specific output directory. Analyzer should write protobuf and other files there.
    /// </summary>
    public string OutPath { get; set; }

    /// <summary>
    /// The kind of the project.
    /// </summary>
    public ProjectType ProjectType { get; set; }

    /// <summary>
    /// MSBuild target framework for the current build.
    /// </summary>
    public string TargetFramework { get; set; }

    /// <summary>
    /// The location of the project.assets.json file produced by a NuGet restore.
    /// This file location corresponds to the MSBuild property 'ProjectAssetsFile'.
    /// </summary>
    public string ProjectAssetsFile { get; set; }

    /// <summary>
    /// Saves the project configuration to the specified file as XML.
    /// </summary>
    public void Save(string fileName) =>
        Serializer.SaveModel(this, fileName);

    /// <summary>
    /// Loads and returns project config from the specified XML file
    /// </summary>
    public static ProjectConfig Load(string fileName) =>
        Serializer.LoadModel<ProjectConfig>(fileName);
}
