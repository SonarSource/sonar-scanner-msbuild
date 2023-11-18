/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CodeCoverageExeTestMethodAttribute : TestMethodAttribute
    {
        private const string EnvironmentVariable = "VsTestToolsInstallerInstalledToolLocation";

        public override TestResult[] Execute(ITestMethod testMethod) =>
            FindCodeCoverageExe() is not null
                ? base.Execute(testMethod)
                : (new TestResult[]
                {
                    new()
                    {
                        LogError = @$"CodeCoverage.exe wasn't found. Set the {EnvironmentVariable} to the path to CodeCoverage.exe. The tool can be downloaded and zip extracted from the Microsoft.CodeCoverage NuGet package. It is in the build\netstandard2.0\CodeCoverage folder in the package.",
                        Outcome = UnitTestOutcome.Inconclusive,
                    }
                });

        public static string FindCodeCoverageExe() =>
            Environment.GetEnvironmentVariable(EnvironmentVariable) is { } path
            && File.Exists(path)
                ? path
                : null;
    }
}
