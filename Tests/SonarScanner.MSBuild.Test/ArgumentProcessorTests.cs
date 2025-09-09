/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

namespace SonarScanner.MSBuild.Test;

[TestClass]
public class ArgumentProcessorTests
{
    private const string ValidUrl = "/d:sonar.host.url=http://foo";

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize() =>
        // The project setup means the default properties file will automatically
        // be copied alongside the product binaries.st of these tests assume
        // the default properties file does not exist so we'll ensure it doesn't.
        // Any tests that do require default properties file should re-create it
        // with known content.
        TestUtils.EnsureDefaultPropertiesFileDoesNotExist();

    [TestMethod]
    public void TryProcessArgs_WhenCommandLineArgsIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ArgumentProcessor.TryProcessArgs(null, new TestLogger(), out var _)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("commandLineArgs");

    [TestMethod]
    public void TryProcessArgs_WhenLoggerIsNull_ThrowsArgumentNullException() =>
        FluentActions.Invoking(() => ArgumentProcessor.TryProcessArgs([], null, out var _)).Should().ThrowExactly<ArgumentNullException>().WithParameterName("logger");

    [TestMethod]
    public void ArgProc_Help()
    {
        ArgumentProcessor.IsHelp(["/other", "-other"]).Should().BeFalse();

        ArgumentProcessor.IsHelp([]).Should().BeTrue();

        ArgumentProcessor.IsHelp(["/?", "/other"]).Should().BeTrue();
        ArgumentProcessor.IsHelp(["-?", "-other"]).Should().BeTrue();

        ArgumentProcessor.IsHelp(["/h", "/other"]).Should().BeTrue();
        ArgumentProcessor.IsHelp(["-h", "-other"]).Should().BeTrue();

        ArgumentProcessor.IsHelp(["/help", "/other"]).Should().BeTrue();
        ArgumentProcessor.IsHelp(["-help", "-other"]).Should().BeTrue();
    }

    [TestMethod]
    public void ArgProc_UnrecognizedArgumentsAreIgnored()
    {
        var logger = new TestLogger();

        // 1. Minimal command line settings with extra values
        var settings = CheckProcessingSucceeds(logger, "begin", "/d:sonar.host.url=foo", "foo", "blah", "/xxxx");
        AssertUrlAndChildCmdLineArgs(settings, "/d:sonar.host.url=foo", "foo", "blah", "/xxxx");
    }

