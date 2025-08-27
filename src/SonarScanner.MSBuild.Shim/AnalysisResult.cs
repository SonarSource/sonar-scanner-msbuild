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

namespace SonarScanner.MSBuild.Shim;

public class AnalysisResult
{
    public ProjectData[] Projects { get; }
    public ScannerEngineInput ScannerEngineInput { get; }
    public string FullPropertiesFilePath { get; }   // ToDo: Remove in SCAN4NET-721
    public bool RanToCompletion { get; set; }       // ToDo: Remove this tangle in SCAN4NET-721, it can only be false when sonar-project.properties file already exists

    public AnalysisResult(ProjectData[] projects, ScannerEngineInput scannerEngineInput = null, string fullPropertiesFilePath = null)
    {
        Projects = projects;
        ScannerEngineInput = scannerEngineInput;            // Can be null when there are no valid projects
        FullPropertiesFilePath = fullPropertiesFilePath;    // Can be null when there are no valid projects
    }

    public ICollection<ProjectInfo> ProjectsByStatus(ProjectInfoValidity status) =>
        Projects.Where(x => x.Status == status).Select(x => x.Project).ToList();
}
