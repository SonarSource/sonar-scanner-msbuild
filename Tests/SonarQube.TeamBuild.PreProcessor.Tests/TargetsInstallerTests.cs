//-----------------------------------------------------------------------
// <copyright file="TargetsInstallerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using TestUtilities;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor.UnitTests
{
    [TestClass]
    public class TargetsInstallerTests
    {

        [TestMethod]
        [Description("The targets file should be copied if none are present. The files should not be copied if they already exist and have not been changed.")]
        public void InstallTargetsFile_Copy()
        {
            CleanupMsbuildDirectories();

            // In case the dummy targets file somehow does not get deleted (e.g. when debugging) , make sure its content is valid XML
            string sourceTargetsContent = @"<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";
            CreateDummySourceTargetsFile(sourceTargetsContent);

            try
            {
                InstallTargetsFileAndAssert(sourceTargetsContent, expectCopy: true);

                // if we try to inject again, the targets should not be copied because they have the same content
                InstallTargetsFileAndAssert(sourceTargetsContent, expectCopy: false);
            }
            finally
            {
                CleanupMsbuildDirectories();
            }
        }

        [TestMethod]
        [Description("The targets should be copied if they don't exist. If they have been changed, the updater should overwrite them")]
        public void InstallTargetsFile_Overwrite()
        {
            CleanupMsbuildDirectories();

            // In case the dummy targets file somehow does not get deleted (e.g. when debugging), make sure its content valid XML
            string sourceTargetsContent1 = @"<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";
            string sourceTargetsContent2 = @"<Project ToolsVersion=""12.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" />";

            CreateDummySourceTargetsFile(sourceTargetsContent1);

            try
            {
                InstallTargetsFileAndAssert(sourceTargetsContent1, expectCopy: true);
                Assert.IsTrue(TargetsInstaller.DestinationDirs.Count == 2, "Expecting two destination directories");

                string path = Path.Combine(TargetsInstaller.DestinationDirs[0], TargetsInstaller.LoaderTargetsName);
                File.Delete(path);

                CreateDummySourceTargetsFile(sourceTargetsContent2);
                InstallTargetsFileAndAssert(sourceTargetsContent2, expectCopy: true);
            }
            finally
            {
                CleanupMsbuildDirectories();
            }
        }

        private static void CleanupMsbuildDirectories()
        {
            // SONARMSBRU-149: we used to deploy the targets file to the 4.0 directory but this
            // is no longer supported. To be on the safe side we'll clean up the old location too.
            IList<string> cleanUpDirs = new List<string>(TargetsInstaller.DestinationDirs);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            cleanUpDirs.Add(Path.Combine(appData, "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore"));

            foreach (string destinationDir in cleanUpDirs)
            {
                string path = Path.Combine(destinationDir, TargetsInstaller.LoaderTargetsName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static void CreateDummySourceTargetsFile(string sourceTargetsContent1)
        {
            string exeLocation = Path.GetDirectoryName(typeof(TargetsInstaller).Assembly.Location);
            string dummyLoaderTargets = Path.Combine(exeLocation, "Targets", TargetsInstaller.LoaderTargetsName);

            if (File.Exists(dummyLoaderTargets))
            {
                File.Delete(dummyLoaderTargets);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dummyLoaderTargets));

            File.AppendAllText(dummyLoaderTargets, sourceTargetsContent1);
        }

        private static void InstallTargetsFileAndAssert(string expectedContent, bool expectCopy)
        {
            TargetsInstaller installer = new TargetsInstaller();
            TestLogger logger = new TestLogger();
            installer.InstallLoaderTargets(logger);

            foreach (string destinationDir in TargetsInstaller.DestinationDirs)
            {
                string path = Path.Combine(destinationDir, TargetsInstaller.LoaderTargetsName);
                Assert.IsTrue(File.Exists(path), ".targets file not found at: " + path);
                Assert.AreEqual(
                    expectedContent,
                    File.ReadAllText(path),
                    ".targets does not have expected content at " + path);

                Assert.IsTrue(logger.DebugMessages.Any(m => m.Contains(destinationDir)));
            }

            if (expectCopy)
            {
                Assert.AreEqual(
                    TargetsInstaller.DestinationDirs.Count,
                    logger.DebugMessages.Count,
                    "All destinations should have been covered");
            }

        }
    }
}
