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

using SonarScanner.MSBuild.PostProcessor;
using SonarScanner.MSBuild.Shim;
using SonarScanner.MSBuild.TFS;

namespace SonarScanner.MSBuild.Test;

[TestClass]
public class BootstrapperClassTests
{
    private const int ErrorCode = 1;
    private string rootDir;
    private string tempDir;
    private IProcessorFactory processorFactory;
    private PreProcessor.PreProcessor preProcessor;
    private PostProcessor.PostProcessor postProcessor;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TestInitialize()
    {
        rootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        // this is the Temp folder used by Bootstrapper
        tempDir = Path.Combine(rootDir, ".sonarqube");
        // it will look in Directory.GetCurrentDir, which is RootDir.
        var analysisConfigFile = Path.Combine(tempDir, "conf", "SonarQubeAnalysisConfig.xml");
        CreateAnalysisConfig(analysisConfigFile);
        MockProcessors(true, true);
    }

    [TestMethod]
    public void Exe_PreProcFails()
    {
        TestUtils.EnsureDefaultPropertiesFileDoesNotExist();
        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            MockProcessors(false, true);

            var logger = CheckExecutionFails(
                AnalysisPhase.PreProcessing,
                true,
                null,
                "/install:true",  // this argument should just pass through
                "/d:sonar.verbose=true",
                "/d:sonar.host.url=http://host:9",
                "/d:another.key=will be ignored");

            logger.AssertWarningsLogged(0);
            logger.AssertVerbosity(LoggerVerbosity.Debug);
            AssertPreProcessorArgs(
                "/install:true",
                "/d:sonar.verbose=true",
                "/d:sonar.host.url=http://host:9",
                "/d:another.key=will be ignored");
            AssertPostProcessorNotCalled();
        }
    }

    [TestMethod]
    public void CopyDlls_WhenFileDoNotExist_FilesAreCopied()
    {
        TestUtils.EnsureDefaultPropertiesFileDoesNotExist();
        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeFalse();
            File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeFalse();
            File.Exists(Path.Combine(tempDir, "bin", "Newtonsoft.Json.dll")).Should().BeFalse();

            CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, null, "/d:sonar.host.url=http://anotherHost");

            File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "bin", "Newtonsoft.Json.dll")).Should().BeTrue();
        }
    }

    [TestMethod]
    public void CopyDlls_WhenFileExistButAreNotLocked_FilesAreCopied()
    {
        TestUtils.EnsureDefaultPropertiesFileDoesNotExist();
        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "bin"));
            File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Close();
            File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Close();
            File.Create(Path.Combine(tempDir, "bin", "Newtonsoft.Json.dll")).Close();

            CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, null, "/d:sonar.host.url=http://anotherHost");

            File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "bin", "Newtonsoft.Json.dll")).Should().BeTrue();
        }
    }

    [TestMethod]
    public void CopyDlls_WhenFileExistAndAreLockedButSameVersion_DoNothing()
    {
        TestUtils.EnsureDefaultPropertiesFileDoesNotExist();
        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "bin"));
            var file1 = File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll"));
            var file2 = File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll"));
            var file3 = File.Create(Path.Combine(tempDir, "bin", "Newtonsoft.Json.dll"));

            CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, _ => new Version(), "/d:sonar.host.url=http://anotherHost");

            File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "bin", "Newtonsoft.Json.dll")).Should().BeTrue();
            file1.Close();
            file2.Close();
            file3.Close();
        }
    }

    [TestCategory(TestCategories.NoLinux)]
    [TestCategory(TestCategories.NoMacOS)]
    // Windows uses mandatory locking, enforced at the OS level so this correctly throws,
    // Unix uses advisory locking, the convention is to use a .lock file, which we do not do, so we do not throw on unix
    [TestMethod]
    public void CopyDlls_WhenFileExistAndAreLockedButDifferentVersion_Fails()
    {
        TestUtils.EnsureDefaultPropertiesFileDoesNotExist();

        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "bin"));
            var file1 = File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll"));
            var file2 = File.Create(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll"));
            var file3 = File.Create(Path.Combine(tempDir, "bin", "Newtonsoft.Json.dll"));
            var callCount = 0;
            Func<string, Version> assemblyVersion = _ =>
            {
                if (callCount == 0)
                {
                    callCount++;
                    return new Version("1.0");
                }
                return new Version("2.0");
            };

            var logger = CheckExecutionFails(AnalysisPhase.PreProcessing, false, assemblyVersion, "/d:sonar.host.url=http://anotherHost");

            File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Common.dll")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "bin", "SonarScanner.MSBuild.Tasks.dll")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "bin", "Newtonsoft.Json.dll")).Should().BeTrue();
            logger.DebugMessages.Should().HaveCount(4);
            logger.DebugMessages[0].Should().Match(@"Cannot delete directory: '*\.sonarqube\bin' because The process cannot access the file 'Newtonsoft.Json.dll' because it is being used by another process..");
            logger.DebugMessages[1].Should().Match(@"Cannot delete file: '*\.sonarqube\bin\Newtonsoft.Json.dll' because The process cannot access the file '*Newtonsoft.Json.dll' because it is being used by another process..");
            logger.DebugMessages[2].Should().Match(@"Cannot delete file: '*\.sonarqube\bin\SonarScanner.MSBuild.Common.dll' because The process cannot access the file '*SonarScanner.MSBuild.Common.dll' because it is being used by another process..");
            logger.DebugMessages[3].Should().Match(@"Cannot delete file: '*\.sonarqube\bin\SonarScanner.MSBuild.Tasks.dll' because The process cannot access the file '*SonarScanner.MSBuild.Tasks.dll' because it is being used by another process..");
            logger.AssertErrorLogged("""
                Cannot copy a different version of the SonarScanner for .NET assemblies because they are used by a running MSBuild/.Net Core process. To resolve this problem try one of the following:
                - Analyze this project using the same version of SonarScanner for .NET
                - Build your project with the '/nr:false' switch
                """);
            file1.Close();
            file2.Close();
            file3.Close();
        }
    }

    [TestMethod]
    public void Exe_PreProcSucceeds()
    {
        TestUtils.EnsureDefaultPropertiesFileDoesNotExist();
        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            var logger = CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, null, "/d:sonar.host.url=http://anotherHost");

            logger.AssertWarningsLogged(0);
            logger.AssertVerbosity(VerbosityCalculator.DefaultLoggingVerbosity);
            AssertPreProcessorArgs("/d:sonar.host.url=http://anotherHost");
        }
    }

    [TestMethod]
    public void Exe_PreProcCleansTemp()
    {
        TestUtils.EnsureDefaultPropertiesFileDoesNotExist();
        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            var filePath = Path.Combine(tempDir, "myfile");
            Directory.CreateDirectory(tempDir);
            var stream = File.Create(filePath);
            stream.Close();
            File.Exists(filePath).Should().BeTrue();

            CheckExecutionSucceeds(AnalysisPhase.PreProcessing, false, null, "/d:sonar.host.url=http://anotherHost");

            File.Exists(filePath).Should().BeFalse();
        }
    }

    [TestMethod]
    public void Exe_PostProc_Fails()
    {
        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            MockProcessors(true, false);
            Directory.CreateDirectory(tempDir);

            var logger = CheckExecutionFails(AnalysisPhase.PostProcessing, false);

            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(1);
            AssertPostProcessorArgs();
        }
    }

    [TestMethod]
    public void Exe_PostProc_Fails_On_Missing_TempFolder()
    {
        Directory.Delete(tempDir, true);

        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            var logger = CheckExecutionFails(AnalysisPhase.PostProcessing, false);
            logger.AssertErrorsLogged(2);
        }
    }

    [TestMethod]
    public void Exe_PostProc_Succeeds()
    {
        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            Directory.CreateDirectory(tempDir);
            var logger = CheckExecutionSucceeds(AnalysisPhase.PostProcessing, false, null, "other params", "yet.more.params");

            logger.AssertWarningsLogged(0);
            // The bootstrapper passes through any parameters it doesn't recognize so the post-processor
            // can decide whether to handle them or not
            AssertPostProcessorArgs("other params", "yet.more.params");
        }
    }

    [TestMethod]
    public void Exe_PostProc_NoAnalysisConfig()
    {
        using (InitializeNonTeamBuildEnvironment(rootDir))
        {
            Directory.CreateDirectory(tempDir);
            var analysisConfigFile = Path.Combine(tempDir, "conf", "SonarQubeAnalysisConfig.xml");
            File.Delete(analysisConfigFile);

            var logger = CheckExecutionFails(AnalysisPhase.PostProcessing, false, null, "other params", "yet.more.params");

            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(2);
            AssertPostProcessorNotCalled();
        }
    }

    private static void CreateAnalysisConfig(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        var config = new AnalysisConfig();
        config.Save(filePath);
    }

    private void MockProcessors(bool preProcessorOutcome, bool postProcessorOutcome)
    {
        preProcessor = Substitute.For<PreProcessor.PreProcessor>(Substitute.For<PreProcessor.IPreprocessorObjectFactory>(), new TestRuntime());
        postProcessor = Substitute.For<PostProcessor.PostProcessor>(
            Substitute.For<SonarScannerWrapper>(Substitute.For<IRuntime>()),
            Substitute.For<SonarEngineWrapper>(Substitute.For<IRuntime>(), Substitute.For<IProcessRunner>()),
            Substitute.For<IRuntime>(),
            Substitute.For<TargetsUninstaller>(Substitute.For<ILogger>()),
            Substitute.For<TfsProcessorWrapper>(Substitute.For<IRuntime>()),
            Substitute.For<SonarProjectPropertiesValidator>(),
            Substitute.For<BuildVNextCoverageReportProcessor>(Substitute.For<ICoverageReportConverter>(), Substitute.For<IRuntime>()),
            Substitute.For<IFileWrapper>());
        processorFactory = Substitute.For<IProcessorFactory>();
        preProcessor.Execute(Arg.Any<string[]>()).Returns(Task.FromResult(preProcessorOutcome));
        postProcessor.Execute(Arg.Any<string[]>(), Arg.Any<AnalysisConfig>(), Arg.Any<IBuildSettings>()).Returns(postProcessorOutcome);
        processorFactory.CreatePostProcessor().Returns(postProcessor);
        processorFactory.CreatePreProcessor().Returns(preProcessor);
    }

    private static EnvironmentVariableScope InitializeNonTeamBuildEnvironment(string workingDirectory)
    {
        Directory.SetCurrentDirectory(workingDirectory);
        return new EnvironmentVariableScope()
            .SetVariable(EnvironmentVariables.BuildDirectoryLegacy, null)
            .SetVariable(EnvironmentVariables.BuildDirectoryTfs2015, null);
    }

    private TestLogger CheckExecutionFails(AnalysisPhase phase, bool debug, Func<string, Version> getAssemblyVersion = null, params string[] args)
    {
        var logger = new TestLogger();
        var settings = MockBootstrapSettings(phase, debug, args);
        var bootstrapper = getAssemblyVersion is null
            ? new BootstrapperClass(processorFactory, settings, logger)
            : new BootstrapperClass(processorFactory, settings, logger, getAssemblyVersion);

        var exitCode = bootstrapper.Execute().Result;
        exitCode.Should().Be(ErrorCode, "Bootstrapper did not return the expected exit code");
        logger.AssertErrorsLogged();
        return logger;
    }

    private TestLogger CheckExecutionSucceeds(AnalysisPhase phase, bool debug, Func<string, Version> getAssemblyVersion = null, params string[] args)
    {
        var logger = new TestLogger();
        var settings = MockBootstrapSettings(phase, debug, args);
        var bootstrapper = getAssemblyVersion is null
            ? new BootstrapperClass(processorFactory, settings, logger)
            : new BootstrapperClass(processorFactory, settings, logger, getAssemblyVersion);
        var exitCode = bootstrapper.Execute().Result;

        exitCode.Should().Be(0, "Bootstrapper did not return the expected exit code");
        logger.AssertErrorsLogged(0);

        return logger;
    }

    private IBootstrapperSettings MockBootstrapSettings(AnalysisPhase phase, bool debug, string[] args)
    {
        var mockBootstrapSettings = Substitute.For<IBootstrapperSettings>();

        File.Create(Path.Combine(rootDir, "SonarScanner.MSBuild.Common.dll")).Close();
        File.Create(Path.Combine(rootDir, "SonarScanner.MSBuild.Tasks.dll")).Close();
        File.Create(Path.Combine(rootDir, "Newtonsoft.Json.dll")).Close();

        mockBootstrapSettings.ChildCmdLineArgs.Returns(args.ToArray());
        mockBootstrapSettings.TempDirectory.Returns(tempDir);
        mockBootstrapSettings.Phase.Returns(phase);
        mockBootstrapSettings.ScannerBinaryDirPath.Returns(rootDir);
        mockBootstrapSettings.LoggingVerbosity.Returns(debug ? LoggerVerbosity.Debug : LoggerVerbosity.Info);

        return mockBootstrapSettings;
    }

    private void AssertPostProcessorNotCalled() =>
        postProcessor.DidNotReceive().Execute(Arg.Any<string[]>(), Arg.Any<AnalysisConfig>(), Arg.Any<IBuildSettings>());

    private void AssertPostProcessorArgs(params string[] expectedArgs) =>
        postProcessor.Received(1).Execute(Arg.Is<string[]>(x => x.SequenceEqual(expectedArgs)), Arg.Any<AnalysisConfig>(), Arg.Any<IBuildSettings>());

    private void AssertPreProcessorArgs(params string[] expectedArgs) =>
        preProcessor.Received(1).Execute(Arg.Is<string[]>(x => x.SequenceEqual(expectedArgs)));
}
