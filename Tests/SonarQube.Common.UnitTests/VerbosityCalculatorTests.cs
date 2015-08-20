//-----------------------------------------------------------------------
// <copyright file="VerbosityCalculatorTests.cs" company="SonarSource SA and Microsoft Corporation">
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
        #region Tests

        [TestMethod]
        [Description(@"Looks at how verbosity is computed when various combinations of <<verbose>> and <<log.level>> values are passed in. <<verbose>> takes precedence over <<log.level>>.
            verbose valid values are 'true' and 'false' and log.level can be 'DEBUG' or a combination of values separtated by '|'")]
        public void FromAnalysisProvider_Precedence()
        {
            CheckVerbosity("Default verbosity does not match", VerbosityCalculator.DefaultLoggingVerbosity);
            CheckVerbosity("Trace and Debug are independent SonarQube verbosity settings. We only track Debug", VerbosityCalculator.DefaultLoggingVerbosity, null, "trace");
            CheckVerbosity("Verbosity settings are case insensitive", VerbosityCalculator.DefaultLoggingVerbosity, null, "debug");
            CheckVerbosity("Verbosity settings are case insensitive", VerbosityCalculator.DefaultLoggingVerbosity, null, "info|debug");
            CheckVerbosity("Verbosity settings are case insensitive", VerbosityCalculator.DefaultLoggingVerbosity, "TRUE", null, 1);
            CheckVerbosity("Verbosity settings are case insensitive", VerbosityCalculator.DefaultLoggingVerbosity, "True", null, 1);
            CheckVerbosity("", VerbosityCalculator.DefaultLoggingVerbosity, null, "***DEBUG***");
            CheckVerbosity("", VerbosityCalculator.DefaultLoggingVerbosity, null, "|DEBUG***");
            CheckVerbosity("", VerbosityCalculator.DefaultLoggingVerbosity, null, "||");

            CheckVerbosity("", LoggerVerbosity.Debug, "true");
            CheckVerbosity("", LoggerVerbosity.Info, "false");
            CheckVerbosity("", LoggerVerbosity.Debug, null, "DEBUG");
            CheckVerbosity("", LoggerVerbosity.Debug, null, "INFO|DEBUG|TRACE");
            

            CheckVerbosity("sonar.verbose takes precedence over sonar.log.level", LoggerVerbosity.Debug, "true", "INFO");
            CheckVerbosity("sonar.verbose takes precedence over sonar.log.level", LoggerVerbosity.Info, "false", "DEBUG");
        }

        #endregion Tests

        private static void CheckVerbosity(string errorMessage, LoggerVerbosity expectedVerbosity, string verbosity = null, string logLevel = null, int expectedNumberOfWarnings = 0)
        {
            var provider = CreatePropertiesProvider(verbosity, logLevel);
            TestLogger logger = new TestLogger();

            var actualVerbosity = VerbosityCalculator.ComputeVerbosity(provider, logger);

            Assert.AreEqual(expectedVerbosity, actualVerbosity, errorMessage);

            logger.AssertErrorsLogged(0);
            logger.AssertWarningsLogged(expectedNumberOfWarnings);
        }

        private static ListPropertiesProvider CreatePropertiesProvider(string verbosity, string logLevel)
        {
            ListPropertiesProvider propertyProvider = new ListPropertiesProvider();
            if (verbosity != null)
            {
                propertyProvider.AddProperty(SonarProperties.Verbose, verbosity);
            }
            if (logLevel != null)
            {
                propertyProvider.AddProperty(SonarProperties.LogLevel, logLevel);
            }

            return propertyProvider;
        }
    }
}