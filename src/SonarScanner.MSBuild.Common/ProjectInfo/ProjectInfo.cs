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

    public bool TryGetAnalyzerResult(AnalysisResultFileType fileType, out AnalysisResult result)
    {
        return TryGetAnalyzerResult(fileType.ToString(), out result);
    }

    public bool TryGetAnalyzerResult(string id, out AnalysisResult result)
    {
        result = null;

        if (AnalysisResults != null)
        {
            result = AnalysisResults.FirstOrDefault(x => AnalysisResult.ResultKeyComparer.Equals(id, x.Id));
        }
        return result != null;
    }

    public bool TryGetAnalysisSetting(string id, out Property result)
    {
        result = null;

        if (AnalysisSettings != null)
        {
            result = AnalysisSettings.FirstOrDefault(x => Property.AreKeysEqual(id, x.Id));
        }
        return result != null;
    }

    public void AddAnalyzerResult(AnalysisResultFileType fileType, string location)
    {
        AddAnalyzerResult(fileType.ToString(), location);
    }

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

        if (AnalysisResults == null)
        {
            AnalysisResults = new List<AnalysisResult>();
        }

        var result = new AnalysisResult() { Id = id, Location = location };
        AnalysisResults.Add(result);
    }

    public DirectoryInfo ProjectFileDirectory()
    {
        return string.IsNullOrWhiteSpace(FullPath) ? null : new FileInfo(FullPath).Directory;
    }

    public string ProjectGuidAsString()
    {
        return ProjectGuid.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant();
    }

    public string TryGetAnalysisFileLocation(AnalysisResultFileType fileType)
    {
        if (TryGetAnalyzerResult(fileType, out var result))
        {
            return result.Location;
        }

        return null;
    }

    public FileInfo[] AllAnalysisFiles(ILogger logger)
    {
        var compiledFilesPath = TryGetAnalysisFileLocation(AnalysisResultFileType.FilesToAnalyze);
        if (compiledFilesPath is null
            || !File.Exists(compiledFilesPath))
        {
            return [];
        }

        var result = new List<FileInfo>();
        foreach (var path in File.ReadAllLines(compiledFilesPath))
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
        return [.. result];
    }
}
