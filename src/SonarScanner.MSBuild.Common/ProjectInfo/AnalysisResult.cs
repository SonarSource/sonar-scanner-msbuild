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

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Data class to describe the output of a single type of analyzer.
/// </summary>
/// <remarks>The class is XML-serializable. Examples of types of analyzer: CodeCoverage, Roslyn Analyzers...</remarks>
public class AnalysisResult
{
    public static readonly IEqualityComparer<string> ResultKeyComparer = StringComparer.Ordinal;

    [XmlAttribute]
    public string Id { get; set; }

    [XmlAttribute]
    public string Location { get; set; }
}
