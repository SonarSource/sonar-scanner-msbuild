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

using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Tasks.IntegrationTest;

internal static class BuildUtilities
{
    /// <summary>
    /// Creates and returns a new MSBuild project using the supplied template
    /// </summary>
    public static ProjectRootElement CreateProjectFromTemplate(string projectFilePath, TestContext testContext, string templateXml, params object[] args)
    {
        CreateFileFromTemplate(projectFilePath, testContext, templateXml, args);
        var projectRoot = ProjectRootElement.Open(projectFilePath);
        return projectRoot;
    }

    /// <summary>
    /// Creates and returns a new MSBuild project using the supplied template
    /// </summary>
    public static void CreateFileFromTemplate(string projectFilePath, TestContext testContext, string templateXml, params object[] args)
    {
        var projectXml = templateXml;
        if (args != null && args.Any())
        {
            projectXml = string.Format(System.Globalization.CultureInfo.CurrentCulture, templateXml, args);
        }

        File.WriteAllText(projectFilePath, projectXml);
        testContext.AddResultFile(projectFilePath);
    }
}
