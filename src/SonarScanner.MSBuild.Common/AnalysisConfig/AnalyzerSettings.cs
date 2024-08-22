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
/// Data class containing the information required to configure
/// the compiler for Roslyn analysis.
/// </summary>
/// <remarks>This class is XML-serializable.</remarks>
public class AnalyzerSettings
{
    /// <summary>
    /// Language which this settings refers to.
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Path to the ruleset file for the Roslyn analyzers.
    /// </summary>
    public string RulesetPath { get; set; }

    /// <summary>
    /// Path to the ruleset file for the Roslyn analyzers with all rules deactivated.
    /// </summary>
    public string DeactivatedRulesetPath { get; set; }

    /// <summary>
    /// File paths for all of the assemblies to pass to the compiler as analyzers.
    /// </summary>
    /// <remarks>This includes analyzer assemblies and their dependencies.</remarks>
    [XmlArray]
    [XmlArrayItem("AnalyzerPlugin")]
    public List<AnalyzerPlugin> AnalyzerPlugins { get; set; }

    /// <summary>
    /// File paths for all files to pass as "AdditionalFiles" to the compiler.
    /// </summary>
    [XmlArray]
    [XmlArrayItem("Path")]
    public List<string> AdditionalFilePaths { get; set; }

    public AnalyzerSettings() { }

    public AnalyzerSettings(string language, string rulesetPath, string deactivatedRulesetPath, IEnumerable<AnalyzerPlugin> analyzerPlugins, IEnumerable<string> additionalFiles)
    {
        if (string.IsNullOrWhiteSpace(rulesetPath))
        {
            throw new ArgumentNullException(nameof(rulesetPath));
        }
        if (string.IsNullOrWhiteSpace(deactivatedRulesetPath))
        {
            throw new ArgumentNullException(nameof(deactivatedRulesetPath));
        }
        _ = analyzerPlugins ?? throw new ArgumentNullException(nameof(analyzerPlugins));
        _ = additionalFiles ?? throw new ArgumentNullException(nameof(additionalFiles));

        Language = language;
        RulesetPath = rulesetPath;
        DeactivatedRulesetPath = deactivatedRulesetPath;
        AnalyzerPlugins = new List<AnalyzerPlugin>(analyzerPlugins);
        AdditionalFilePaths = new List<string>(additionalFiles);
    }

}
