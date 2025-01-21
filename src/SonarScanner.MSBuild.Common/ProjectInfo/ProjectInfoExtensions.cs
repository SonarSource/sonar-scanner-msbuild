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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Extension methods for <see cref="ProjectInfo"/>
/// </summary>
public static class ProjectInfoExtensions
{
    /// <summary>
    /// Attempts to find and return the analyzer result with the specified id
    /// </summary>
    /// <returns>True if the analyzer result was found, otherwise false</returns>
    public static bool TryGetAnalyzerResult(this ProjectInfo projectInfo, AnalysisType analyzerType, out AnalysisResult result)
    {
        return TryGetAnalyzerResult(projectInfo, analyzerType.ToString(), out result);
    }

    /// <summary>
    /// Attempts to find and return the analyzer result with the specified id
    /// </summary>
    /// <returns>True if the analyzer result was found, otherwise false</returns>
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

    /// <summary>
    /// Attempts to find and return the analysis setting with the specified id
    /// </summary>
    /// <returns>True if the setting was found, otherwise false</returns>
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

    /// <summary>
    /// Adds an analysis result of the specified type
    /// </summary>
    /// <remarks>The method does not check whether an analysis result with the same id already exists i.e. duplicate results are allowed</remarks>
    public static void AddAnalyzerResult(this ProjectInfo projectInfo, AnalysisType analyzerType, string location)
    {
        AddAnalyzerResult(projectInfo, analyzerType.ToString(), location);
    }

    /// <summary>
    /// Adds an analysis result of the specified kind
    /// </summary>
    /// <remarks>The method does not check whether an analysis result with the same id already exists i.e. duplicate results are allowed</remarks>
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

    /// <summary>
    /// Returns the full path of the directory containing the project file
    /// </summary>
    public static DirectoryInfo GetDirectory(this ProjectInfo projectInfo)
    {
        if (projectInfo == null)
        {
            throw new ArgumentNullException(nameof(projectInfo));
        }

        return !string.IsNullOrWhiteSpace(projectInfo.FullPath)
            ? new FileInfo(projectInfo.FullPath).Directory
            : null;
    }

    /// <summary>
    /// Returns the ProjectGuid formatted as a string
    /// </summary>
    public static string GetProjectGuidAsString(this ProjectInfo projectInfo)
    {
        if (projectInfo == null)
        {
            throw new ArgumentNullException(nameof(projectInfo));
        }

        return projectInfo.ProjectGuid.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant();
    }

    /// <summary>
    /// Attempts to return the file location for the specified type of analysis result.
    /// Returns null if there is not a result for the specified type.
    /// Note that callers must check if the file exists before attempting to read.
    /// </summary>
    public static string TryGetAnalysisFileLocation(this ProjectInfo projectInfo, AnalysisType analysisType)
    {
        if (projectInfo.TryGetAnalyzerResult(analysisType, out var result))
        {
            return result.Location;
        }

        return null;
    }

    /// <summary>
    /// Returns the list of files to be analyzed. If there are no files to be analyzed
    /// then an empty list will be returned.
    /// </summary>
    public static FileInfo[] GetAllAnalysisFiles(this ProjectInfo projectInfo, ILogger logger)
    {
        var compiledFilesPath = projectInfo.TryGetAnalysisFileLocation(AnalysisType.FilesToAnalyze);
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
