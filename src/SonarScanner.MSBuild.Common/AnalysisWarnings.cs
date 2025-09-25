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
using Newtonsoft.Json;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Simple logger implementation that logs output to the console.
/// </summary>
public class AnalysisWarnings
{
    /// <summary>
    /// List of analysis warnings that should be logged.
    /// </summary>
    protected readonly IList<string> messages = [];

    private readonly IFileWrapper fileWrapper;
    private readonly ILogger logger;

    public AnalysisWarnings(IFileWrapper fileWrapper, ILogger logger)
    {
        this.fileWrapper = fileWrapper;
        this.logger = logger;
    }

    /// <summary>
    /// Log a warning and display it in the UI (starting from SQ 9.9 LTS).
    /// </summary>
    public void Log(string message, params object[] args)
    {
        messages.Add(string.Format(CultureInfo.CurrentCulture, message, args));
        logger.LogWarning(message, args);
    }

    /// <summary>
    /// Creates the .json file in the specified output folder containing all warning messages.
    /// See: https://github.com/SonarSource/sonar-dotnet-enterprise/blob/master/sonar-dotnet-shared-library/src/main/java/org/sonarsource/dotnet/shared/plugins/AnalysisWarningsSensor.java#L54.
    /// </summary>
    public virtual void Write(string outputFolder)
    {
        if (messages.Any())
        {
            var warningsJson = JsonConvert.SerializeObject(messages.Select(x => new { text = x }).ToArray(), Formatting.Indented);
            fileWrapper.WriteAllText(Path.Combine(outputFolder, FileConstants.AnalysisWarningsFileName), warningsJson);
        }
    }
}