    [TestMethod]
    public void ArgProc_StripVerbsAndPrefixes()
    {
        var logger = new TestLogger();

        var settings = CheckProcessingSucceeds(logger, "begin", "/d:sonar.host.url=foo", "/begin:true", "/install:true");
        AssertUrlAndChildCmdLineArgs(settings, "/d:sonar.host.url=foo", "/begin:true", "/install:true");

        settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "begin", "/installXXX:true");
        AssertUrlAndChildCmdLineArgs(settings, "/d:sonar.host.url=foo", "/installXXX:true");
    }

    [TestMethod]
    public void ArgProc_PropertyOverriding()
    {
        // Command line properties should take precedence
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "settings");
        var fullPropertiesPath = Path.Combine(testDir, "settings.txt");
        var properties = new AnalysisProperties { new(SonarProperties.Verbose, "true") };
        properties.Save(fullPropertiesPath);

        var logger = new TestLogger();

        // 1. Settings file only
        var settings = CheckProcessingSucceeds(logger, "begin", "/s: " + fullPropertiesPath);
        LoggerVerbosity.Debug.Should().Be(settings.LoggingVerbosity);

        // 2. Both file and cmd line
        settings = CheckProcessingSucceeds(logger, "begin", "/s: " + fullPropertiesPath, "/d:sonar.verbose=false");
        LoggerVerbosity.Info.Should().Be(settings.LoggingVerbosity);

        // 3. Cmd line only
        settings = CheckProcessingSucceeds(logger, "begin", "/d:sonar.verbose=false", "/d:other=property", "/d:a=b c");
        LoggerVerbosity.Info.Should().Be(settings.LoggingVerbosity); // cmd line wins
    }

    [TestMethod]
    public void ArgProc_InvalidCmdLineProperties()
    {
        // Incorrectly formed /d:[key]=[value] arguments
        TestLogger logger;

        logger = CheckProcessingFails(
            "/d:sonar.host.url=foo",
            "/d: key1=space before",
            "/d:key2 = space after)");

        logger.Should().HaveErrors(
            "The format of the analysis property  key1=space before is invalid",
            "The format of the analysis property key2 = space after) is invalid");
    }

    [TestMethod]
    public void ArgProc_WithDashedArguments_Long()
    {
        // Incorrectly formed /d:[key]=[value] arguments
        var logger = new TestLogger();

        var arguments = "-d:sonar.host.url=http://foo -version:1.2 -organization:123456789 -key:gggzzz -login:ddddd";

        var settings = CheckProcessingSucceeds(logger, arguments, "begin");

        AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
        logger.Should().HaveNoWarnings();
        AssertExpectedChildArguments(settings, arguments);
    }

    [TestMethod]
    public void ArgProc_WithDashedArguments_Short()
    {
        // Incorrectly formed /d:[key]=[value] arguments
        var logger = new TestLogger();

        var arguments = "-d:sonar.host.url=http://foo -v:1.2 -k:123456789";

        var settings = CheckProcessingSucceeds(logger, arguments, "begin");

        AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
        logger.Should().HaveNoWarnings();
        AssertExpectedChildArguments(settings, arguments);
    }

    [TestMethod]
    public void ArgProc_BeginVerb()
    {
        var logger = new TestLogger();

        // 1. Minimal parameters -> valid
        var settings = CheckProcessingSucceeds(logger, ValidUrl, "begin");
        AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
        logger.Should().HaveNoWarnings();
        AssertExpectedChildArguments(settings, ValidUrl);

        // 2. With additional parameters -> valid
        logger = new();
        settings = CheckProcessingSucceeds(logger, ValidUrl, "begin", "ignored", "k=2");
        AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
        logger.Should().HaveNoWarnings();
        AssertExpectedChildArguments(settings, ValidUrl, "ignored", "k=2");

        // 3. Multiple occurrences -> error
        logger = CheckProcessingFails(ValidUrl, "begin", "begin");
        logger.Should().HaveErrorOnce("A value has already been supplied for this argument: begin. Existing: ''");

        // 4. Missing -> invalid (missing verb)
        logger = CheckProcessingFails(ValidUrl);
        logger.Should().HaveErrors(Resources.ERROR_CmdLine_NeitherBeginNorEndSupplied);

        // 5. Incorrect case -> treated as unrecognized argument -> invalid (missing verb)
        logger = CheckProcessingFails(ValidUrl, "BEGIN");
        logger.Should().HaveErrors(Resources.ERROR_CmdLine_NeitherBeginNorEndSupplied);
    }

    [TestMethod]
    public void ArgProc_BeginVerb_MatchesOnlyCompleteWord()
    {
        var logger = new TestLogger();

        // 1. "beginx" -> invalid (missing verb)
        CheckProcessingFails("beginX");

        // 2. "begin", "beginx" should not be treated as duplicates
        var settings = CheckProcessingSucceeds(logger, ValidUrl, "begin", "beginX");
        AssertExpectedPhase(AnalysisPhase.PreProcessing, settings);
        logger.Should().HaveNoWarnings();
        AssertExpectedChildArguments(settings, ValidUrl, "beginX");
    }

    [TestMethod]
    public void ArgProc_EndVerb()
    {
        var logger = new TestLogger();

        // 1. Minimal parameters -> valid
        var settings = CheckProcessingSucceeds(logger, "end");
        AssertExpectedPhase(AnalysisPhase.PostProcessing, settings);
        AssertExpectedChildArguments(settings);

        // 2. With additional parameters -> valid
        logger = new TestLogger();
        settings = CheckProcessingSucceeds(logger, "end", "ignored", "/d:key=value");
        AssertExpectedPhase(AnalysisPhase.PostProcessing, settings);
        logger.Should().HaveNoWarnings();
        AssertExpectedChildArguments(settings, "ignored", "/d:key=value");

        // 3. Multiple occurrences -> invalid (duplicated argument)
        logger = CheckProcessingFails(ValidUrl, "end", "end");
        logger.Errors.Should().ContainSingle();

        // 4. Missing, no other arguments -> invalid (missing verb)
        logger = CheckProcessingFails();
        logger.Should().HaveErrors(Resources.ERROR_CmdLine_NeitherBeginNorEndSupplied);

        // 5. Partial match -> unrecognized -> invalid (missing verb)
        logger = CheckProcessingFails("endx");
        logger.Should().HaveErrors(Resources.ERROR_CmdLine_NeitherBeginNorEndSupplied);
    }

    [TestMethod]
    public void ArgProc_EndVerb_MatchesOnlyCompleteWord()
    {
        var logger = new TestLogger();

        // "end", "endx" should not be treated as duplicates
        var settings = CheckProcessingSucceeds(logger, "end", "endX", "endXXX");

        AssertExpectedPhase(AnalysisPhase.PostProcessing, settings);
        logger.Should().HaveNoWarnings();
        AssertExpectedChildArguments(settings, "endX", "endXXX");
    }

    [TestMethod]
    public void ArgProc_BeginAndEndVerbs()
    {
        // 1. Both present
        var logger = CheckProcessingFails(ValidUrl, "begin", "end");
        logger.Should().HaveErrors(1)
            .And.HaveErrors("Invalid command line parameters. Please specify either 'begin' or 'end', not both.");
    }

    [TestMethod]
    public void ArgProc_SonarVerbose_IsBool()
    {
        var logger = new TestLogger();

        var settings = CheckProcessingSucceeds(logger, "/d:sonar.host.url=foo", "begin", "/d:sonar.verbose=yes");
        settings.LoggingVerbosity.Should().Be(VerbosityCalculator.DefaultLoggingVerbosity, "Only expecting true or false");

        logger.Should().HaveNoErrors()
            .And.HaveWarningOnce("Expecting the sonar.verbose property to be set to either 'true' or 'false' (case-sensitive) but it was set to 'yes'.");
    }

    [TestMethod]
    public void ArgProc_SonarVerbose_CmdAndFile()
    {
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "settings");
        var fullPropertiesPath = Path.Combine(testDir, "settings.txt");
        var properties = new AnalysisProperties
        {
            new(SonarProperties.HostUrl, "http://settingsFile"),
            new(SonarProperties.LogLevel, "INFO|DEBUG")
        };
        properties.Save(fullPropertiesPath);

        var logger = new TestLogger();

        var settings = CheckProcessingSucceeds(logger, "begin", "/s: " + fullPropertiesPath);
        settings.ChildCmdLineArgs.Should().Contain("/s: " + fullPropertiesPath);
        settings.LoggingVerbosity.Should().Be(LoggerVerbosity.Debug);

        settings = CheckProcessingSucceeds(logger, "begin", "/s: " + fullPropertiesPath, "/d:sonar.verbose=false");
        settings.LoggingVerbosity.Should().Be(LoggerVerbosity.Info, "sonar.verbose takes precedence");
    }

    private static IBootstrapperSettings CheckProcessingSucceeds(TestLogger logger, params string[] cmdLineArgs)
    {
        var success = ArgumentProcessor.TryProcessArgs(cmdLineArgs, logger, out var settings);

        success.Should().BeTrue("Expecting processing to succeed");
        settings.Should().NotBeNull("Settings should not be null if processing succeeds");
        logger.Should().HaveNoErrors();

        return settings;
    }

    private static TestLogger CheckProcessingFails(params string[] cmdLineArgs)
    {
        var logger = new TestLogger();
        var success = ArgumentProcessor.TryProcessArgs(cmdLineArgs, logger, out var settings);

        success.Should().BeFalse("Expecting processing to fail");
        settings.Should().BeNull("Settings should be null if processing fails");
        logger.Should().HaveErrors();

        return logger;
    }

    private static void AssertUrlAndChildCmdLineArgs(IBootstrapperSettings settings, params string[] expectedCmdLineArgs) =>
        settings.ChildCmdLineArgs.Should().BeEquivalentTo(expectedCmdLineArgs, "Unexpected child command line arguments");

    private static void AssertExpectedPhase(AnalysisPhase expected, IBootstrapperSettings settings) =>
        settings.Phase.Should().Be(expected, "Unexpected analysis phase");

    private static void AssertExpectedChildArguments(IBootstrapperSettings actualSettings, params string[] expected) =>
        actualSettings.ChildCmdLineArgs.Should().BeEquivalentTo(expected, "Unexpected child command line arguments");
}
