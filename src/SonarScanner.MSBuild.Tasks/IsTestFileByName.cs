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
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Tasks;

/// <summary>
/// MSBuild task that determines whether a file should be treated as a
/// test file for analysis purposes based on its path and file name
/// </summary>
/// <remarks>The task applies a regular expression to the file name being tested to determine whether
/// the file is test file or not. The regular expression used is read from the analysis config file.</remarks>
public sealed class IsTestFileByName : Task
{
    /// <summary>
    /// Id of the SonarQube test setting that specifies the RegEx to use when determining
    /// if a project is a test project or not
    /// </summary>
    public const string TestRegExSettingId = "sonar.msbuild.testProjectPattern";

    #region Input properties

    /// <summary>
    /// The directory containing the analysis config settings file
    /// </summary>
    [Required]
    public string AnalysisConfigDir { get; set; }

    /// <summary>
    /// The full path and file name of the file being checked
    /// </summary>
    [Required]
    public string FullFilePath { get; set; }

    /// <summary>
    /// Return value - true or false
    /// </summary>
    [Output]
    public bool IsTest { get; private set; }

    #endregion Input properties

    #region Overrides

    public override bool Execute()
    {
        var logger = new MSBuildLoggerAdapter(Log);

        if (TaskUtilities.TryGetConfig(AnalysisConfigDir, new MSBuildLoggerAdapter(Log)) is AnalysisConfig config)
        {
            if (config.GetAnalysisSettings(true, logger).TryGetValue(TestRegExSettingId, out var regEx) && !string.IsNullOrWhiteSpace(regEx))
            {
                Log.LogMessage(MessageImportance.Low, Resources.IsTest_UsingRegExFromConfig, regEx);
                try
                {
                    // Let's use a case sensitive regex (default behavior)
                    IsTest = Regex.IsMatch(FullFilePath, regEx, RegexOptions.None, RegexConstants.DefaultTimeout);
                }
                catch (ArgumentException ex) // thrown for invalid regular expressions
                {
                    Log.LogError(Resources.IsTest_InvalidRegularExpression, regEx, ex.Message, TestRegExSettingId);
                }
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, Resources.IsTest_NameNotChecked);
            }
        }

        return !Log.HasLoggedErrors;
    }

    #endregion Overrides
}
