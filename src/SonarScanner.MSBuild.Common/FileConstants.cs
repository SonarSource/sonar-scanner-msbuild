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

public static class FileConstants
{
    /// <summary>
    /// Name of the per-project file that contain configuration used by our analyzers.
    /// </summary>
    public const string ProjectConfigFileName = "SonarProjectConfig.xml";

    /// <summary>
    /// Name of the per-project file that contain information used
    /// during analysis and when generating the sonar-scanner.properties file
    /// </summary>
    public const string ProjectInfoFileName = "ProjectInfo.xml";

    /// <summary>
    /// Name of the file containing analysis configuration settings
    /// </summary>
    public const string ConfigFileName = "SonarQubeAnalysisConfig.xml";

    /// <summary>
    /// Name of the import before target file
    /// </summary>
    public const string ImportBeforeTargetsName = "SonarQube.Integration.ImportBefore.targets";

    /// <summary>
    /// Name of the targets file that contains the integration pieces
    /// </summary>
    public const string IntegrationTargetsName = "SonarQube.Integration.targets";

    /// <summary>
    /// Name of the file containing the UI Warnings to be shown in SQ/SC.
    /// </summary>
    public const string UIWarningsFileName = "AnalysisWarnings.S4NET.json";
}
