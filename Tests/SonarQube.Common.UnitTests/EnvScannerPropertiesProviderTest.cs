using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class EnvScannerPropertiesProviderTest
    {
        [TestMethod]
        public void ParseValidJson()
        {
            EnvScannerPropertiesProvider provider = new EnvScannerPropertiesProvider("{ \"sonar.host.url\": \"http://myhost\"}");
            Assert.AreEqual(provider.GetAllProperties().First().Id, "sonar.host.url");
            Assert.AreEqual(provider.GetAllProperties().First().Value, "http://myhost");
            Assert.AreEqual(1, provider.GetAllProperties().Count());
        }

        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable("SONARQUBE_SCANNER_PARAMS", null);
        }

        [TestMethod]
        public void ParseInvalidJson()
        {
            IAnalysisPropertyProvider provider;
            TestLogger logger = new TestLogger();
            Environment.SetEnvironmentVariable("SONARQUBE_SCANNER_PARAMS", "trash");
            bool result = EnvScannerPropertiesProvider.TryCreateProvider(logger, out provider);
            Assert.IsFalse(result);
            logger.AssertErrorLogged("Failed to parse properties from the environment variable 'SONARQUBE_SCANNER_PARAMS'");
        }

        [TestMethod]
        public void NonExistingEnvVar()
        {
            EnvScannerPropertiesProvider provider = new EnvScannerPropertiesProvider(null);
            Assert.AreEqual(0, provider.GetAllProperties().Count());
        }
    }
}
