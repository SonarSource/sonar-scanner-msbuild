/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

namespace TestUtilities;

public static class ProjectInfoExtensions
{
    public static void AssertAnalysisResultDoesNotExists(this ProjectInfo projectInfo, string resultId)
    {
        projectInfo.AnalysisResultFiles.Should().NotBeNull("AnalysisResults should not be null");
        projectInfo.FindAnalysisResultFile(resultId).Should().BeNull("Not expecting to find an analysis result for id. Id: {0}", resultId);
    }

    public static AnalysisResultFile AssertAnalysisResultFileExists(this ProjectInfo projectInfo, string resultId)
    {
        projectInfo.AnalysisResultFiles.Should().NotBeNull("AnalysisResults should not be null");
        var result = projectInfo.FindAnalysisResultFile(resultId);
        result.Should().NotBeNull("Returned analysis result should not be null. Id: {0}", resultId);
        return result;
    }
}
