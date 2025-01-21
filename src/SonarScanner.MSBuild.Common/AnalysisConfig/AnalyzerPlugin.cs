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
using System.Xml.Serialization;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// XML-serializable data class for a single SonarQube plugin containing an analyzer
/// </summary>
public class AnalyzerPlugin
{
    public AnalyzerPlugin()
    {
    }

    public AnalyzerPlugin(string key, string version, string staticResourceName, IEnumerable<string> assemblies)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        StaticResourceName = staticResourceName ?? throw new ArgumentNullException(nameof(staticResourceName));

        if (assemblies == null)
        {
            throw new ArgumentNullException(nameof(assemblies));
        }
        AssemblyPaths = new List<string>(assemblies);
    }

    [XmlAttribute("Key")]
    public string Key { get; set; }

    [XmlAttribute("Version")]
    public string Version { get; set; }

    /// <summary>
    /// Name of the static resource in the plugin that contains the analyzer artifacts
    /// </summary>
    [XmlAttribute("StaticResourceName")]
    public string StaticResourceName { get; set; }

    /// <summary>
    /// File paths for all of the assemblies to pass to the compiler as analyzers
    /// </summary>
    /// <remarks>This includes analyzer assemblies and their dependencies</remarks>
    [XmlArray]
    [XmlArrayItem("Path")]
    public List<string> AssemblyPaths { get; set; }
}
