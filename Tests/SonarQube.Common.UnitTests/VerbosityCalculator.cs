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
    public class VerbosityCalculatorTests
    {
        [TestMethod]
        public void FromAnalysisConfig_Verbose()
        {
            // Arrange
            AnalysisConfig config = new AnalysisConfig();
            config.AdditionalSettings = new List<AnalysisSetting>();
            config.AdditionalSettings.Add(new AnalysisSetting() { Id = SonarProperties.Verbose, Value = "true", Inherited = false });
            config.AdditionalSettings.Add(new AnalysisSetting() { Id = SonarProperties.LogLevel, Value = "INFO", Inherited = false });

            // Act
            var verbosity = VerbosityCalculator.ComputeVerbosity(config, new TestLogger());

            // Assert
            Assert.AreEqual(LoggerVerbosity.Debug, verbosity);
        }

        [TestMethod]
        public void FromAnalysisConfig_LogLevel()
        {
            // Arrange
            AnalysisConfig config = new AnalysisConfig();
            config.AdditionalSettings = new List<AnalysisSetting>();
            config.AdditionalSettings.Add(new AnalysisSetting() { Id = SonarProperties.LogLevel, Value = "DEBUG|INFO", Inherited = false });

            // Act
            var verbosity = VerbosityCalculator.ComputeVerbosity(config, new TestLogger());

            // Assert
            Assert.AreEqual(LoggerVerbosity.Debug, verbosity);
        }

        [TestMethod]
        public void FromAnalysisConfig_NoSetting()
        {
            // Arrange
            AnalysisConfig config = new AnalysisConfig();
            config.AdditionalSettings = new List<AnalysisSetting>();

            // Act
            var verbosity = VerbosityCalculator.ComputeVerbosity(config, new TestLogger());

            // Assert
            Assert.AreEqual(LoggerVerbosity.Info, verbosity);
        }
    }
}
