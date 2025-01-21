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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

// TODO: The tests should be made platform-aware.
// They currently assume the running platform is Windows.
// Some of them (like InstallTargetsFile_Overwrite) would fail if run on other OSes.
[TestClass]
public class TargetsInstallerTests
{
    private string workingDirectory;
    private TestLogger logger;
    private IMsBuildPathsSettings msBuildPathSettingsMock;
    private IFileWrapper fileWrapperMock;
    private IDirectoryWrapper directoryWrapperMock;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Init()
    {
        CleanupMsbuildDirectories();
        workingDirectory = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "sonarqube");

        logger = new TestLogger();
        msBuildPathSettingsMock = Substitute.For<IMsBuildPathsSettings>();
        fileWrapperMock = Substitute.For<IFileWrapper>();
        directoryWrapperMock = Substitute.For<IDirectoryWrapper>();
    }

    [TestCleanup]
    public void TearDown() =>
        CleanupMsbuildDirectories();

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

        var msBuildPathSettings = new MsBuildPathSettings(Substitute.For<ILogger>());

        InstallTargetsFileAndAssert(sourceTargetsContent1, expectCopy: true);
        // If the current user account is LocalSystem, then the local application data folder is inside %windir%\system32.
        // When a 32-bit process tries to use this folder on a 64-bit machine, it is redirected to %windir%\SysWOW64.
        // In that case the scanner needs to deploy ImportBefore.targets to both locations, doubling the number of destination directories (14 instead of 7).
        var hasCorrectCount = msBuildPathSettings.GetImportBeforePaths().Count() is 7 or 14;
        hasCorrectCount.Should().BeTrue("Expecting 7 destination directories (or 14 if the local application data folder is located in %windir%)");

        var path = Path.Combine(msBuildPathSettings.GetImportBeforePaths().First(), FileConstants.ImportBeforeTargetsName);
        File.Delete(path);

        CreateDummySourceTargetsFile(sourceTargetsContent2);
        InstallTargetsFileAndAssert(sourceTargetsContent2, expectCopy: true);
        InstallTargetsFileAndAssert(sourceTargetsContent2, expectCopy: true);
    }

    [TestMethod]
    public void InstallLoaderTargets_GlobalTargets_Exist()
    {
        var targetsInstaller = new TargetsInstaller(logger, msBuildPathSettingsMock, fileWrapperMock, directoryWrapperMock);
        msBuildPathSettingsMock.GetGlobalTargetsPaths().Returns(["global"]);
        fileWrapperMock.Exists("global\\SonarQube.Integration.ImportBefore.targets").Returns(true);

        using (new AssertIgnoreScope())
        {
            targetsInstaller.InstallLoaderTargets("c:\\project");
        }

        logger.Warnings.Should().Contain(x => x.StartsWith("This version of the SonarScanner for MSBuild automatically deploys"));
    }

    [TestMethod]
    public void InstallLoaderTargets_GlobalTargets_NotExist()
    {
        var targetsInstaller = new TargetsInstaller(logger, msBuildPathSettingsMock, fileWrapperMock, directoryWrapperMock);

        msBuildPathSettingsMock.GetGlobalTargetsPaths().Returns(["global"]);
        fileWrapperMock.Exists("global\\SonarQube.Integration.ImportBefore.targets").Returns(false);

        using (new AssertIgnoreScope())
        {
            targetsInstaller.InstallLoaderTargets("c:\\project");
        }

        logger.Warnings.Should().NotContain(x => x.StartsWith("This version of the SonarScanner for MSBuild automatically deploys"));
    }

    [TestMethod]
    public void InstallLoaderTargets_Running_Under_LocalSystem_Account()
    {
        var targetsInstaller = new TargetsInstaller(logger, msBuildPathSettingsMock, fileWrapperMock, directoryWrapperMock);
        msBuildPathSettingsMock.GetGlobalTargetsPaths().Returns(["c:\\windows\\system32\\appdata"]);

        Action act = () =>
        {
            using (new AssertIgnoreScope())
            {
                targetsInstaller.InstallLoaderTargets("c:\\project");
            }
        };

        act.Should().NotThrow();
    }

    [TestMethod]
    public void InstallLoaderTargets_ExceptionsOnCopyAreSuppressed()
    {
        var targetsInstaller = new TargetsInstaller(logger, msBuildPathSettingsMock, fileWrapperMock, directoryWrapperMock);
        var exceptionThrown = false;
        var sourcePathRegex = "bin\\\\(?:debug|release)\\\\targets\\\\SonarQube.Integration.targets";

        fileWrapperMock
            .ReadAllText(Arg.Is<string>(x => Regex.IsMatch(x, sourcePathRegex, RegexOptions.IgnoreCase)))
            .Returns("sourceContent");
        fileWrapperMock.Exists("c:\\project\\bin\\targets\\SonarQube.Integration.targets").Returns(false);
        fileWrapperMock
            .When(x => x.Copy(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(x =>
            {
                exceptionThrown = true;
                throw new InvalidOperationException("This exception should be caught and suppressed by the product code");
            });

        using (new AssertIgnoreScope())
        {
            targetsInstaller.InstallLoaderTargets("c:\\project");
        }

        exceptionThrown.Should().BeTrue();
        logger.AssertSingleWarningExists("This exception should be caught and suppressed by the product code");
    }

    [TestMethod]
    public void InstallLoaderTargets_InternalCopyTargetFileToProject_Same_Content()
    {
        InstallLoaderTargets_InternalCopyTargetFileToProject(
            sourceContent: "target content",
            destinationExists: true,
            destinationContent: "target content");

        logger.DebugMessages.Should().Contain(x => x.Equals("The file SonarQube.Integration.targets is up to date at c:\\project\\bin\\targets"));
    }

    [TestMethod]
    public void InstallLoaderTargets_InternalCopyTargetFileToProject_Different_Content()
    {
        InstallLoaderTargets_InternalCopyTargetFileToProject(
            sourceContent: "target content",
            destinationExists: true,
            destinationContent: "different content");

        logger.DebugMessages.Should().Contain(x => x.Equals("The file SonarQube.Integration.targets was overwritten at c:\\project\\bin\\targets"));
    }

    [TestMethod]
    public void InstallLoaderTargets_InternalCopyTargetFileToProject_Not_Exists()
    {
        InstallLoaderTargets_InternalCopyTargetFileToProject(
            sourceContent: "target content",
            destinationExists: false);

        logger.DebugMessages.Should().Contain(x => x.Equals("Installed SonarQube.Integration.targets to c:\\project\\bin\\targets"));
    }

    [TestMethod]
    public void InstallLoaderTargets_InternalCopyTargetsFile_Same_Content()
    {
        InstallLoaderTargets_InternalCopyTargetsFile(
            sourceContent: "target content",
            destinationExists: true,
            destinationContent: "target content");

        logger.DebugMessages.Should().Contain(x => x.Equals("The file SonarQube.Integration.ImportBefore.targets is up to date at c:\\global paths"));
    }

    [TestMethod]
    public void InstallLoaderTargets_InternalCopyTargetsFile_Different_Content()
    {
        InstallLoaderTargets_InternalCopyTargetsFile(
            sourceContent: "target content",
            destinationExists: true,
            destinationContent: "different content");

        logger.DebugMessages.Should().Contain(x => x.Equals("The file SonarQube.Integration.ImportBefore.targets was overwritten at c:\\global paths"));
    }

    [TestMethod]
    public void InstallLoaderTargets_InternalCopyTargetsFile_Not_Exists()
    {
        InstallLoaderTargets_InternalCopyTargetsFile(
            sourceContent: "target content",
            destinationExists: false);

        logger.DebugMessages.Should().Contain(x => x.Equals("Installed SonarQube.Integration.ImportBefore.targets to c:\\global paths"));
    }

    private void InstallLoaderTargets_InternalCopyTargetFileToProject(string sourceContent, bool destinationExists, string destinationContent = null)
    {
        const string DestinationPath = @"c:\project\bin\targets\SonarQube.Integration.targets";

        var targetsInstaller = new TargetsInstaller(logger, msBuildPathSettingsMock, fileWrapperMock, directoryWrapperMock);
        var sourcePath = Path.Combine(
            Path.GetDirectoryName(typeof(ArgumentProcessor).Assembly.Location),
            "Targets",
            "SonarQube.Integration.targets");
        fileWrapperMock.ReadAllText(sourcePath).Returns(sourceContent);
        fileWrapperMock.ReadAllText(DestinationPath).Returns(destinationContent);
        fileWrapperMock.Exists(DestinationPath).Returns(destinationExists);

        using (new AssertIgnoreScope())
        {
            targetsInstaller.InstallLoaderTargets("c:\\project");
        }

        var sameContent = sourceContent.Equals(destinationContent);
        if (!destinationExists || !sameContent)
        {
            // Copy is executed once, overwriting existing files
            fileWrapperMock.Received(1).Copy(sourcePath, DestinationPath, true);
        }
        else
        {
            // Copy is not executed
            fileWrapperMock.DidNotReceive().Copy(sourcePath, DestinationPath, Arg.Any<bool>());
        }
    }

    private void InstallLoaderTargets_InternalCopyTargetsFile(string sourceContent, bool destinationExists, string destinationContent = null)
    {
        var targetsInstaller = new TargetsInstaller(logger, msBuildPathSettingsMock, fileWrapperMock, directoryWrapperMock);
        var sourcePath = Path.Combine(
            Path.GetDirectoryName(typeof(ArgumentProcessor).Assembly.Location),
            "Targets",
            "SonarQube.Integration.ImportBefore.targets");

        msBuildPathSettingsMock.GetImportBeforePaths().Returns(["c:\\global paths"]);
        fileWrapperMock.ReadAllText(sourcePath).Returns(sourceContent);
        fileWrapperMock.ReadAllText("c:\\global paths\\SonarQube.Integration.ImportBefore.targets").Returns(destinationContent);
        fileWrapperMock.Exists("c:\\global paths\\SonarQube.Integration.ImportBefore.targets").Returns(destinationExists);

        using (new AssertIgnoreScope())
        {
            targetsInstaller.InstallLoaderTargets("c:\\project");
        }

        var sameContent = sourceContent.Equals(destinationContent);
        if (!destinationExists || !sameContent)
        {
            // Copy is executed once, overwriting existing files
            fileWrapperMock.Received(1).Copy(sourcePath, "c:\\global paths\\SonarQube.Integration.ImportBefore.targets", true);
        }
        else
        {
            // Copy is not executed
            fileWrapperMock.DidNotReceive().Copy(sourcePath, "c:\\global paths\\SonarQube.Integration.ImportBefore.targets", Arg.Any<bool>());
        }
    }

    private static void CleanupMsbuildDirectories()
    {
        // SONARMSBRU-149: we used to deploy the targets file to the 4.0 directory but this
        // is no longer supported. To be on the safe side we'll clean up the old location too.
        IList<string> cleanUpDirs = new MsBuildPathSettings(Substitute.For<ILogger>()).GetImportBeforePaths().ToList();

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
            installer.InstallLoaderTargets(this.workingDirectory);
        }

        var msBuildPathSettings = new MsBuildPathSettings(Substitute.For<ILogger>());

        foreach (var destinationDir in msBuildPathSettings.GetImportBeforePaths())
        {
            var path = Path.Combine(destinationDir, FileConstants.ImportBeforeTargetsName);
            File.Exists(path).Should().BeTrue(".targets file not found at: " + path);
            File.ReadAllText(path).Should().Be(expectedContent,
                ".targets does not have expected content at " + path);

            localLogger.DebugMessages.Should().Contain(x => x.Contains(destinationDir));
        }

        var targetsPath = Path.Combine(this.workingDirectory, "bin", "targets", FileConstants.IntegrationTargetsName);
        File.Exists(targetsPath).Should().BeTrue(".targets file not found at: " + targetsPath);

        if (expectCopy)
        {
            localLogger.DebugMessages.Should().HaveCount(msBuildPathSettings.GetImportBeforePaths().Count() + 1,
                "All destinations should have been covered");
        }
    }
}
