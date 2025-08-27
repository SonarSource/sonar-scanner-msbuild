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

namespace TestUtilities;

public static class ProjectInfoAssertions
{
    public static void AssertExpectedValues(ProjectInfo expected, ProjectInfo actual)
    {
        actual.Should().NotBeNull("Supplied ProjectInfo should not be null");

        actual.FullPath.Should().Be(expected.FullPath, "Unexpected FullPath");
        actual.ProjectLanguage.Should().Be(expected.ProjectLanguage, "Unexpected ProjectLanguage");
        actual.ProjectType.Should().Be(expected.ProjectType, "Unexpected ProjectType");
        actual.ProjectGuid.Should().Be(expected.ProjectGuid, "Unexpected ProjectGuid");
        actual.ProjectName.Should().Be(expected.ProjectName, "Unexpected ProjectName");
        actual.IsExcluded.Should().Be(expected.IsExcluded, "Unexpected IsExcluded");

        CompareAnalysisResults(expected, actual);
    }

    public static ProjectInfo AssertProjectInfoExists(string rootOutputFolder, string fullProjectFileName)
    {
        var items = Directory.EnumerateDirectories(rootOutputFolder, "*.*", SearchOption.TopDirectoryOnly)
            .Select(x => Path.Combine(x, FileConstants.ProjectInfoFileName))
            .Where(File.Exists)
            .Select(ProjectInfo.Load)
            .ToArray();
        items.Should().NotBeEmpty("Failed to locate any project info files under the specified root folder");
        var match = items.FirstOrDefault(x => fullProjectFileName.Equals(x.FullPath, StringComparison.OrdinalIgnoreCase));
        match.Should().NotBeNull("Failed to retrieve a project info file for the specified project: {0}", fullProjectFileName);
        return match;
    }

    private static void CompareAnalysisResults(ProjectInfo expected, ProjectInfo actual)
    {
        // We're assuming the actual analysis results have been reloaded by the serializer
        // so they should never be null
        actual.AnalysisResults.Should().NotBeNull("actual AnalysisResults should not be null");

        if (expected.AnalysisResults == null || !expected.AnalysisResults.Any())
        {
            actual.AnalysisResults.Should().BeEmpty("actual AnalysisResults should be empty");
        }
        else
        {
            foreach (var expectedResult in expected.AnalysisResults)
            {
                actual.AssertAnalysisResultExists(expectedResult.Id, expectedResult.Location);
            }

            actual.AnalysisResults.Should().HaveCount(expected.AnalysisResults.Count, "Unexpected additional analysis results found");
        }
    }
}
