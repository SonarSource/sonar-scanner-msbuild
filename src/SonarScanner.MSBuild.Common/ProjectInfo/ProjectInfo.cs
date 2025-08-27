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

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// XML-serializable data class to describe a single project.
/// </summary>
[XmlRoot(Namespace = XmlNamespace)]
public class ProjectInfo
{
    public const string XmlNamespace = "http://www.sonarsource.com/msbuild/integration/2015/1";

    public string ProjectName { get; set; }
    public string ProjectLanguage { get; set; }
    public ProjectType ProjectType { get; set; }
    public Guid ProjectGuid { get; set; }
    public string FullPath { get; set; }    // Path to csproj/vbproj/*proj
    public bool IsExcluded { get; set; }
    public string Encoding { get; set; }    // Default encoding for files without BOM
    public List<AnalysisResult> AnalysisResults { get; set; }
    public AnalysisProperties AnalysisSettings { get; set; }
    public string Configuration { get; set; }   // MsBuild /p:Configuration:Release|Debug|SomethingElse parameter
    public string Platform { get; set; }        // MsBuild /p:Platform parameter
    public string TargetFramework { get; set; }

    public void Save(string fileName) =>
        Serializer.SaveModel(this, fileName);

    public static ProjectInfo Load(string fileName) =>
        Serializer.LoadModel<ProjectInfo>(fileName);

    public AnalysisResult FindAnalysisResultFile(AnalysisResultFileType fileType) =>
        FindAnalysisResultFile(fileType.ToString());

    public AnalysisResult FindAnalysisResultFile(string id) =>
        AnalysisResults?.FirstOrDefault(x => AnalysisResult.ResultKeyComparer.Equals(id, x.Id));

    public Property FindAnalysisSetting(string id) =>
        AnalysisSettings?.FirstOrDefault(x => Property.AreKeysEqual(id, x.Id));

    public void AddAnalyzerResult(AnalysisResultFileType fileType, string location) =>
        AddAnalyzerResult(fileType.ToString(), location);

    public void AddAnalyzerResult(string id, string location)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(nameof(id));
        }
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentNullException(nameof(location));
        }

        AnalysisResults ??= [];
        AnalysisResults.Add(new() { Id = id, Location = location });
    }

    public DirectoryInfo ProjectFileDirectory() =>
        string.IsNullOrWhiteSpace(FullPath) ? null : new FileInfo(FullPath).Directory;

    public string ProjectGuidAsString() =>
        ProjectGuid.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant();

    public FileInfo[] AllAnalysisFiles(ILogger logger)
    {
        if (FindAnalysisResultFile(AnalysisResultFileType.FilesToAnalyze)?.Location is { } filesToAnalyze && File.Exists(filesToAnalyze))
        {
            var result = new List<FileInfo>();
            foreach (var path in File.ReadAllLines(filesToAnalyze))
            {
                try
                {
                    result.Add(new FileInfo(path));
                }
                catch (Exception ex)
                {
                    logger.LogDebug(Resources.MSG_AnalysisFileCouldNotBeAdded, path, ex.Message);
                }
            }
            return result.ToArray();
        }
        else
        {
            return [];
        }
    }
}
