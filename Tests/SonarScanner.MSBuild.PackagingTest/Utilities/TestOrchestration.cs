/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

namespace SonarScanner.MSBuild.PackagingTest.Utilities;

public static class TestOrchestration
{
    public static bool IsReleaseBranch => bool.TryParse(Environment.GetEnvironmentVariable("IS_RELEASE_BRANCH"), out var value) && value;
    public static string FullVersion => typeof(TestOrchestration).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;

    private static bool IsCIContext =>
        Environment.GetEnvironmentVariable("BUILD_REASON") is not null      // Azure DevOps
        || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null; // GitHub Actions

    public static void InitializeTestClass()
    {
        if (!IsCIContext)
        {
            Assert.Inconclusive("This test must run on the CI environment, after the signing.");
        }
    }
}
