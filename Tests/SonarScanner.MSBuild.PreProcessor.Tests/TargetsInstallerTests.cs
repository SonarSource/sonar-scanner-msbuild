/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.UnitTests
{
    // TODO: The tests should be made platform-aware.
    // They currently assume the running platform is Windows.
    // Some of them (like InstallTargetsFile_Overwrite) would fail if run on other OSes.
    [TestClass]
    public class TargetsInstallerTests
    {
        private string WorkingDirectory;
        private TestLogger logger;
        private Mock<IMsBuildPathsSettings> msBuildPathSettingsMock;
        private Mock<IFileWrapper> fileWrapperMock;
        private Mock<IDirectoryWrapper> directoryWrapperMock;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Init()
        {
            CleanupMsbuildDirectories();
            this.WorkingDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "sonarqube");

            this.logger = new TestLogger();
            this.msBuildPathSettingsMock = new Mock<IMsBuildPathsSettings>();
            this.fileWrapperMock = new Mock<IFileWrapper>();
            this.directoryWrapperMock = new Mock<IDirectoryWrapper>();
        }

        [TestCleanup]
        public void TearDown()
        {
            CleanupMsbuildDirectories();
        }

        [TestMethod]
        [Description("The targets file should be copied if none are present. The files should not be copied if they already exist and have not been changed.")]
        public void InstallTargetsFile_Copy()
        {
            // In case the dummy targets file somehow does not get deleted (e.g. when debugging) , make sure its content is valid XML
            var sourceTargetsContent = @"<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";
            CreateDummySourceTargetsFile(sourceTargetsContent);

            InstallTargetsFileAndAssert(sourceTargetsContent, expectCopy: true);

            // if we try to inject again, the targets should not be copied because they have the same content
            InstallTargetsFileAndAssert(sourceTargetsContent, expectCopy: false);
        }

        [TestMethod]
        [Description("The targets should be copied if they don't exist. If they have been changed, the updater should overwrite them")]
        public void InstallTargetsFile_Overwrite()
        {
            // In case the dummy targets file somehow does not get deleted (e.g. when debugging), make sure its content valid XML
            var sourceTargetsContent1 = @"<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";
            var sourceTargetsContent2 = @"<Project ToolsVersion=""12.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";

            CreateDummySourceTargetsFile(sourceTargetsContent1);

            var msBuildPathSettings = new MsBuildPathSettings();

            InstallTargetsFileAndAssert(sourceTargetsContent1, expectCopy: true);
            msBuildPathSettings.GetImportBeforePaths().Should().HaveCount(7, "Expecting six destination directories");

            var path = Path.Combine(msBuildPathSettings.GetImportBeforePaths().First(), FileConstants.ImportBeforeTargetsName);
            File.Delete(path);

            CreateDummySourceTargetsFile(sourceTargetsContent2);
            InstallTargetsFileAndAssert(sourceTargetsContent2, expectCopy: true);
            InstallTargetsFileAndAssert(sourceTargetsContent2, expectCopy: true);
        }

        [TestMethod]
        public void InstallLoaderTargets_GlobalTargets_Exist()
        {
            // Arrange
            var targetsInstaller = new TargetsInstaller(this.logger, this.msBuildPathSettingsMock.Object,
                this.fileWrapperMock.Object, this.directoryWrapperMock.Object);

            this.msBuildPathSettingsMock.Setup(x => x.GetGlobalTargetsPaths()).Returns(new[] { "global" });
            this.fileWrapperMock.Setup(x => x.Exists("global\\SonarQube.Integration.ImportBefore.targets")).Returns(true);

            // Act
            using (new AssertIgnoreScope())
            {
                targetsInstaller.InstallLoaderTargets("c:\\project");
            }

            // Assert
            this.logger.Warnings.Should().Contain(m =>
                m.StartsWith("This version of the SonarScanner for MSBuild automatically deploys"));
        }

        [TestMethod]
        public void InstallLoaderTargets_GlobalTargets_NotExist()
        {
            // Arrange
            var targetsInstaller = new TargetsInstaller(this.logger, this.msBuildPathSettingsMock.Object,
                this.fileWrapperMock.Object, this.directoryWrapperMock.Object);

            this.msBuildPathSettingsMock.Setup(x => x.GetGlobalTargetsPaths()).Returns(new[] { "global" });
            this.fileWrapperMock.Setup(x => x.Exists("global\\SonarQube.Integration.ImportBefore.targets")).Returns(false);

            // Act
            using (new AssertIgnoreScope())
            {
                targetsInstaller.InstallLoaderTargets("c:\\project");
            }

            // Assert
            this.logger.Warnings.Should().NotContain(m =>
                m.StartsWith("This version of the SonarScanner for MSBuild automatically deploys"));
        }

        [TestMethod]
        public void InstallLoaderTargets_Running_Under_LocalSystem_Account()
        {
            // Arrange
            var targetsInstaller = new TargetsInstaller(this.logger, this.msBuildPathSettingsMock.Object,
                this.fileWrapperMock.Object, this.directoryWrapperMock.Object);

            this.msBuildPathSettingsMock.Setup(x => x.GetImportBeforePaths()).Returns(new[] { "c:\\windows\\system32\\appdata" });

            // Act
            Action act = () =>
            {
                using (new AssertIgnoreScope())
                {
                    targetsInstaller.InstallLoaderTargets("c:\\project");
                }
            };

            // Assert
            act.Should().NotThrow();
        }

        [TestMethod]
        public void InstallLoaderTargets_ExceptionsOnCopyAreSuppressed()
        {
            // Arrange
            var targetsInstaller = new TargetsInstaller(this.logger, this.msBuildPathSettingsMock.Object,
                this.fileWrapperMock.Object, this.directoryWrapperMock.Object);

            bool exceptionThrown = false;

            var sourcePathRegex = "bin\\\\(?:debug|release)\\\\targets\\\\SonarQube.Integration.targets";
            this.fileWrapperMock
                .Setup(x => x.ReadAllText(It.IsRegex(sourcePathRegex, RegexOptions.IgnoreCase)))
                .Returns("sourceContent");

            this.fileWrapperMock
                .Setup(x => x.Exists("c:\\project\\bin\\targets\\SonarQube.Integration.targets"))
                .Returns(false);

            this.fileWrapperMock
                .Setup(x => x.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Callback(() =>
                {
                    exceptionThrown = true;
                    throw new InvalidOperationException("This exception should be caught and suppressed by the product code");
                });

            // Act
            using (new AssertIgnoreScope())
            {
                targetsInstaller.InstallLoaderTargets("c:\\project");
            }

            // Assert
            exceptionThrown.Should().BeTrue();
            this.logger.AssertSingleWarningExists("This exception should be caught and suppressed by the product code");
        }

        [TestMethod]
        public void InstallLoaderTargets_InternalCopyTargetFileToProject_Same_Content()
        {
            InstallLoaderTargets_InternalCopyTargetFileToProject(
                sourceContent: "target content",
                destinationExists: true,
                destinationContent: "target content");

            this.logger.DebugMessages.Should().Contain(m =>
                m.Equals("The file SonarQube.Integration.targets is up to date at c:\\project\\bin\\targets"));
        }

        [TestMethod]
        public void InstallLoaderTargets_InternalCopyTargetFileToProject_Different_Content()
        {
            InstallLoaderTargets_InternalCopyTargetFileToProject(
                sourceContent: "target content",
                destinationExists: true,
                destinationContent: "different content");

            this.logger.DebugMessages.Should().Contain(m =>
                m.Equals("The file SonarQube.Integration.targets was overwritten at c:\\project\\bin\\targets"));
        }

        [TestMethod]
        public void InstallLoaderTargets_InternalCopyTargetFileToProject_Not_Exists()
        {
            InstallLoaderTargets_InternalCopyTargetFileToProject(
                sourceContent: "target content",
                destinationExists: false);

            this.logger.DebugMessages.Should().Contain(m =>
                m.Equals("Installed SonarQube.Integration.targets to c:\\project\\bin\\targets"));
        }

        [TestMethod]
        public void InstallLoaderTargets_InternalCopyTargetsFile_Same_Content()
        {
            InstallLoaderTargets_InternalCopyTargetsFile(
                sourceContent: "target content",
                destinationExists: true,
                destinationContent: "target content");

            this.logger.DebugMessages.Should().Contain(m =>
                m.Equals("The file SonarQube.Integration.ImportBefore.targets is up to date at c:\\global paths"));
        }

        [TestMethod]
        public void InstallLoaderTargets_InternalCopyTargetsFile_Different_Content()
        {
            InstallLoaderTargets_InternalCopyTargetsFile(
                sourceContent: "target content",
                destinationExists: true,
                destinationContent: "different content");

            this.logger.DebugMessages.Should().Contain(m =>
                m.Equals("The file SonarQube.Integration.ImportBefore.targets was overwritten at c:\\global paths"));
        }

        [TestMethod]
        public void InstallLoaderTargets_InternalCopyTargetsFile_Not_Exists()
        {
            InstallLoaderTargets_InternalCopyTargetsFile(
                sourceContent: "target content",
                destinationExists: false);

            this.logger.DebugMessages.Should().Contain(m =>
                m.Equals("Installed SonarQube.Integration.ImportBefore.targets to c:\\global paths"));
        }

        private void InstallLoaderTargets_InternalCopyTargetFileToProject(string sourceContent, bool destinationExists, string destinationContent = null)
        {
            // Arrange
            var targetsInstaller = new TargetsInstaller(this.logger, this.msBuildPathSettingsMock.Object,
                this.fileWrapperMock.Object, this.directoryWrapperMock.Object);

            var sourcePathRegex = "bin\\\\(?:debug|release)\\\\targets\\\\SonarQube.Integration.targets";

            this.fileWrapperMock
                .Setup(x => x.ReadAllText(It.IsRegex(sourcePathRegex, RegexOptions.IgnoreCase)))
                .Returns(sourceContent);
            this.fileWrapperMock
                .Setup(x => x.ReadAllText("c:\\project\\bin\\targets\\SonarQube.Integration.targets"))
                .Returns(destinationContent);
            this.fileWrapperMock
                .Setup(x => x.Exists("c:\\project\\bin\\targets\\SonarQube.Integration.targets"))
                .Returns(destinationExists);

            // Act
            using (new AssertIgnoreScope())
            {
                targetsInstaller.InstallLoaderTargets("c:\\project");
            }

            // Assert
            var sameContent = sourceContent.Equals(destinationContent);

            if (!destinationExists || !sameContent)
            {
                // Copy is executed once, overwriting existing files
                this.fileWrapperMock.Verify(
                    x => x.Copy(It.IsRegex(sourcePathRegex, RegexOptions.IgnoreCase), "c:\\project\\bin\\targets\\SonarQube.Integration.targets", true),
                    Times.Once);
            }
            else
            {
                // Copy is not executed
                this.fileWrapperMock.Verify(
                    x => x.Copy(It.IsRegex(sourcePathRegex, RegexOptions.IgnoreCase), "c:\\project\\bin\\targets\\SonarQube.Integration.targets", It.IsAny<bool>()),
                    Times.Never);
            }
        }

        private void InstallLoaderTargets_InternalCopyTargetsFile(string sourceContent, bool destinationExists, string destinationContent = null)
        {
            // Arrange
            var targetsInstaller = new TargetsInstaller(this.logger, this.msBuildPathSettingsMock.Object,
                this.fileWrapperMock.Object, this.directoryWrapperMock.Object);

            var sourcePathRegex = "bin\\\\(?:debug|release)\\\\targets\\\\SonarQube.Integration.ImportBefore.targets";

            this.msBuildPathSettingsMock
                .Setup(x => x.GetImportBeforePaths())
                .Returns(new[] { "c:\\global paths" });
            this.fileWrapperMock
                .Setup(x => x.ReadAllText(It.IsRegex(sourcePathRegex, RegexOptions.IgnoreCase)))
                .Returns(sourceContent);
            this.fileWrapperMock
                .Setup(x => x.ReadAllText("c:\\global paths\\SonarQube.Integration.ImportBefore.targets"))
                .Returns(destinationContent);
            this.fileWrapperMock
                .Setup(x => x.Exists("c:\\global paths\\SonarQube.Integration.ImportBefore.targets"))
                .Returns(destinationExists);

            // Act
            using (new AssertIgnoreScope())
            {
                targetsInstaller.InstallLoaderTargets("c:\\project");
            }

            // Assert
            var sameContent = sourceContent.Equals(destinationContent);

            if (!destinationExists || !sameContent)
            {
                // Copy is executed once, overwriting existing files
                this.fileWrapperMock.Verify(
                    x => x.Copy(It.IsRegex(sourcePathRegex, RegexOptions.IgnoreCase), "c:\\global paths\\SonarQube.Integration.ImportBefore.targets", true),
                    Times.Once);
            }
            else
            {
                // Copy is not executed
                this.fileWrapperMock.Verify(
                    x => x.Copy(It.IsRegex(sourcePathRegex, RegexOptions.IgnoreCase), "c:\\global paths\\SonarQube.Integration.ImportBefore.targets", It.IsAny<bool>()),
                    Times.Never);
            }
        }

        private static void CleanupMsbuildDirectories()
        {
            // SONARMSBRU-149: we used to deploy the targets file to the 4.0 directory but this
            // is no longer supported. To be on the safe side we'll clean up the old location too.
            IList<string> cleanUpDirs = new MsBuildPathSettings().GetImportBeforePaths().ToList();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            cleanUpDirs.Add(Path.Combine(appData, "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"));

            foreach (var destinationDir in cleanUpDirs)
            {
                var path = Path.Combine(destinationDir, FileConstants.ImportBeforeTargetsName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static void CreateDummySourceTargetsFile(string sourceTargetsContent1)
        {
            var exeLocation = Path.GetDirectoryName(typeof(TargetsInstaller).Assembly.Location);

            var dummyLoaderBeforeTargets = Path.Combine(exeLocation, "Targets", FileConstants.ImportBeforeTargetsName);
            var dummyLoaderTargets = Path.Combine(exeLocation, "Targets", FileConstants.IntegrationTargetsName);

            if (File.Exists(dummyLoaderBeforeTargets))
            {
                File.Delete(dummyLoaderBeforeTargets);
            }
            if (File.Exists(dummyLoaderTargets))
            {
                File.Delete(dummyLoaderTargets);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dummyLoaderBeforeTargets));

            File.AppendAllText(dummyLoaderBeforeTargets, sourceTargetsContent1);
            File.AppendAllText(dummyLoaderTargets, sourceTargetsContent1);
        }

        private void InstallTargetsFileAndAssert(string expectedContent, bool expectCopy)
        {
            var localLogger = new TestLogger();
            var installer = new TargetsInstaller(localLogger);

            using (new AssertIgnoreScope())
            {
                installer.InstallLoaderTargets(this.WorkingDirectory);
            }

            var msBuildPathSettings = new MsBuildPathSettings();

            foreach (var destinationDir in msBuildPathSettings.GetImportBeforePaths())
            {
                var path = Path.Combine(destinationDir, FileConstants.ImportBeforeTargetsName);
                File.Exists(path).Should().BeTrue(".targets file not found at: " + path);
                File.ReadAllText(path).Should().Be(expectedContent,
                    ".targets does not have expected content at " + path);

                localLogger.DebugMessages.Any(m => m.Contains(destinationDir)).Should().BeTrue();
            }

            var targetsPath = Path.Combine(this.WorkingDirectory, "bin", "targets", FileConstants.IntegrationTargetsName);
            File.Exists(targetsPath).Should().BeTrue(".targets file not found at: " + targetsPath);

            if (expectCopy)
            {
                localLogger.DebugMessages.Should().HaveCount(msBuildPathSettings.GetImportBeforePaths().Count() + 1,
                    "All destinations should have been covered");
            }
        }
    }
}
