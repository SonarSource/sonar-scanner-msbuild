/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using SonarQube.Common;

namespace SonarScanner.Shim
{
    public class ProjectData
    {
        public ProjectData(ProjectInfo project)
        {
            Project = project;
        }

        public string Guid => Project.GetProjectGuidAsString();
        public string VisualStudioCoverageLocation => Project.TryGetAnalysisFileLocation(AnalysisType.VisualStudioCodeCoverage);

        public bool CoverageAnalysisExists(ILogger logger)
        {
            var visualStudioCoverageLocation = VisualStudioCoverageLocation;

            if (visualStudioCoverageLocation != null && !File.Exists(visualStudioCoverageLocation))
            {
                logger.LogWarning(Resources.WARN_CodeCoverageReportNotFound, visualStudioCoverageLocation);
                return false;
            }

            return true;
        }

        public ProjectInfoValidity Status { get; set; }
        public ProjectInfo Project { get; }
        // Files that are used by the project and are located in its folder
        public ICollection<string> ProjectFiles { get; } = new HashSet<string>();
        // Files, that are used by the project, but located in a different folder
        public ICollection<string> ExternalFiles { get; } = new HashSet<string>();
        // Roslyn analysis output files (json)
        public ICollection<string> RoslynReportFilePaths { get; } = new HashSet<string>();
        // The folders where the protobuf files are generated
        public ICollection<string> AnalyzerOutPaths { get; } = new HashSet<string>();

        public bool HasFiles => ProjectFiles.Any() || ExternalFiles.Any();
    }
}
