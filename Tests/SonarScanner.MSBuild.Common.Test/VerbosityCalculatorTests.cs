/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
 * mailto: info AT sonarsource DOT com
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

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Common.Test;

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
        CheckVerbosity("Verbosity settings are case-sensitive", VerbosityCalculator.DefaultLoggingVerbosity, null, "debug");
        CheckVerbosity("Verbosity settings are case-sensitive", VerbosityCalculator.DefaultLoggingVerbosity, null, "info|debug");
        CheckVerbosity("Verbosity settings are case-sensitive", VerbosityCalculator.DefaultLoggingVerbosity, "TRUE", null, 1);
        CheckVerbosity("Verbosity settings are case-sensitive", VerbosityCalculator.DefaultLoggingVerbosity, "True", null, 1);
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
        var logger = new TestLogger();

        var actualVerbosity = VerbosityCalculator.ComputeVerbosity(provider, logger);

        actualVerbosity.Should().Be(expectedVerbosity, errorMessage);

        logger.Should().HaveNoErrors();
        logger.Should().HaveWarnings(expectedNumberOfWarnings);
    }

    private static ListPropertiesProvider CreatePropertiesProvider(string verbosity, string logLevel)
    {
        var propertyProvider = new ListPropertiesProvider();
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
