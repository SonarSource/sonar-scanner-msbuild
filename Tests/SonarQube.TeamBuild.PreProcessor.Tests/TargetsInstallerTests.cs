/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.UnitTests
{
    // TODO: The tests should be made platform-aware.
    // They currently assume the running platform is Windows.
    // Some of them (like InstallTargetsFile_Overwrite) would fail if run on other OSes.
    [TestClass]
    public class TargetsInstallerTests
    {
        private string WorkingDirectory;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Init()
        {
            CleanupMsbuildDirectories();
            WorkingDirectory = TestUtils.CreateTestSpecificFolder(TestContext, "sonarqube");
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

            InstallTargetsFileAndAssert(sourceTargetsContent1, expectCopy: true);
            Assert.IsTrue(FileConstants.ImportBeforeDestinationDirectoryPaths.Count == 2, "Expecting two destination directories");

            var path = Path.Combine(FileConstants.ImportBeforeDestinationDirectoryPaths[0], FileConstants.ImportBeforeTargetsName);
            File.Delete(path);

            CreateDummySourceTargetsFile(sourceTargetsContent2);
            InstallTargetsFileAndAssert(sourceTargetsContent2, expectCopy: true);
            InstallTargetsFileAndAssert(sourceTargetsContent2, expectCopy: true);
        }

        private static void CleanupMsbuildDirectories()
        {
            // SONARMSBRU-149: we used to deploy the targets file to the 4.0 directory but this
            // is no longer supported. To be on the safe side we'll clean up the old location too.
            IList<string> cleanUpDirs = new List<string>(FileConstants.ImportBeforeDestinationDirectoryPaths);
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
            var installer = new TargetsInstaller();
            var logger = new TestLogger();
            installer.InstallLoaderTargets(logger, WorkingDirectory);

            foreach (var destinationDir in FileConstants.ImportBeforeDestinationDirectoryPaths)
            {
                var path = Path.Combine(destinationDir, FileConstants.ImportBeforeTargetsName);
                Assert.IsTrue(File.Exists(path), ".targets file not found at: " + path);
                Assert.AreEqual(
                    expectedContent,
                    File.ReadAllText(path),
                    ".targets does not have expected content at " + path);

                Assert.IsTrue(logger.DebugMessages.Any(m => m.Contains(destinationDir)));
            }

            var targetsPath = Path.Combine(WorkingDirectory, "bin", "targets", FileConstants.IntegrationTargetsName);
            Assert.IsTrue(File.Exists(targetsPath), ".targets file not found at: " + targetsPath);

            if (expectCopy)
            {
                Assert.AreEqual(
                    FileConstants.ImportBeforeDestinationDirectoryPaths.Count + 1,
                    logger.DebugMessages.Count,
                    "All destinations should have been covered");
            }
        }
    }
}
