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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test.AnalysisConfigProcessing;

[TestClass]
public class AnalysisConfigGeneratorTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void GenerateFile_TrustStoreProperties_Mapped()
    {
        var processArgs = CreateProcessedArgs(
        [
            new Property("sonar.scanner.truststorePath", @"C:\path\to\truststore.pfx"),
            new Property("sonar.scanner.truststorePassword", "changeit")
        ]);

        var config = GenerateAnalysisConfig(processArgs);

        config.Should().NotBeNull();
        config.LocalSettings.Should().HaveCount(2)
            .And.Contain(x => x.Id == "javax.net.ssl.trustStore" && x.Value == "C:/path/to/truststore.pfx")
            .And.Contain(x => x.Id == "javax.net.ssl.trustStorePassword" && x.Value == "changeit");
    }

    [DataTestMethod]
    [DataRow(SonarProperties.Verbose, "true")]
    [DataRow(SonarProperties.Organization, "org")]
    [DataRow(SonarProperties.HostUrl, "http://localhost:9000")]
    public void GenerateFile_UnmappedProperties(string id, string value)
    {
        var processArgs = CreateProcessedArgs([new Property(id, value)]);

        var config = GenerateAnalysisConfig(processArgs);

        config.Should().NotBeNull();
        config.LocalSettings.Should().ContainSingle(x => x.Id == id && x.Value == value);
    }

    private AnalysisConfig GenerateAnalysisConfig(ProcessedArgs processedArgs) =>
        AnalysisConfigGenerator.GenerateFile(
            processedArgs,
            BuildSettings.CreateNonTeamBuildSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext)),
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            new List<AnalyzerSettings>(),
            null,
            null);

    private static ProcessedArgs CreateProcessedArgs(IEnumerable<Property> properties = null)
    {
        var propertiesMock = Substitute.For<IAnalysisPropertyProvider>();
        propertiesMock.GetAllProperties().Returns(properties ?? new List<Property>());
        var processedArgs = new ProcessedArgs(
            "key",
            "name",
            null,
            null,
            false,
            propertiesMock,
            Substitute.For<IAnalysisPropertyProvider>(),
            Substitute.For<IAnalysisPropertyProvider>(),
            Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            Substitute.For<IOperatingSystemProvider>(),
            Substitute.For<ILogger>());
        return processedArgs;
    }
}
