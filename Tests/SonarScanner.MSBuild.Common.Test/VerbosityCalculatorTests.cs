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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class VerbosityCalculatorTests
{
    // Looks at how verbosity is computed when various combinations of <<verbose>> and <<log.level>> values are passed in.
    // <<verbose>> takes precedence over <<log.level>>.
    // verbose valid values are (case-insesitive) 'true' and 'false' and log.level can be (case-insesitive) 'DEBUG' or a combination of values separtated by '|'")]
    [TestMethod]
    public void FromAnalysisProvider_Precedence()
    {
        CheckVerbosity(VerbosityCalculator.DefaultLoggingVerbosity);
        CheckVerbosity(VerbosityCalculator.DefaultLoggingVerbosity, null, "trace");
        CheckVerbosity(LoggerVerbosity.Debug, null, "debug", "sonar.log.level=debug was specified - setting the log verbosity to 'DEBUG'");
        CheckVerbosity(LoggerVerbosity.Debug, null, "info|debug", "sonar.log.level=info|debug was specified - setting the log verbosity to 'DEBUG'");
        CheckVerbosity(LoggerVerbosity.Debug, "TRUE", null, "sonar.verbose=True was specified - setting the log verbosity to 'Debug'");
        CheckVerbosity(LoggerVerbosity.Debug, "True", null, "sonar.verbose=True was specified - setting the log verbosity to 'Debug'");
        CheckVerbosity(VerbosityCalculator.DefaultLoggingVerbosity, null, "***DEBUG***");
        CheckVerbosity(VerbosityCalculator.DefaultLoggingVerbosity, null, "|DEBUG***");
        CheckVerbosity(VerbosityCalculator.DefaultLoggingVerbosity, null, "||");

        CheckVerbosity(LoggerVerbosity.Debug, "true", null, "sonar.verbose=True was specified - setting the log verbosity to 'Debug'");
        CheckVerbosity(LoggerVerbosity.Info, "false", null, "sonar.verbose=False was specified - setting the log verbosity to 'Info'");
        CheckVerbosity(LoggerVerbosity.Debug, null, "DEBUG", "sonar.log.level=DEBUG was specified - setting the log verbosity to 'DEBUG'");
        CheckVerbosity(LoggerVerbosity.Debug, null, "INFO|DEBUG|TRACE", "sonar.log.level=INFO|DEBUG|TRACE was specified - setting the log verbosity to 'DEBUG'");

        CheckVerbosity(LoggerVerbosity.Debug, "true", "INFO", "sonar.verbose=True was specified - setting the log verbosity to 'Debug'");
        CheckVerbosity(LoggerVerbosity.Info, "false", "DEBUG", "sonar.verbose=False was specified - setting the log verbosity to 'Info'");

        CheckVerbosity(LoggerVerbosity.Info, "SomeWrongVerbosity", null, null, "Expecting the sonar.verbose property to be set to either 'true' or 'false' but it was set to 'SomeWrongVerbosity'.");
        CheckVerbosity(
            LoggerVerbosity.Debug,
            "true",
            "DEBUG",
            "sonar.verbose=True was specified - setting the log verbosity to 'Debug'");
    }

    private static void CheckVerbosity(LoggerVerbosity expectedVerbosity, string verbosity = null, string logLevel = null, string expectedDebug = null, string expectedWarning = null)
    {
        var provider = CreatePropertiesProvider(verbosity, logLevel);
        var logger = new TestLogger();

        var actualVerbosity = VerbosityCalculator.ComputeVerbosity(provider, logger);

        actualVerbosity.Should().Be(expectedVerbosity);

        logger.Should().HaveNoErrors();

        if (expectedDebug is null)
        {
            logger.Should().HaveNoDebugs();
        }
        else
        {
            logger.Should().HaveDebugOnce(expectedDebug);
        }
        if (expectedWarning is null)
        {
            logger.Should().HaveNoWarnings();
        }
        else
        {
            logger.Should().HaveWarningOnce(expectedWarning);
        }
    }

    private static ListPropertiesProvider CreatePropertiesProvider(string verbosity, string logLevel)
    {
        var propertyProvider = new ListPropertiesProvider();
        if (verbosity is not null)
        {
            propertyProvider.AddProperty(SonarProperties.Verbose, verbosity);
        }
        if (logLevel is not null)
        {
            propertyProvider.AddProperty(SonarProperties.LogLevel, logLevel);
        }

        return propertyProvider;
    }
}
