//-----------------------------------------------------------------------
// <copyright file="BootstrapperSettingsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.IO;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class BootstrapperSettingsTests
    {
        public TestContext TestContext { get; set; }

        private static readonly string DownloadFolderRelativePath = Path.Combine(BootstrapperSettings.RelativePathToTempDir, BootstrapperSettings.RelativePathToDownloadDir);

        #region Tests

        [TestMethod]
        public void BootSettings_InvalidArguments()
        {
            string validUrl = "http://myserver";
            ILogger validLogger = new TestLogger();
            string validArgs = string.Empty;

            AssertException.Expects<ArgumentNullException>(() => new BootstrapperSettings(AnalysisPhase.PostProcessing, validArgs, null, validLogger));
            AssertException.Expects<ArgumentNullException>(() => new BootstrapperSettings(AnalysisPhase.PreProcessing, validArgs, validUrl, null));
        }

        [TestMethod]
        public void BootSettings_Properties()
        {
            // Check the properties values and that relative paths are turned into absolute paths

            // 0. Setup
            TestLogger logger = new TestLogger();

            using (EnvironmentVariableScope envScope = new EnvironmentVariableScope())
            {
                envScope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, @"c:\temp");

                // 1. Default value -> relative to download dir
                IBootstrapperSettings settings = new BootstrapperSettings(AnalysisPhase.PreProcessing, string.Empty, "http://sq", logger);
                AssertExpectedServerUrl("http://sq", settings);
                AssertExpectedPreProcessPath(Path.Combine(@"c:\temp", DownloadFolderRelativePath, BootstrapperSettings.PreProcessorExeName), settings);
                AssertExpectedPostProcessPath(Path.Combine(@"c:\temp", DownloadFolderRelativePath, BootstrapperSettings.PostProcessorExeName), settings);
            }
        }

        [TestMethod]
        public void BootSettings_DownloadDirFromEnvVars()
        {
            // 0. Setup
            TestLogger logger = new TestLogger();

            // 1. Legacy TFS variable will be used if available
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, "legacy tf build");
                scope.SetVariable(BootstrapperSettings.BuildDirectory_TFS2015, null);

                IBootstrapperSettings settings = new BootstrapperSettings(AnalysisPhase.PreProcessing, string.Empty, "http://sq", logger);
                AssertExpectedDownloadDir(Path.Combine("legacy tf build", DownloadFolderRelativePath), settings);
            }

            // 2. TFS2015 variable will be used if available
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, null);
                scope.SetVariable(BootstrapperSettings.BuildDirectory_TFS2015, "tfs build");

                IBootstrapperSettings settings = new BootstrapperSettings(AnalysisPhase.PreProcessing, string.Empty, "http://sq", logger);
                AssertExpectedDownloadDir(Path.Combine("tfs build", DownloadFolderRelativePath), settings);
            }

            // 3. CWD has least precedence over env variables
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, null);
                scope.SetVariable(BootstrapperSettings.BuildDirectory_TFS2015, null);

                IBootstrapperSettings settings = new BootstrapperSettings(AnalysisPhase.PreProcessing, "http://sq", string.Empty, logger);
                AssertExpectedDownloadDir(Path.Combine(Directory.GetCurrentDirectory(), DownloadFolderRelativePath), settings);
            }
        }

        #endregion Tests

        #region Checks

        private static void AssertExpectedDownloadDir(string expected, IBootstrapperSettings settings)
        {
            string actual = settings.DownloadDirectory;
            Assert.AreEqual(expected, actual, "Unexpected download dir", true /* ignore case */);
        }

        private static void AssertExpectedPreProcessPath(string expected, IBootstrapperSettings settings)
        {
            string actual = settings.PreProcessorFilePath;
            Assert.AreEqual(expected, actual, true /* ignore case */, "Unexpected PreProcessFilePath");
        }

        private static void AssertExpectedPostProcessPath(string expected, IBootstrapperSettings settings)
        {
            string actual = settings.PostProcessorFilePath;
            Assert.AreEqual(expected, actual, true /* ignore case */, "Unexpected PostProcessFilePath");
        }

        private static void AssertExpectedServerUrl(string expected, IBootstrapperSettings settings)
        {
            string actual = settings.SonarQubeUrl;
            Assert.AreEqual(expected, actual, true /* ignore case */, "Unexpected server url");
        }

        #endregion Checks
    }
}