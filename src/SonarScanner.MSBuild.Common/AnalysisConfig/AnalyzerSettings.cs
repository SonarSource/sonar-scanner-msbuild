/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

namespace SonarScanner.MSBuild.Common
{
    /// <summary>
    /// Data class containing the information required to configure
    /// the compiler for Roslyn analysis
    /// </summary>
    /// <remarks>This class is XML-serializable</remarks>
    public class AnalyzerSettings
    {
        public AnalyzerSettings()
        {
        }

        public AnalyzerSettings(string language, string ruleSetFilePath, IEnumerable<string> analyzerAssemblies, IEnumerable<string> additionalFiles)
        {
            if (string.IsNullOrWhiteSpace(ruleSetFilePath))
            {
                throw new ArgumentNullException(nameof(ruleSetFilePath));
            }
            if (analyzerAssemblies == null)
            {
                throw new ArgumentNullException(nameof(analyzerAssemblies));
            }
            if (additionalFiles == null)
            {
                throw new ArgumentNullException(nameof(additionalFiles));
            }

            Language = language;
            RuleSetFilePath = ruleSetFilePath;
            AnalyzerAssemblyPaths = new List<string>(analyzerAssemblies);
            AdditionalFilePaths = new List<string>(additionalFiles);
        }

        /// <summary>
        /// Language which this settings refers to
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Path to the ruleset for the Roslyn analyzers
        /// </summary>
        public string RuleSetFilePath { get; set; }

        /// <summary>
        /// File paths for all of the assemblies to pass to the compiler as analyzers
        /// </summary>
        /// <remarks>This includes analyzer assemblies and their dependencies</remarks>
        [XmlArray]
        [XmlArrayItem("Path")]
        public List<string> AnalyzerAssemblyPaths { get; set; }

        /// <summary>
        /// File paths for all files to pass as "AdditionalFiles" to the compiler
        /// </summary>
        [XmlArray]
        [XmlArrayItem("Path")]
        public List<string> AdditionalFilePaths { get; set; }
    }
}
