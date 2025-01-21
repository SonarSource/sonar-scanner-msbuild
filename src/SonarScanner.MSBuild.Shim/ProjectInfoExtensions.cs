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
using System.Text;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Shim;

public static class ProjectInfoExtensions
{
    public static ProjectInfoValidity Classify(this ProjectInfo projectInfo, ILogger logger)
    {
        if (projectInfo.IsExcluded)
        {
            logger.LogInfo(Resources.MSG_ProjectIsExcluded, projectInfo.FullPath);
            return ProjectInfoValidity.ExcludeFlagSet;
        }

        if (HasInvalidGuid(projectInfo))
        {
            logger.LogWarning(Resources.WARN_InvalidProjectGuid, projectInfo.ProjectGuid, projectInfo.FullPath);
            return ProjectInfoValidity.InvalidGuid;
        }

        return ProjectInfoValidity.Valid;
    }

    public static void FixEncoding(this ProjectInfo projectInfo, string globalSourceEncoding, Action logIfGlobalEncodingIsIgnored)
    {
        if (projectInfo.Encoding is null)
        {
            if (globalSourceEncoding is null)
            {
                if (ProjectLanguages.IsCSharpProject(projectInfo.ProjectLanguage) || ProjectLanguages.IsVbProject(projectInfo.ProjectLanguage))
                {
                    projectInfo.Encoding = Encoding.UTF8.WebName;
                }
            }
            else
            {
                projectInfo.Encoding = globalSourceEncoding;
            }
        }
        else if (globalSourceEncoding is not null)
        {
            logIfGlobalEncodingIsIgnored();
        }
    }

    private static bool HasInvalidGuid(ProjectInfo project)
    {
        return project.ProjectGuid == Guid.Empty;
    }
}
