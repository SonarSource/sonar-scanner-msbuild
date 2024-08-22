/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class AdditionalFilesServiceTest
{
    private static readonly DirectoryInfo ProjectBaseDir = new("C:\\dev");

    private readonly IDirectoryWrapper wrapper;
    private readonly TestLogger logger = new();
    private readonly AdditionalFilesService sut;

    public AdditionalFilesServiceTest()
    {
        wrapper = Substitute.For<IDirectoryWrapper>();
        wrapper
            .EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories)
            .Returns([]);
        sut = new(wrapper, logger);
    }

    [TestMethod]
    public void AdditionalFiles_NullSettings_NoExtensionsFound()
    {
        var files = sut.AdditionalFiles(new() { ScanAllAnalysis = true, LocalSettings = null, ServerSettings = null }, ProjectBaseDir);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
        wrapper.DidNotReceive().EnumerateFiles(Arg.Any<DirectoryInfo>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [TestMethod]
    public void AdditionalFiles_EmptySettings_NoExtensionsFound()
    {
        var files = sut.AdditionalFiles(new() { ScanAllAnalysis = true, ServerSettings = null }, ProjectBaseDir);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
        wrapper.DidNotReceive().EnumerateFiles(Arg.Any<DirectoryInfo>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [TestMethod]
    public void AdditionalFiles_ScanAllAnalysisDisabled()
    {
        wrapper
            .EnumerateFiles(Arg.Any<DirectoryInfo>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns([new("valid.js")]);
        var config = new AnalysisConfig
        {
            ScanAllAnalysis = false,
            LocalSettings = [],
            ServerSettings = [new("sonar.javascript.file.suffixes", ".js")]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow(".sonarqube")]
    [DataRow(".SONARQUBE")]
    [DataRow(".SonaRQubE")]
    [DataRow(".sonar")]
    [DataRow(".SONAR")]
    public void AdditionalFiles_ExtensionsFound_SonarQubeIgnored(string template)
    {
        var valid = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, "valid"));
        var invalid = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, template));
        var invalidNested = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, template, "conf"));
        wrapper
            .EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories)
            .Returns([valid, invalid, invalidNested]);
        wrapper
            .EnumerateFiles(valid, "*", SearchOption.TopDirectoryOnly)
            .Returns([
                // sources
                new("valid.js"),
                new($"{template}.js"),
                new($"not{template}{Path.DirectorySeparatorChar}not{template}.js"),
                new($"{template}not{Path.DirectorySeparatorChar}{template}not.js"),
                // tests
                new($"{template}.test.js"),
                new($"not{template}{Path.DirectorySeparatorChar}not{template}.spec.js"),
                ]);
        wrapper
            .EnumerateFiles(invalid, "*", SearchOption.TopDirectoryOnly)
            .Returns([
                new($"invalid.js"),
                new($"invalid.test.js"),
                new($"invalid.spec.js"),
                ]);
        wrapper
            .EnumerateFiles(invalidNested, "*", SearchOption.TopDirectoryOnly)
            .Returns([
                new($"alsoInvalid.js"),
                new($"alsoInvalid.test.js"),
                new($"alsoInvalid.spec.js"),
                ]);
        var analysisConfig = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings =
            [
                new("sonar.javascript.file.suffixes", ".js"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(
            "valid.js",
            $"{template}.js",
            $"not{template}.js",
            $"{template}not.js");
        files.Tests.Select(x => x.Name).Should().BeEquivalentTo(
            $"{template}.test.js",
            $"not{template}.spec.js");
    }

    [TestMethod]
    public void AdditionalFiles_LocalSettingsTakePrecedence()
    {
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns([new("valid.haskell"), new("invalid.js")]);
        var config = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [new("sonar.javascript.file.suffixes", ".haskell")],
            ServerSettings = [new("sonar.javascript.file.suffixes", ".js")]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(["valid.haskell"]);
        files.Tests.Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow(".js,.jsx")]
    [DataRow(".js, .jsx")]
    [DataRow(" .js, .jsx")]
    [DataRow(" .js,.jsx")]
    [DataRow(".js,.jsx ")]
    [DataRow(".js,.jsx ")]
    [DataRow("js,jsx")]
    [DataRow(" js , jsx ")]
    public void AdditionalFiles_ExtensionsFound_AllExtensionPermutations(string propertyValue)
    {
        var allFiles = new[]
        {
            "valid.js",
            "valid.JSX",
            "invalid.ajs",
            "invalidjs",
            @"C:\.js",
            @"C:\.jsx"
        };
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns(allFiles.Select(x => new FileInfo(x)));
        var config = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings = [new("sonar.javascript.file.suffixes", propertyValue)]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo("valid.js", "valid.JSX");
        files.Tests.Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow("sonar.tsql.file.suffixes")]
    [DataRow("sonar.plsql.file.suffixes")]
    [DataRow("sonar.yaml.file.suffixes")]
    [DataRow("sonar.xml.file.suffixes")]
    [DataRow("sonar.json.file.suffixes")]
    [DataRow("sonar.css.file.suffixes")]
    [DataRow("sonar.html.file.suffixes")]
    [DataRow("sonar.javascript.file.suffixes")]
    [DataRow("sonar.typescript.file.suffixes")]
    public void AdditionalFiles_ExtensionsFound_SingleProperty(string propertyName)
    {
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns([new("valid.sql"), new("valid.js"), new("invalid.cs")]);
        var config = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings = [new(propertyName, ".sql,.js")]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo("valid.sql", "valid.js");
        files.Tests.Should().BeEmpty();
    }

    [TestMethod]
    public void AdditionalFiles_ExtensionsFound_MultipleProperties()
    {
        var allFiles = new[]
        {
            "valid.cs.html",
            "valid.sql",
            "invalid.js",
            "invalid.html",
            "invalid.vb.html"
        };
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns(allFiles.Select(x => new FileInfo(x)));
        var analysisConfig = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings =
            [
                new("sonar.html.file.suffixes", ".cs.html"),
                new("sonar.tsql.file.suffixes", ".sql"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo("valid.cs.html", "valid.sql");
        files.Tests.Should().BeEmpty();
    }

    [TestMethod]
    public void AdditionalFiles_ExtensionsFound_MultipleProperties_NoAdditionalParameters()
    {
        var allFiles = new[]
        {
            // source files
            $"{Path.DirectorySeparatorChar}.js",      // should be ignored
            $"{Path.DirectorySeparatorChar}.jsx",     // should be ignored
            "file1.js",
            "file2.jsx",
            "file3.ts",
            "file4.tsx",
            // js test files
            "file5.spec.js",
            "file6.test.js",
            "file7.spec.jsx",
            "file8.test.jsx",
            // ts test files
            "file9.spec.ts",
            "file10.test.TS",
            "file11.spec.tsx",
            "file12.test.TSx",
            // random invalid file
            "invalid.html"
        };
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns(allFiles.Select(x => new FileInfo(x)));
        var analysisConfig = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings =
            [
                new("sonar.javascript.file.suffixes", "js,jsx"),
                new("sonar.typescript.file.suffixes", ".ts,.tsx"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo("file1.js", "file2.jsx", "file3.ts", "file4.tsx");
        files.Tests.Select(x => x.Name).Should().BeEquivalentTo(
            "file5.spec.js",
            "file6.test.js",
            "file7.spec.jsx",
            "file8.test.jsx",
            "file9.spec.ts",
            "file10.test.TS",
            "file11.spec.tsx",
            "file12.test.TSx");
        logger.AssertNoWarningsLogged();
    }

    [DataTestMethod]
    [DataRow("sonar.tests")]
    [DataRow("sonar.sources")]
    [DataRow("sonar.inclusions")]
    [DataRow("sonar.test.inclusions")]
    public void AdditionalFiles_ExtensionsFound_MultipleProperties_WithAdditionalParameters(string param)
    {
        var allFiles = new[]
        {
            // source files
            "file1.js",
            "file2.jsx",
            "file3.ts",
            "file4.tsx",
            // js test files
            "file5.spec.js",
            "file6.test.js",
            "file7.spec.jsx",
            "file8.test.jsx",
            // ts test files
            "file9.spec.ts",
            "file10.test.ts",
            "file11.spec.tsx",
            "file12.test.tsx",
            // random invalid file
            "invalid.html"
        };
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns(allFiles.Select(x => new FileInfo(x)));
        var analysisConfig = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [new(param, "whatever")],
            ServerSettings =
            [
                new("sonar.javascript.file.suffixes", ".js,.jsx"),
                new("sonar.typescript.file.suffixes", ".ts,.tsx"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
        logger.AssertWarningLogged($"""The support for multi-language analysis may not function correctly if {param} is set. If this is the case, please explicitly set "sonar.scanner.scanAll=false" to disable the multi-language analysis.""");
    }
}
