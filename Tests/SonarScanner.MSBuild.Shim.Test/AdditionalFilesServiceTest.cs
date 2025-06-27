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
        wrapper.ReceivedWithAnyArgs(1).EnumerateDirectories(null, null, default);
        wrapper.ReceivedWithAnyArgs(1).EnumerateFiles(null, null, default);
    }

    [TestMethod]
    public void AdditionalFiles_EmptySettings_NoExtensionsFound()
    {
        var files = sut.AdditionalFiles(new() { ScanAllAnalysis = true, ServerSettings = null }, ProjectBaseDir);

        files.Sources.Should().BeEmpty();
        files.Tests.Should().BeEmpty();
        wrapper.ReceivedWithAnyArgs(1).EnumerateDirectories(null, null, default);
        wrapper.ReceivedWithAnyArgs(1).EnumerateFiles(null, null, default);
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
                new("invalid.js"),
                new("invalid.test.js"),
                new("invalid.spec.js"),
            ]);
        wrapper
            .EnumerateFiles(invalidNested, "*", SearchOption.TopDirectoryOnly)
            .Returns([
                new("alsoInvalid.js"),
                new("alsoInvalid.test.js"),
                new("alsoInvalid.spec.js"),
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
            .Returns([new("valid.haskell"), new(Path.Combine(ProjectBaseDir.FullName, "invalid.js"))]);
        var config = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [new("sonar.javascript.file.suffixes", ".haskell")],
            ServerSettings = [new("sonar.javascript.file.suffixes", ".js")]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo("valid.haskell");
        files.Tests.Should().BeEmpty();
    }

    [TestCategory(TestCategories.NoUnixNeedsReview)]
    [DataTestMethod]
    [DataRow(".js,.jsx")]
    [DataRow(".js, .jsx")]
    [DataRow(" .js, .jsx")]
    [DataRow(" .js,.jsx")]
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

    [TestCategory(TestCategories.NoUnixNeedsReview)]
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
    [DataRow("sonar.php.file.suffixes")]
    [DataRow("sonar.azureresourcemanager.file.suffixes")]
    [DataRow("sonar.terraform.file.suffixes")]
    [DataRow("sonar.go.file.suffixes")]
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
            "valid.php",
            "valid.bicep",
            "valid.tf",
            "valid.go",
            "invalid.js",
            "invalid.html",
            "invalid.vb.html",
        };
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns(allFiles.Select(x => new FileInfo(Path.Combine(ProjectBaseDir.FullName, x))));
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
                new("sonar.php.file.suffixes", ".php"),
                new("sonar.azureresourcemanager.file.suffixes", ".bicep"),
                new("sonar.terraform.file.suffixes", ".tf"),
                new("sonar.go.file.suffixes", ".go"),
            ]
        };

        var files = sut.AdditionalFiles(analysisConfig, ProjectBaseDir);

        files.Sources.Select(x => x.Name).Should().BeEquivalentTo("valid.cs.html", "valid.sql", "valid.py", "valid.ipynb", "valid.php", "valid.bicep", "valid.tf", "valid.go");
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
            .Returns(allFiles.Select(x => new FileInfo(Path.Combine(ProjectBaseDir.FullName, x))));
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

    [TestCategory(TestCategories.NoUnixNeedsReview)]
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

    [TestCategory(TestCategories.NoUnixNeedsReview)]
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

    [TestMethod]
    public void AdditionalFiles_WildcardPattern()
    {
        var nestedFolder = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, "nested"));
        wrapper
            .EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories)
            .Returns([nestedFolder]);
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(ProjectBaseDir.FullName, "Dfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyApp.dfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyOtherApp.Dfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "Dfile.production")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyDfile.production"))
            ]);
        wrapper
            .EnumerateFiles(nestedFolder, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(nestedFolder.FullName, "Dfile")),
                new(Path.Combine(nestedFolder.FullName, "MyApp.dfile")),
                new(Path.Combine(nestedFolder.FullName, "MyOtherApp.Dfile")),
                new(Path.Combine(nestedFolder.FullName, "Dfile.production")),
                new(Path.Combine(nestedFolder.FullName, "MyDfile.production"))
            ]);
        var config = new AnalysisConfig { ScanAllAnalysis = true, LocalSettings = [], ServerSettings = [new("sonar.docker.file.patterns", "**/Dfile,**/*.dfile,**/*.Dfile")] };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.FullName).Should().BeEquivalentTo(
            Path.Combine(ProjectBaseDir.FullName, "Dfile"),
            Path.Combine(ProjectBaseDir.FullName, "MyApp.dfile"),
            Path.Combine(ProjectBaseDir.FullName, "MyOtherApp.Dfile"),
            Path.Combine(nestedFolder.FullName, "Dfile"),
            Path.Combine(nestedFolder.FullName, "MyApp.dfile"),
            Path.Combine(nestedFolder.FullName, "MyOtherApp.Dfile"));
        files.Tests.Should().BeEmpty();
    }

    [TestMethod]
    public void AdditionalFiles_WildcardPattern_BaseDir()
    {
        var nestedFolder = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, "nested"));
        wrapper
            .EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories)
            .Returns([nestedFolder]);
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(ProjectBaseDir.FullName, "Dfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyApp.dfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyOtherApp.Dfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "Dfile.production")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyDfile.production"))
            ]);
        wrapper
            .EnumerateFiles(nestedFolder, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(nestedFolder.FullName, "Dfile")),
                new(Path.Combine(nestedFolder.FullName, "MyApp.dfile")),
                new(Path.Combine(nestedFolder.FullName, "MyOtherApp.Dfile")),
                new(Path.Combine(nestedFolder.FullName, "Dfile.production")),
                new(Path.Combine(nestedFolder.FullName, "MyDfile.production"))
            ]);
        var config = new AnalysisConfig { ScanAllAnalysis = true, LocalSettings = [], ServerSettings = [new("sonar.docker.file.patterns", "Dfile,*.dfile,*.Dfile")] };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.FullName).Should().BeEquivalentTo(
            Path.Combine(ProjectBaseDir.FullName, "Dfile"),
            Path.Combine(ProjectBaseDir.FullName, "MyApp.dfile"),
            Path.Combine(ProjectBaseDir.FullName, "MyOtherApp.Dfile"));
        files.Tests.Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow("sonar.docker.file.patterns")]
    [DataRow("sonar.java.jvmframeworkconfig.file.patterns")]
    [DataRow("sonar.text.inclusions")]
    public void AdditionalFiles_WildcardPattern_RelativePattern(string property)
    {
        var nestedFolder = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, "nested"));
        wrapper
            .EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories)
            .Returns([nestedFolder]);
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(ProjectBaseDir.FullName, "Dfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyApp.dfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyOtherApp.Dfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "Dfile.production")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyDfile.production"))
            ]);
        wrapper
            .EnumerateFiles(nestedFolder, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(nestedFolder.FullName, "Dfile")),
                new(Path.Combine(nestedFolder.FullName, "MyApp.dfile")),
                new(Path.Combine(nestedFolder.FullName, "MyOtherApp.Dfile")),
                new(Path.Combine(nestedFolder.FullName, "Dfile.production")),
                new(Path.Combine(nestedFolder.FullName, "MyDfile.production"))
            ]);
        var config = new AnalysisConfig { ScanAllAnalysis = true, LocalSettings = [], ServerSettings = [new(property, "nested/Dfile,nested/*.dfile,nested/*.Dfile")] };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.FullName).Should().BeEquivalentTo(
            Path.Combine(nestedFolder.FullName, "Dfile"),
            Path.Combine(nestedFolder.FullName, "MyApp.dfile"),
            Path.Combine(nestedFolder.FullName, "MyOtherApp.Dfile"));
        files.Tests.Should().BeEmpty();
    }

    [TestMethod]
    public void AdditionalFiles_WildcardPattern_UsingHardCodedPatterns()
    {
        var nestedFolder = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, "folder"));
        wrapper
            .EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories)
            .Returns([nestedFolder]);
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(ProjectBaseDir.FullName, "Dockerfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyApp.dockerfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyOtherApp.Dockerfile")),
                new(Path.Combine(ProjectBaseDir.FullName, "Dockerfile.production")),
                new(Path.Combine(ProjectBaseDir.FullName, "MyDockerfile.production"))
            ]);
        wrapper
            .EnumerateFiles(nestedFolder, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(nestedFolder.FullName, "Dockerfile")),
                new(Path.Combine(nestedFolder.FullName, "MyApp.dockerfile")),
                new(Path.Combine(nestedFolder.FullName, "MyOtherApp.Dockerfile")),
                new(Path.Combine(nestedFolder.FullName, "Dockerfile.production")),
                new(Path.Combine(nestedFolder.FullName, "MyDockerfile.production"))
            ]);
        var config = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings = []
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.FullName).Should().BeEquivalentTo(
            Path.Combine(ProjectBaseDir.FullName, "Dockerfile"),
            Path.Combine(ProjectBaseDir.FullName, "MyApp.dockerfile"),
            Path.Combine(ProjectBaseDir.FullName, "MyOtherApp.Dockerfile"),
            Path.Combine(ProjectBaseDir.FullName, "Dockerfile.production"),
            Path.Combine(nestedFolder.FullName, "Dockerfile"),
            Path.Combine(nestedFolder.FullName, "MyApp.dockerfile"),
            Path.Combine(nestedFolder.FullName, "MyOtherApp.Dockerfile"),
            Path.Combine(nestedFolder.FullName, "Dockerfile.production"));
        files.Tests.Should().BeEmpty();
    }

    [TestMethod]
    public void AdditionalFiles_WildcardPatternJvmFrameworkConfig_DefaultPatterns()
    {
        var src = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, "src"));
        var main = new DirectoryInfo(Path.Combine(src.FullName, "main"));
        var resources = new DirectoryInfo(Path.Combine(main.FullName, "resources"));
        wrapper
            .EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories)
            .Returns([src, main, resources]);
        wrapper
            .EnumerateFiles(resources, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(resources.FullName, "application.yaml")),
                new(Path.Combine(resources.FullName, "application.yml")),
                new(Path.Combine(resources.FullName, "application.properties")),
                new(Path.Combine(resources.FullName, "docker-compose.yaml")),
                new(Path.Combine(resources.FullName, "docker-compose.yml")),
                new(Path.Combine(resources.FullName, "sonar-project.properties")),
            ]);
        var config = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings =
            [
                // https://github.com/SonarSource/sonar-iac/blob/759acf5ab743361180a9c0c7c33018c9f16bc3d8/iac-common/src/main/java/org/sonar/iac/common/predicates/JvmConfigFilePredicate.java#L33-L34
                new("sonar.java.jvmframeworkconfig.file.patterns", "**/src/main/resources/**/*app*.properties,**/src/main/resources/**/*app*.yaml,**/src/main/resources/**/*app*.yml")
            ]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.FullName).Should().BeEquivalentTo(
            Path.Combine(resources.FullName, "application.yaml"),
            Path.Combine(resources.FullName, "application.yml"),
            Path.Combine(resources.FullName, "application.properties"));
        files.Tests.Should().BeEmpty();
    }

    [TestMethod]
    public void AdditionalFiles_WildcardPatternTextInclusion_DefaultPatterns()
    {
        var src = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, "src"));
        var nested = new DirectoryInfo(Path.Combine(src.FullName, "nested"));
        var aws = new DirectoryInfo(Path.Combine(ProjectBaseDir.FullName, ".aws"));
        wrapper
            .EnumerateDirectories(ProjectBaseDir, "*", SearchOption.AllDirectories)
            .Returns([src, nested, aws]);
        wrapper
            .EnumerateFiles(ProjectBaseDir, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(ProjectBaseDir.FullName, "file.sh")),
                new(Path.Combine(ProjectBaseDir.FullName, "file.bash")),
                new(Path.Combine(ProjectBaseDir.FullName, "file.conf")),
                new(Path.Combine(ProjectBaseDir.FullName, "file.cs")),
                new(Path.Combine(ProjectBaseDir.FullName, ".env")),
            ]);
        wrapper
            .EnumerateFiles(src, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(src.FullName, "file.zsh")),
                new(Path.Combine(src.FullName, "file.ksh")),
                new(Path.Combine(src.FullName, "file.pem")),
                new(Path.Combine(src.FullName, "file.java")),
                new(Path.Combine(src.FullName, ".env")),
            ]);
        wrapper
            .EnumerateFiles(nested, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(nested.FullName, "file.ps1")),
                new(Path.Combine(nested.FullName, "file.properties")),
                new(Path.Combine(nested.FullName, "file.config")),
                new(Path.Combine(nested.FullName, ".env")),
            ]);
        wrapper
            .EnumerateFiles(aws, "*", SearchOption.TopDirectoryOnly)
            .Returns(
            [
                new(Path.Combine(aws.FullName, "config")),
                new(Path.Combine(aws.FullName, "workflow.yaml")),
                new(Path.Combine(aws.FullName, ".env")),
            ]);
        var config = new AnalysisConfig
        {
            ScanAllAnalysis = true,
            LocalSettings = [],
            ServerSettings =
            [
                // https://github.com/SonarSource/sonar-text-enterprise/blob/ab4f194bec799f6fdef294d46041be633747a822/sonar-text-plugin/src/main/java/org/sonar/plugins/common/TextAndSecretsSensor.java#L55
                new("sonar.text.inclusions", "**/*.sh,**/*.bash,**/*.zsh,**/*.ksh,**/*.ps1,**/*.properties,**/*.conf,**/*.pem,**/*.config,.env,.aws/config")
            ]
        };

        var files = sut.AdditionalFiles(config, ProjectBaseDir);

        files.Sources.Select(x => x.FullName).Should().BeEquivalentTo(
            Path.Combine(ProjectBaseDir.FullName, "file.sh"),
            Path.Combine(ProjectBaseDir.FullName, "file.bash"),
            Path.Combine(ProjectBaseDir.FullName, "file.conf"),
            Path.Combine(ProjectBaseDir.FullName, ".env"),
            Path.Combine(src.FullName, "file.zsh"),
            Path.Combine(src.FullName, "file.ksh"),
            Path.Combine(src.FullName, "file.pem"),
            Path.Combine(nested.FullName, "file.ps1"),
            Path.Combine(nested.FullName, "file.properties"),
            Path.Combine(nested.FullName, "file.config"),
            Path.Combine(aws.FullName, "config"));
        files.Tests.Should().BeEmpty();
    }
}
