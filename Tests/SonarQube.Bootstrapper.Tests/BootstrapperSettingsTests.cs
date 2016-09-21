//-----------------------------------------------------------------------
// <copyright file="BootstrapperSettingsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class BootstrapperSettingsTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void BootSettings_InvalidArguments()
        {
            string validUrl = "http://myserver";
            ILogger validLogger = new TestLogger();
            IList<string> validArgs = null;

            AssertException.Expects<ArgumentNullException>(() => new BootstrapperSettings(AnalysisPhase.PostProcessing, validArgs, null, LoggerVerbosity.Debug, validLogger));
            AssertException.Expects<ArgumentNullException>(() => new BootstrapperSettings(AnalysisPhase.PreProcessing, validArgs, validUrl, LoggerVerbosity.Debug, null));
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
                IBootstrapperSettings settings = new BootstrapperSettings(AnalysisPhase.PreProcessing, null, "http://sq", LoggerVerbosity.Debug, logger);
                AssertExpectedServerUrl("http://sq", settings);

            }
        }

        #endregion Tests

        #region Checks

        private static void AssertExpectedServerUrl(string expected, IBootstrapperSettings settings)
        {
            string actual = settings.SonarQubeUrl;
            Assert.AreEqual(expected, actual, true /* ignore case */, "Unexpected server url");
        }

        #endregion Checks
    }
}