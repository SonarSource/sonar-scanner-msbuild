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

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.AnalysisWarning;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.Interfaces;
using SonarScanner.MSBuild.PostProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor;
using TestUtilities;

namespace SonarScanner.MSBuild.Test.AnalysisWarning
{
    [TestClass]
    public class NetFrameworkBootstrapperClassTests
    {
        private string rootDir;
        private string tempDir;
        private Mock<IProcessorFactory> mockProcessorFactory;
        private Mock<IPreProcessor> mockPreProcessor;
        private Mock<IPostProcessor> mockPostProcessor;
        private Mock<IFrameworkVersionProvider> mockFrameworkVersionProvider;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void MyTestInitialize()
        {
            rootDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
            // this is the Temp folder used by Bootstrapper
            tempDir = Path.Combine(rootDir, ".sonarqube");
            // it will look in Directory.GetCurrentDir, which is RootDir.
            var analysisConfigFile = Path.Combine(tempDir, "conf", "SonarQubeAnalysisConfig.xml");
            CreateAnalysisConfig(analysisConfigFile);
            MockProcessors(true, true);
            mockFrameworkVersionProvider = new Mock<IFrameworkVersionProvider>();
        }

        [TestMethod]
        public void ExecutePostProc_NetFrameworkLowerThan462_SucceedsAndWarningCreated()
        {
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                const string netframework46Warning =
                    "From the 6th of July 2022, new versions of this scanner will no longer support .NET framework runtime environments less than .NET Framework 4.6.2." +
                    " For more information see https://community.sonarsource.com/t/54684";
                // this is usually created by the PreProcessor
                Directory.CreateDirectory(tempDir);
                var outFolder = Path.Combine(tempDir, "out");
                Directory.CreateDirectory(outFolder);
                mockFrameworkVersionProvider.Setup(x => x.IsLowerThan462FrameworkVersion()).Returns(true);

                // Act
                var logger = CheckExecutionSucceeds(AnalysisPhase.PostProcessing, false, true, "other params", "yet.more.params");

                // Assert
                logger.AssertWarningsLogged(0);

                // The bootstrapper pass through any parameters it doesn't recognize so the post-processor
                // can decide whether to handle them or not
                AssertPostProcessorArgs("other params", "yet.more.params");

                var analysisWarningFile = Path.Combine(tempDir, "out", "AnalysisWarnings.Scanner.json");

                File.Exists(analysisWarningFile).Should().BeTrue();
                File.ReadAllText(analysisWarningFile).Should().Contain(netframework46Warning);
            }
        }

        [TestMethod]
        public void ExecutePostProc_NetFrameworHigherOrEqualThan462_SucceedsAndNoWarningCreated()
        {
            using (InitializeNonTeamBuildEnvironment(rootDir))
            {
                // this is usually created by the PreProcessor
                Directory.CreateDirectory(tempDir);

                // Act
                var logger = CheckExecutionSucceeds(AnalysisPhase.PostProcessing, false, false, "other params", "yet.more.params");

                // Assert
                logger.AssertWarningsLogged(0);

                // The bootstrapper pass through any parameters it doesn't recognize so the post-processor
                // can decide whether to handle them or not
                AssertPostProcessorArgs("other params", "yet.more.params");

                var analysisWarningFile = Path.Combine(tempDir, "out", "AnalysisWarnings.Scanner.json");

                File.Exists(analysisWarningFile).Should().BeFalse();
            }
        }

        private TestLogger CheckExecutionSucceeds(AnalysisPhase phase, bool debug, bool lowerThan462FrameworkVersion,  params string[] args)
        {
            var logger = new TestLogger();
            var settings = MockBootstrapSettings(phase, debug, args);
            var bootstrapper = lowerThan462FrameworkVersion
                ? new NetFrameworkBootstrapperClass(mockProcessorFactory.Object, settings, logger, mockFrameworkVersionProvider.Object)
                : new NetFrameworkBootstrapperClass(mockProcessorFactory.Object, settings, logger);

            var exitCode = bootstrapper.Execute().Result;

            exitCode.Should().Be(0, "Bootstrapper did not return the expected exit code");
            logger.AssertErrorsLogged(0);

            return logger;
        }

        private IBootstrapperSettings MockBootstrapSettings(AnalysisPhase phase, bool debug, string[] args)
        {
            Mock<IBootstrapperSettings> mockBootstrapSettings;

            File.Create(Path.Combine(rootDir, "SonarScanner.MSBuild.Common.dll")).Close();
            File.Create(Path.Combine(rootDir, "SonarScanner.MSBuild.Tasks.dll")).Close();
            File.Create(Path.Combine(rootDir, "Newtonsoft.Json.dll")).Close();

            mockBootstrapSettings = new Mock<IBootstrapperSettings>();
            mockBootstrapSettings.SetupGet(x => x.ChildCmdLineArgs).Returns(args.ToArray);
            mockBootstrapSettings.SetupGet(x => x.TempDirectory).Returns(tempDir);
            mockBootstrapSettings.SetupGet(x => x.Phase).Returns(phase);
            mockBootstrapSettings.SetupGet(x => x.ScannerBinaryDirPath).Returns(rootDir);
            mockBootstrapSettings.SetupGet(x => x.LoggingVerbosity).Returns(debug ? LoggerVerbosity.Debug : LoggerVerbosity.Info);

            return mockBootstrapSettings.Object;
        }

        private static EnvironmentVariableScope InitializeNonTeamBuildEnvironment(string workingDirectory)
        {
            Directory.SetCurrentDirectory(workingDirectory);
            return new EnvironmentVariableScope()
                   .SetVariable(BootstrapperSettings.BuildDirectory_Legacy, null)
                   .SetVariable(BootstrapperSettings.BuildDirectory_TFS2015, null);
        }

        private void CreateAnalysisConfig(string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            var config = new AnalysisConfig();
            config.Save(filePath);
        }

        private void MockProcessors(bool preProcessorOutcome, bool postProcessorOutcome)
        {
            mockPreProcessor = new Mock<IPreProcessor>();
            mockPostProcessor = new Mock<IPostProcessor>();
            mockPreProcessor.Setup(x => x.Execute(It.IsAny<string[]>())).Returns(Task.FromResult(preProcessorOutcome));
            mockPostProcessor.Setup(x => x.Execute(It.IsAny<string[]>(), It.IsAny<AnalysisConfig>(), It.IsAny<IBuildSettings>())).Returns(postProcessorOutcome);
            mockProcessorFactory = new Mock<IProcessorFactory>();
            mockProcessorFactory.Setup(x => x.CreatePostProcessor()).Returns(mockPostProcessor.Object);
            mockProcessorFactory.Setup(x => x.CreatePreProcessor()).Returns(mockPreProcessor.Object);
        }

        private void AssertPostProcessorArgs(params string[] expectedArgs) =>
            mockPostProcessor.Verify(x => x.Execute(expectedArgs, It.IsAny<AnalysisConfig>(), It.IsAny<IBuildSettings>()), Times.Once());
    }
}
