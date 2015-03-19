//-----------------------------------------------------------------------
// <copyright file="FileLocatorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestUtilities;

namespace Sonar.Common.UnitTests
{
    [TestClass]
    public class FileLocatorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void FileLoc_GetSonarRunner()
        {
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string runnerFile = Path.Combine(testDir, FileLocator.SonarRunnerFileName);
            File.WriteAllText(runnerFile, "dummy runner file");

            using(TestUtilities.EnvironmentVariableScope scope = new TestUtilities.EnvironmentVariableScope())
            {
                // 1. Not found on path
                scope.SetPath("c:\\");
                string actual = FileLocator.FindDefaultSonarRunnerExecutable();
                Assert.IsNull(actual, "Not expecting the file to be found");

                // 2. Found on path
                scope.SetPath("c:\\;" + testDir);
                actual = FileLocator.FindDefaultSonarRunnerExecutable();
                Assert.IsNotNull(actual, "Expecting the runner file to be found");
                Assert.AreEqual(runnerFile, actual, "Unexpected file name returned");
            }
        }

        [TestMethod]
        public void FileLoc_GetSonarRunnerProperties()
        {
            // 0. Set up
            string runnerRootDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string binDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "bin");
            string configDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "conf");

            string runnerFile = Path.Combine(binDir, FileLocator.SonarRunnerFileName);
            File.WriteAllText(runnerFile, "dummy runner file");

            string configFile = Path.Combine(configDir, "sonar-runner.properties");
            File.WriteAllText(configFile, "dummy runner properties file");

            using (TestUtilities.EnvironmentVariableScope scope = new TestUtilities.EnvironmentVariableScope())
            {
                // 1. Runner not found on path -> nothing
                scope.SetPath("c:\\");
                string actual = FileLocator.FindDefaultSonarRunnerProperties();
                Assert.IsNull(actual, "Not expecting the runner properties file to be found");


                // 2. Runner found on path but no properties file -> not found
                scope.SetPath("c:\\;" + binDir);
                File.Delete(configFile);

                using (new AssertIgnoreScope())
                {
                    actual = FileLocator.FindDefaultSonarRunnerProperties();
                }
                Assert.IsNull(actual, "Not expecting the runner properties file to be found");
                

                // 3. Runner found on path and config file exists in expected relative location -> found
                
                // Create the properties file in the expected location
                configFile = Path.Combine(configDir, "sonar-runner.properties");
                File.WriteAllText(configFile, "dummy runner properties file");

                actual = FileLocator.FindDefaultSonarRunnerProperties();
                Assert.IsNotNull(actual, "Expecting the runner properties file to be found");
                Assert.AreEqual(configFile, actual, "Unexpected file name returned");
            }
        }


        #endregion

    }
}
