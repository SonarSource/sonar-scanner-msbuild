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

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class SonarPropertiesTests
{
    /// <summary>
    /// Strings that are used to indicate arguments that contain non sensitive data.
    /// </summary>
    private static readonly IEnumerable<string> NonSensitivePropertyKeys =
    [
        SonarProperties.ClientCertPath,
        SonarProperties.JavaExePath,
        SonarProperties.SkipJreProvisioning,
        SonarProperties.HostUrl,
        SonarProperties.SonarcloudUrl,
        SonarProperties.ApiBaseUrl,
        SonarProperties.ConnectTimeout,
        SonarProperties.SocketTimeout,
        SonarProperties.ResponseTimeout,
        SonarProperties.UserHome,
        SonarProperties.LogLevel,
        SonarProperties.Organization,
        SonarProperties.OperatingSystem,
        SonarProperties.Architecture,
        SonarProperties.PluginCacheDirectory,
        SonarProperties.ProjectBaseDir,
        SonarProperties.ProjectBranch,
        SonarProperties.ProjectKey,
        SonarProperties.ProjectName,
        SonarProperties.ProjectVersion,
        SonarProperties.PullRequestBase,
        SonarProperties.PullRequestCacheBasePath,
        SonarProperties.SourceEncoding,
        SonarProperties.Verbose,
        SonarProperties.VsCoverageXmlReportsPaths,
        SonarProperties.VsTestReportsPaths,
        SonarProperties.WorkingDirectory,
        SonarProperties.CacheBaseUrl,
        SonarProperties.HttpTimeout,
        SonarProperties.ScanAllAnalysis,
        SonarProperties.Sources,
        SonarProperties.Tests
    ];

    /// <summary>
    /// The purpose of this test is to consider if an argument is sensitive when adding new ones.
    /// </summary>
    [TestMethod]
    public void PropertySensitivityShouldBeDeclared()
    {
        var type = typeof(SonarProperties);
        var fields = type.GetFields()
            .Where(x => !x.Name.Equals(nameof(SonarProperties.SensitivePropertyKeys)))
            .SelectMany(x =>
            {
                var value = x.GetValue(type);
                return value as IEnumerable<object> ?? [value];
            });

        SonarProperties.SensitivePropertyKeys.Concat(NonSensitivePropertyKeys).Should().BeEquivalentTo(fields);
    }
}
