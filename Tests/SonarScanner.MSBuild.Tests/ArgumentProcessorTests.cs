/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

using System;
using System.IO;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class ArgumentProcessorTests
    {
        [TestInitialize]
        public void Initialize()
        {
            // The project setup means the default properties file will automatically
            // be copied alongside the product binaries.st of these tests assume
            // the default properties file does not exist so we'll ensure it doesn't.
            // Any tests that do require default properties file should re-create it
            // with known content.
            BootstrapperTestUtils.EnsureDefaultPropertiesFileDoesNotExist();
        }

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void TryProcessArgs_WhenCommandLineArgsIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ArgumentProcessor.TryProcessArgs(null, new TestLogger(), out var settings);

            // Act & Assert
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("commandLineArgs");
        }

        [TestMethod]
        public void TryProcessArgs_WhenLoggerIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            Action action = () => ArgumentProcessor.TryProcessArgs(new string[0], null, out var settings);

            // Act & Assert
            action.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void ArgProc_Help()
        {
            ArgumentProcessor.IsHelp(new string[] { "sad", "/d:s=r", "/h" }).Should().BeTrue();
            ArgumentProcessor.IsHelp(new string[] { "sad", "/d:s=r", "/hr" }).Should().BeFalse();

            ArgumentProcessor.IsHelp(new string[] { "sad", "/d:s=r", "/?" }).Should().BeTrue();
            ArgumentProcessor.IsHelp(new string[] { "sad", "/d:s=r", "/??" }).Should().BeFalse();
        }

        [TestMethod]
        public void ArgProc_UnrecognizedArgumentsAreIgnored()
        {
            var logger = new TestLogger();

            // 1. Minimal command line settings with extra values
            var settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "foo", "blah", "/xxxx");
            AssertUrlAndChildCmdLineArgs(settings, "/d:sonar.host.url=foo", "foo", "blah", "/xxxx");
        }

        [TestMethod]
        public void ArgProc_StripVerbsAndPrefixes()
        {
            var logger = new TestLogger();

            var settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "/begin:true", "/install:true");
            AssertUrlAndChildCmdLineArgs(settings, "/d:sonar.host.url=foo", "/begin:true", "/install:true");

            settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "begin", "/installXXX:true");
            AssertUrlAndChildCmdLineArgs(settings, "/d:sonar.host.url=foo", "/installXXX:true");
        }

        [TestMethod]
        public void ArgProc_PropertyOverriding()
        {
            // Command line properties should take precedence

            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "settings");
            var fullPropertiesPath = Path.Combine(testDir, "settings.txt");
            var properties = new AnalysisProperties
            {
                new Property() { Id = SonarProperties.Verbose, Value = "true" }
            };
            properties.Save(fullPropertiesPath);

            var logger = new TestLogger();

            // 1. Settings file only
            var settings = CheckProcessingSucceeds(logger, "/s: " + fullPropertiesPath);
             LoggerVerbosity.Debug.Should().Be(settings.LoggingVerbosity);

            //// 2. Both file and cmd line
            settings = CheckProcessingSucceeds(logger, "/s: " + fullPropertiesPath, "/d:sonar.verbose=false");
             LoggerVerbosity.Info.Should().Be(settings.LoggingVerbosity);

            //// 3. Cmd line only
            settings = CheckProcessingSucceeds(logger, "/d:sonar.verbose=false", "/d:other=property", "/d:a=b c");
             LoggerVerbosity.Info.Should().Be(settings.LoggingVerbosity); // cmd line wins
        }

        [TestMethod]
        public void ArgProc_InvalidCmdLineProperties()
        {
            // Incorrectly formed /d:[key]=[value] arguments
            TestLogger logger;

            logger = CheckProcessingFails("/d:sonar.host.url=foo",
                "/d: key1=space before",
                "/d:key2 = space after)");

            logger.AssertSingleErrorExists(" key1");
            logger.AssertSingleErrorExists("key2 ");
        }

        [TestMethod]
        public void ArgProc_WithDashedArguments_Long()
        {
            // Incorrectly formed /d:[key]=[value] arguments
            TestLogger logger = new TestLogger();

            var arguments = "-d:sonar.host.url=http://foo -version:1.2 -organization:123456789 -key:gggzzz -login:ddddd";

            var settings = CheckProcessingSucceeds(logger, arguments, "begin");

            AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
            logger.AssertWarningsLogged(0);
            AssertExpectedChildArguments(settings, arguments);
        }

        [TestMethod]
        public void ArgProc_WithDashedArguments_Short()
        {
            // Incorrectly formed /d:[key]=[value] arguments
            TestLogger logger= new TestLogger();

            var arguments = "-d:sonar.host.url=http://foo -v:1.2 -k:123456789";

            var settings = CheckProcessingSucceeds(logger, arguments, "begin");

            AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
            logger.AssertWarningsLogged(0);
            AssertExpectedChildArguments(settings, arguments);
        }

        [TestMethod]
        public void ArgProc_BeginVerb()
        {
            // Arrange
            var logger = new TestLogger();
            var validUrl = "/d:sonar.host.url=http://foo";

            // 1. Minimal parameters -> valid
            var settings = CheckProcessingSucceeds(logger, validUrl, "begin");
            AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
            logger.AssertWarningsLogged(0);
            AssertExpectedChildArguments(settings, validUrl);

            // 2. With additional parameters -> valid
            settings = CheckProcessingSucceeds(logger, validUrl, "begin", "ignored", "k=2");
            AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
            logger.AssertWarningsLogged(0);
            AssertExpectedChildArguments(settings, validUrl, "ignored", "k=2");

            // 3. Multiple occurrences -> error
            logger = CheckProcessingFails(validUrl, "begin", "begin");
            logger.AssertSingleErrorExists(ArgumentProcessor.BeginVerb);

            // 4. Missing -> valid with warning
            logger = new TestLogger();
            settings = CheckProcessingSucceeds(logger, validUrl);
            logger.AssertSingleWarningExists(ArgumentProcessor.BeginVerb);
            AssertExpectedChildArguments(settings, validUrl);

            // 5. Incorrect case -> treated as unrecognized argument
            // -> valid with 1 warning (no begin / end specified warning)
            logger = new TestLogger();
            settings = CheckProcessingSucceeds(logger, validUrl, "BEGIN"); // wrong case
            logger.AssertWarningsLogged(1);
            logger.AssertSingleWarningExists(ArgumentProcessor.BeginVerb);
            AssertExpectedChildArguments(settings, validUrl, "BEGIN");
        }

        [TestMethod]
        public void ArgProc_BeginVerb_MatchesOnlyCompleteWord()
        {
            // Arrange
            TestLogger logger;
            var validUrl = "/d:sonar.host.url=http://foo";

            // 1. "beginx" -> valid, child argument "beginx"
            logger = new TestLogger();
            var settings = CheckProcessingSucceeds(logger, validUrl, "beginX");
            AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
            logger.AssertWarningsLogged(1); // Expecting a warning because "beginX" should not be recognized as "begin"
            AssertExpectedChildArguments(settings, validUrl, "beginX");

            // 2. "begin", "beginx" should not be treated as duplicates
            logger = new TestLogger();
            settings = CheckProcessingSucceeds(logger, validUrl, "begin", "beginX");
            AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
            logger.AssertWarningsLogged(0);
            AssertExpectedChildArguments(settings, validUrl, "beginX");
        }

        [TestMethod]
        public void ArgProc_EndVerb()
        {
            // Arrange
            var logger = new TestLogger();
            var validUrl = "/d:sonar.host.url=http://foo";

            // 1. Minimal parameters -> valid
            var settings = CheckProcessingSucceeds(logger, "end");
            AssertExpectedPhase(AnalysisPhase.PostProcessing, settings);
            AssertExpectedChildArguments(settings);

            // 2. With additional parameters -> valid
            logger = new TestLogger();
            settings = CheckProcessingSucceeds(logger, "end", "ignored", "/d:key=value");
            AssertExpectedPhase(AnalysisPhase.PostProcessing, settings);
            logger.AssertWarningsLogged(0);
            AssertExpectedChildArguments(settings, "ignored", "/d:key=value");

            // 3. Multiple occurrences -> invalid
            logger = CheckProcessingFails(validUrl, "end", "end");
            logger.AssertSingleErrorExists(ArgumentProcessor.EndVerb);

            // 4. Missing, no other arguments -> valid with warning
            logger = new TestLogger();
            settings = CheckProcessingSucceeds(logger);
            AssertExpectedPhase(AnalysisPhase.PostProcessing, settings);
            logger.AssertWarningsLogged(1);
            AssertExpectedChildArguments(settings);

            // 5. Partial match -> unrecognized -> treated as preprocessing
            logger = new TestLogger();
            settings = CheckProcessingSucceeds(logger, "endx");
            AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
            logger.AssertWarningsLogged(1);
        }

        [TestMethod]
        public void ArgProc_EndVerb_MatchesOnlyCompleteWord()
        {
            // Arrange
            TestLogger logger;
            IBootstrapperSettings settings;
            logger = new TestLogger();

            // Act
            // "end", "endx" should not be treated as duplicates
            settings = CheckProcessingSucceeds(logger, "end", "endX", "endXXX");

            // Assert
            AssertExpectedPhase(AnalysisPhase.PostProcessing, settings);
            logger.AssertWarningsLogged(0);
            AssertExpectedChildArguments(settings, "endX", "endXXX");
        }

        [TestMethod]
        public void ArgProc_BeginAndEndVerbs()
        {
            // 0. Setup
            TestLogger logger;
            var validUrl = "/d:sonar.host.url=http://foo";

            // 1. Both present
            logger = CheckProcessingFails(validUrl, "begin", "end");
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("begin", "end");
        }

        [TestMethod]
        public void ArgProc_SonarVerbose_IsBool()
        {
            var logger = new TestLogger();

            var settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "begin", "/d:sonar.verbose=yes");
            settings.LoggingVerbosity.Should().Be(VerbosityCalculator.DefaultLoggingVerbosity, "Only expecting true or false");

            logger.AssertErrorsLogged(0);
            logger.AssertSingleWarningExists("yes");
        }

        [TestMethod]
        public void ArgProc_SonarVerbose_CmdAndFile()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "settings");
            var fullPropertiesPath = Path.Combine(testDir, "settings.txt");
            var properties = new AnalysisProperties
            {
                new Property() { Id = SonarProperties.HostUrl, Value = "http://settingsFile" },
                new Property() { Id = SonarProperties.LogLevel, Value = "INFO|DEBUG" }
            };
            properties.Save(fullPropertiesPath);

            var logger = new TestLogger();

            var settings = CheckProcessingSucceeds(logger, "/s: " + fullPropertiesPath);
            settings.ChildCmdLineArgs.Should().Contain("/s: " + fullPropertiesPath);
            settings.LoggingVerbosity.Should().Be(LoggerVerbosity.Debug);

            settings = CheckProcessingSucceeds(logger, "/s: " + fullPropertiesPath, "/d:sonar.verbose=false");
            settings.LoggingVerbosity.Should().Be(LoggerVerbosity.Info, "sonar.verbose takes precedence");
        }

        #endregion Tests

        #region Checks

        private static IBootstrapperSettings CheckProcessingSucceeds(TestLogger logger, params string[] cmdLineArgs)
        {
            var success = ArgumentProcessor.TryProcessArgs(cmdLineArgs, logger, out IBootstrapperSettings settings);

            success.Should().BeTrue("Expecting processing to succeed");
            settings.Should().NotBeNull("Settings should not be null if processing succeeds");
            logger.AssertErrorsLogged(0);

            return settings;
        }

        private static TestLogger CheckProcessingFails(params string[] cmdLineArgs)
        {
            var logger = new TestLogger();
            var success = ArgumentProcessor.TryProcessArgs(cmdLineArgs, logger, out IBootstrapperSettings settings);

            success.Should().BeFalse("Expecting processing to fail");
            settings.Should().BeNull("Settings should be null if processing fails");
            logger.AssertErrorsLogged();

            return logger;
        }

        private static void AssertUrlAndChildCmdLineArgs(IBootstrapperSettings settings, params string[] expectedCmdLineArgs)
        {
            settings.ChildCmdLineArgs.Should().BeEquivalentTo(expectedCmdLineArgs, "Unexpected child command line arguments");
        }

        private static void AssertExpectedPhase(AnalysisPhase expected, IBootstrapperSettings settings)
        {
            settings.Phase.Should().Be(expected, "Unexpected analysis phase");
        }

        private static void AssertExpectedChildArguments(IBootstrapperSettings actualSettings, params string[] expected)
        {
            actualSettings.ChildCmdLineArgs.Should().BeEquivalentTo(expected, "Unexpected child command line arguments");
        }

        #endregion Checks
    }
}
