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

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class AdditionalFilesServiceTest
{
    private readonly IDirectoryWrapper wrapper;
    private readonly DirectoryInfo directoryInfo;
    private readonly AdditionalFilesService sut;

    public AdditionalFilesServiceTest()
    {
        wrapper = Substitute.For<IDirectoryWrapper>();
        sut = new(wrapper);
        directoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
    }

    [TestMethod]
    public void AdditionalFiles_EmptyServerSettings_NoExtensionsFound()
    {
        var files = sut.AdditionalFiles(new() {MultiFileAnalysis = true, ServerSettings = [] }, directoryInfo);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
        wrapper.DidNotReceive().EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [TestMethod]
    public void AdditionalFiles_NullServerSettings_NoExtensionsFound()
    {
        var files = sut.AdditionalFiles(new() { MultiFileAnalysis = true, ServerSettings = null }, directoryInfo);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
        wrapper.DidNotReceive().EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    [TestMethod]
    public void AdditionalFiles_MultiFileAnalysisDisabled()
    {
        wrapper.EnumerateFiles(directoryInfo.FullName, "*", SearchOption.AllDirectories).Returns(["valid.js"]);
        var config = new AnalysisConfig
        {
            MultiFileAnalysis = false,
            LocalSettings = [],
            ServerSettings = [new("sonar.javascript.file.suffixes", ".js")]
        };

        var files = sut.AdditionalFiles(config, directoryInfo);

        files.Sources.Should().BeEmpty();
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
        wrapper.EnumerateFiles(directoryInfo.FullName, "*", SearchOption.AllDirectories).Returns(["valid.js", "valid.JSX", "invalid.ajs", "invalidjs", @"C:\.js", @"C:\.jsx"]);
        var config = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [],
            ServerSettings = [new("sonar.javascript.file.suffixes", propertyValue)]
        };

        var files = sut.AdditionalFiles(config, directoryInfo);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(["valid.js", "valid.JSX"]);
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
        wrapper.EnumerateFiles(directoryInfo.FullName, "*", SearchOption.AllDirectories).Returns(["valid.sql", "valid.js", "invalid.cs"]);
        var config = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [],
            ServerSettings = [new(propertyName, ".sql,.js")]
        };

        var files = sut.AdditionalFiles(config, directoryInfo);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(["valid.sql", "valid.js"]);
        files.Tests.Should().BeEmpty();
    }

    [TestMethod]
    public void AdditionalFiles_ExtensionsFound_MultipleProperties()
    {
        wrapper.EnumerateFiles(directoryInfo.FullName, "*", SearchOption.AllDirectories).Returns(["valid.cs.html", "valid.sql", "invalid.js", "invalid.html", "invalid.vb.html"]);
        var analysisConfig = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [],
            ServerSettings =
            [
                new("sonar.html.file.suffixes", ".cs.html"),
                new("sonar.tsql.file.suffixes", ".sql"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, directoryInfo);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(["valid.cs.html", "valid.sql"]);
        files.Tests.Should().BeEmpty();
    }

    [TestMethod]
    public void AdditionalFiles_ExtensionsFound_MultipleProperties_TestFilesExist_NoSonarTests()
    {
        wrapper
            .EnumerateFiles(directoryInfo.FullName, "*", SearchOption.AllDirectories)
            .Returns([
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
                ]);

        var analysisConfig = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [],
            ServerSettings =
            [
                new("sonar.javascript.file.suffixes", "js,jsx"),
                new("sonar.typescript.file.suffixes", ".ts,.tsx"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, directoryInfo);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo(["file1.js", "file2.jsx", "file3.ts", "file4.tsx"]);
        files.Tests.Select(x => x.Name).Should().BeEquivalentTo([
            "file5.spec.js",
            "file6.test.js",
            "file7.spec.jsx",
            "file8.test.jsx",
            "file9.spec.ts",
            "file10.test.TS",
            "file11.spec.tsx",
            "file12.test.TSx"
        ]);
    }

    [TestMethod]
    public void AdditionalFiles_ExtensionsFound_MultipleProperties_TestFilesExist_WithSonarTests()
    {
        wrapper
            .EnumerateFiles(directoryInfo.FullName, "*", SearchOption.AllDirectories)
            .Returns([
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
                ]);

        var analysisConfig = new AnalysisConfig
        {
            MultiFileAnalysis = true,
            LocalSettings = [new("sonar.tests", "whatever")],
            ServerSettings =
            [
                new("sonar.javascript.file.suffixes", ".js,.jsx"),
                new("sonar.typescript.file.suffixes", ".ts,.tsx"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, directoryInfo);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo([
            "file1.js",
            "file2.jsx",
            "file3.ts",
            "file4.tsx",
            "file5.spec.js",
            "file6.test.js",
            "file7.spec.jsx",
            "file8.test.jsx",
            "file9.spec.ts",
            "file10.test.ts",
            "file11.spec.tsx",
            "file12.test.tsx"
        ]);
        files.Tests.Should().BeEmpty();
    }
}
