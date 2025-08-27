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

public static class ProjectInfoExtensions
{
    public static bool TryGetAnalyzerResult(this ProjectInfo projectInfo, AnalysisResultFileType fileType, out AnalysisResult result)
    {
        return TryGetAnalyzerResult(projectInfo, fileType.ToString(), out result);
    }

    public static bool TryGetAnalyzerResult(this ProjectInfo projectInfo, string id, out AnalysisResult result)
    {
        if (projectInfo == null)
        {
            throw new ArgumentNullException(nameof(projectInfo));
        }

        result = null;

        if (projectInfo.AnalysisResults != null)
        {
            result = projectInfo.AnalysisResults.FirstOrDefault(ar => AnalysisResult.ResultKeyComparer.Equals(id, ar.Id));
        }
        return result != null;
    }

    public static bool TryGetAnalysisSetting(this ProjectInfo projectInfo, string id, out Property result)
    {
        if (projectInfo == null)
        {
            throw new ArgumentNullException(nameof(projectInfo));
        }

        result = null;

        if (projectInfo.AnalysisSettings != null)
        {
            result = projectInfo.AnalysisSettings.FirstOrDefault(ar => Property.AreKeysEqual(id, ar.Id));
        }
        return result != null;
    }

    public static void AddAnalyzerResult(this ProjectInfo projectInfo, AnalysisResultFileType fileType, string location)
    {
        AddAnalyzerResult(projectInfo, fileType.ToString(), location);
    }

    public static void AddAnalyzerResult(this ProjectInfo projectInfo, string id, string location)
    {
        if (projectInfo == null)
        {
            throw new ArgumentNullException(nameof(projectInfo));
        }
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(nameof(id));
        }
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentNullException(nameof(location));
        }

        if (projectInfo.AnalysisResults == null)
        {
            projectInfo.AnalysisResults = new List<AnalysisResult>();
        }

        var result = new AnalysisResult() { Id = id, Location = location };
        projectInfo.AnalysisResults.Add(result);
    }

    public static DirectoryInfo ProjectFileDirectory(this ProjectInfo projectInfo)
    {
        if (projectInfo == null)
        {
            throw new ArgumentNullException(nameof(projectInfo));
        }

        return string.IsNullOrWhiteSpace(projectInfo.FullPath) ? null : new FileInfo(projectInfo.FullPath).Directory;
    }

    public static string GetProjectGuidAsString(this ProjectInfo projectInfo)
    {
        if (projectInfo == null)
        {
            throw new ArgumentNullException(nameof(projectInfo));
        }

        return projectInfo.ProjectGuid.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant();
    }

    public static string TryGetAnalysisFileLocation(this ProjectInfo projectInfo, AnalysisResultFileType fileType)
    {
        if (projectInfo.TryGetAnalyzerResult(fileType, out var result))
        {
            return result.Location;
        }

        return null;
    }

    public static FileInfo[] GetAllAnalysisFiles(this ProjectInfo projectInfo, ILogger logger)
    {
        var compiledFilesPath = projectInfo.TryGetAnalysisFileLocation(AnalysisResultFileType.FilesToAnalyze);
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
