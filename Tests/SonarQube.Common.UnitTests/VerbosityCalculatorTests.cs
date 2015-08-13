//-----------------------------------------------------------------------
// <copyright file="ConsoleLoggerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
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

        [TestMethod]
        public void FromAnalysisProvider()
        {
            CheckVerbosity("Default verbosity does not match", VerbosityCalculator.DefaultLoggingVerbosity);
            CheckVerbosity("Trace and Debug are independent SonarQube verbosity settings. We only track Debug", VerbosityCalculator.DefaultLoggingVerbosity, "trace");

            CheckVerbosity("", LoggerVerbosity.Debug, "true");
            CheckVerbosity("", LoggerVerbosity.Debug, "TRue");
            CheckVerbosity("", LoggerVerbosity.Info, "false");
            CheckVerbosity("", LoggerVerbosity.Debug, "debug");
            CheckVerbosity("", LoggerVerbosity.Debug, "INFO|DEBUG|TRACE");
            CheckVerbosity("", LoggerVerbosity.Debug, "***debug***");

            CheckVerbosity("sonar.verbose takes precedence over sonar.log.level", LoggerVerbosity.Debug, "true", "INFO");
            CheckVerbosity("sonar.verbose takes precedence over sonar.log.level", LoggerVerbosity.Info, "FALSE", "DEBUG");
        }

        private static void CheckVerbosity(string errorMessage, LoggerVerbosity expectedVerbosity, string verbosity = null, string logLevel = null)
        {
            var provider = CreateProvider(verbosity, logLevel);
            TestLogger logger = new TestLogger();

            var actualVerbosity = VerbosityCalculator.ComputeVerbosity(provider, logger);

            Assert.AreEqual(expectedVerbosity, actualVerbosity, errorMessage);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(0);
        }

        private static ListPropertiesProvider CreateProvider(string verbosity, string logLevel)
        {
            ListPropertiesProvider propertyProvider = new ListPropertiesProvider();
            if (verbosity!=null)
            {
                propertyProvider.AddProperty(SonarProperties.Verbose, verbosity);
            }
            if (logLevel!=null)
            {
                propertyProvider.AddProperty(SonarProperties.LogLevel, logLevel);
            }

            return propertyProvider;
        }
    }
}