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

using NSubstitute.ExceptionExtensions;

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

    [TestMethod]
    [DataRow("build-wrapper-dump.json")]
    [DataRow("./compile_commands.json")]
    [DataRow(".\\compile_commands.json")]
    [DataRow("C:/dev/BUILD-WRAPPER-DUMP.json")]
    [DataRow("C:\\dev\\cOmpile_commAnDs.json")]
    [DataRow("C:\\dev/whatever/compile_commands.json")]
    [DataRow("C:\\dev/whatever\\build-wrapper-dump.json")]
    public void AdditionalFiles_ExcludedFilesIgnored(string excluded)
    {
        wrapper
            .EnumerateFiles(Arg.Any<DirectoryInfo>(), Arg.Any<string>(), Arg.Any<SearchOption>())
            .Returns(
            [
                new("valid.json"),
                new(excluded)
            ]);

        var config = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings = [new("sonar.json.file.suffixes", ".json")]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo("valid.json");
        files.Tests.Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow("sonar.tsql.file.suffixes")]
    [DataRow("sonar.plsql.file.suffixes")]
    [DataRow("sonar.yaml.file.suffixes")]
    [DataRow("sonar.json.file.suffixes")]
    [DataRow("sonar.css.file.suffixes")]
    [DataRow("sonar.html.file.suffixes")]
    [DataRow("sonar.javascript.file.suffixes")]
    [DataRow("sonar.typescript.file.suffixes")]
    [DataRow("sonar.python.file.suffixes")]
    [DataRow("sonar.ipynb.file.suffixes")]
    [DataRow("sonar.azureresourcemanager.file.suffixes")]
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
            "valid.py",
            "valid.ipynb",
            "valid.bicep",
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
                new("sonar.python.file.suffixes", ".py"),
                new("sonar.ipynb.file.suffixes", ".ipynb"),
                new("sonar.azureresourcemanager.file.suffixes", ".bicep"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo("valid.cs.html", "valid.sql", "valid.py", "valid.ipynb", "valid.bicep");
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

    [TestMethod]
    public void AdditionalFiles_DirectoryAccessFail()
    {
        wrapper.EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories).Throws(_ => new DirectoryNotFoundException("Error message"));
        var analysisConfig = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings = [new("sonar.typescript.file.suffixes", ".ts,.tsx")]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
        wrapper.Received(1).EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories);
        logger.DebugMessages[0].Should().Be($"Reading directories from: '{ProjectBaseDir}'.");
        logger.DebugMessages[1].Should().MatchEquivalentOf(@"HResult: -2147024893, Exception: System.IO.DirectoryNotFoundException: Error message
   at NSubstitute.ExceptionExtensions.ExceptionExtensions.<>c__DisplayClass2_0.<Throws>b__0(CallInfo ci) *");
        logger.DebugMessages[2].Should().Be($"Reading files from: '{ProjectBaseDir}'.");
        logger.DebugMessages[3].Should().Be($"Found 0 files in: '{ProjectBaseDir}'.");
        logger.AssertSingleWarningExists($"Failed to get directories from: '{ProjectBaseDir}'.");
    }

    [TestMethod]
    public void AdditionalFiles_FileAccessFail()
    {
        var firstDirectory = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, "first directory"));
        var secondDirectory = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, "second directory"));
        wrapper.EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories).Returns([firstDirectory, secondDirectory]);
        wrapper.EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly).Returns([new FileInfo("file in base dir.ts")]);
        wrapper.EnumerateFiles(firstDirectory, "*", SearchOption.TopDirectoryOnly).Throws(_ => new PathTooLongException("Error message"));
        wrapper.EnumerateFiles(secondDirectory, "*", SearchOption.TopDirectoryOnly).Returns([new FileInfo("file in second dir.ts")]);
        var analysisConfig = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings = [new("sonar.typescript.file.suffixes", ".ts,.tsx")]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo("file in base dir.ts", "file in second dir.ts");
        files.Tests.Should().BeEmpty();
        wrapper.Received(1).EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories);
        wrapper.Received(3).EnumerateFiles(Arg.Any<DirectoryInfo>(), "*", SearchOption.TopDirectoryOnly);

        logger.DebugMessages.Should().HaveCount(8);
        logger.DebugMessages[0].Should().Be(@"Reading directories from: 'C:\dev'.");
        logger.DebugMessages[1].Should().Be(@"Found 2 directories in: 'C:\dev'.");
        logger.DebugMessages[2].Should().Be(@"Reading files from: 'C:\dev\first directory'.");
        logger.DebugMessages[3].Should().MatchEquivalentOf(@"HResult: -2147024690, Exception: System.IO.PathTooLongException: Error message
   at NSubstitute.ExceptionExtensions.ExceptionExtensions.<>c__DisplayClass2_0.<Throws>b__0(CallInfo ci) *");
        logger.DebugMessages[4].Should().Be(@"Reading files from: 'C:\dev\second directory'.");
        logger.DebugMessages[5].Should().Be(@"Found 1 files in: 'C:\dev\second directory'.");
        logger.DebugMessages[6].Should().Be(@"Reading files from: 'C:\dev'.");
        logger.DebugMessages[7].Should().Be(@"Found 1 files in: 'C:\dev'.");

        logger.AssertSingleWarningExists(@"Failed to get files from: 'C:\dev\first directory'.");
    }
}
