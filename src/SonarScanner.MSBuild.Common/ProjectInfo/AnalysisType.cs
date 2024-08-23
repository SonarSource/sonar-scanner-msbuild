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

namespace SonarScanner.MSBuild.Common;

/* If we move to a plug-in model (i.e. so handlers for new types of analyzers
   can be plugged in at runtime e.g. using MEF) then this enum would be removed.
   For the time being we are only supported a known set of analyzers.
*/

/// <summary>
/// Lists the known types of analyzers that are handled by the properties generator
/// </summary>
public enum AnalysisType
{
    /// <summary>
    /// List of files that should be analyzed
    /// </summary>
    /// <remarks>The files could be of any type and any language</remarks>
    FilesToAnalyze,

    /// <summary>
    /// An XML code coverage report produced by the Visual Studio code coverage tool
    /// </summary>
    VisualStudioCodeCoverage
}
