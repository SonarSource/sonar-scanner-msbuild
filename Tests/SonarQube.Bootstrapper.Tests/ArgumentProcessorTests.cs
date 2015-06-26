//-----------------------------------------------------------------------
// <copyright file="ArgumentProcessorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.IO;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class ArgumentProcessorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void ArgProc_UnrecognizedArgumentsAreIgnored()
        {
            TestLogger logger = new TestLogger();

            // 1. Minimal command line settings with extra values
            IBootstrapperSettings settings = CheckProcessingSucceeds(logger, "/p:sonar.host.url=foo", "foo", "blah", "/xxxx");
            AssertExpectedUrl("foo", settings);
        }

        [TestMethod]
        public void ArgProc_MissingUrl()
        {
            TestLogger logger;
            
            logger = CheckProcessingFails("/p:SONAR.host.url=foo"); // case-sensitive key name so won't be found
            logger.AssertSingleErrorExists("sonar.host.url");
        }
        
        [TestMethod]
        public void ArgProc_PropertyOverriding()
        {
            // Command line properties should take precedence

            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "settings");
            string fullPropertiesPath = Path.Combine(testDir, "settings.txt");
            AnalysisProperties properties = new AnalysisProperties();
            properties.Add(new Property() { Id = SonarProperties.HostUrl, Value = "http://settingsFile" });
            properties.Save(fullPropertiesPath);

            TestLogger logger = new TestLogger();

            // 1. Settings file only
            IBootstrapperSettings settings = CheckProcessingSucceeds(logger, "/s: " + fullPropertiesPath);
            AssertExpectedUrl("http://settingsFile", settings);

            // 2. Both file and cmd line
            settings = CheckProcessingSucceeds(logger, "/s: " + fullPropertiesPath, "/p:sonar.host.url=http://cmdline");
            AssertExpectedUrl("http://cmdline", settings); // cmd line wins

            // 3. Cmd line only
            settings = CheckProcessingSucceeds(logger, "/p:sonar.host.url=http://cmdline", "/p:other=property", "/p:a=b c");
            AssertExpectedUrl("http://cmdline", settings);
        }

        [TestMethod]
        public void ArgProc_InvalidCmdLineProperties()
        {
            // Incorrectly formed /p:[key]=[value] arguments
            TestLogger logger;

            logger = CheckProcessingFails("/p:sonar.host.url=foo",
                "/p: key1=space before",
                "/p:key2 = space after)");

            logger.AssertSingleErrorExists(" key1");
            logger.AssertSingleErrorExists("key2 ");
        }

        #endregion


        #region Checks methods

        private static IBootstrapperSettings CheckProcessingSucceeds(TestLogger logger, params string[] cmdLineArgs)
        {
            IBootstrapperSettings settings;
            bool success = ArgumentProcessor.TryProcessArgs(cmdLineArgs, logger, out settings);

            Assert.IsTrue(success, "Expecting processing to succeed");
            Assert.IsNotNull(settings, "Settings should not be null if processing succeds");
            logger.AssertErrorsLogged(0);

            return settings;
        }

        private static TestLogger CheckProcessingFails(params string[] cmdLineArgs)
        {
            TestLogger logger = new TestLogger();
            IBootstrapperSettings settings;
            bool success = ArgumentProcessor.TryProcessArgs(cmdLineArgs, logger, out settings);

            Assert.IsFalse(success, "Expecting processing to fail");
            Assert.IsNull(settings, "Settings should be null if processing fails");
            logger.AssertErrorsLogged();

            return logger;
        }

        private static void AssertExpectedUrl(string expected, IBootstrapperSettings settings)
        {
            Assert.AreEqual(expected, settings.SonarQubeUrl, "Unexpected SonarQube URL");
        }

        #endregion
    }
}
