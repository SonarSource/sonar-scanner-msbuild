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

using NSubstitute.Extensions;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class TFSProcessorWrapperTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Execute_WhenConfigIsNull_Throws()
    {
        var testSubject = new TfsProcessorWrapper(new TestLogger(), Substitute.For<OperatingSystemProvider>(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()));
        Action act = () => testSubject.Execute(null, [], string.Empty);

        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
    }

    [TestMethod]
    public void Execute_WhenUserCmdLineArgumentsIsNull_Throws()
    {
        var testSubject = new TfsProcessorWrapper(new TestLogger(), Substitute.For<OperatingSystemProvider>(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()));
        Action act = () => testSubject.Execute(new AnalysisConfig(), null, string.Empty);

        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("userCmdLineArguments");
    }

    [TestMethod]
    public void Execute_ReturnTrue()
    {
        var testSubject = Substitute.ForPartsOf<TfsProcessorWrapper>(new TestLogger(), Substitute.For<OperatingSystemProvider>(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()));
        testSubject
            .Configure()
            .ExecuteProcessorRunner(Arg.Any<AnalysisConfig>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<string>(), Arg.Any<IProcessRunner>())
            .Returns(true);
        var result = testSubject.Execute(new AnalysisConfig(), [], "some/path");

        result.Should().BeTrue();
    }

    [TestMethod]
    public void Ctor_WhenLoggerIsNull_Throws()
    {
        Action act = () => _ = new TfsProcessorWrapper(null, Substitute.For<OperatingSystemProvider>(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()));

        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void Ctor_WhenOperatingSystemProviderIsNull_Throws()
    {
        Action act = () => _ = new TfsProcessorWrapper(new TestLogger(), null);

        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("operatingSystemProvider");
    }

    [TestMethod]
    public void TfsProcessor_StandardAdditionalArgumentsPassed()
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig { SonarScannerWorkingDirectory = "c:\\work" };

        var success = ExecuteTFSProcessorIgnoringAsserts(config, [], logger, "c:\\exe.Path", "d:\\propertiesFile.Path", mockRunner);

        VerifyProcessRunOutcome(mockRunner, logger, "c:\\work", success, true);
    }

    [TestMethod]
    public void TfsProcessor_CmdLineArgsOrdering()
    {
        // Check that user arguments are passed through to the wrapper and that they appear first
        var logger = new TestLogger();
        var args = new[] { "ConvertCoverage", "d:\\propertiesFile.Path" };

        var mockRunner = new MockProcessRunner(executeResult: true);

        var success = ExecuteTFSProcessorIgnoringAsserts(
            new AnalysisConfig { SonarScannerWorkingDirectory = "D:\\dummyWorkingDirectory" },
            args,
            logger,
            "c:\\dummy.exe",
            "c:\\foo.properties",
            mockRunner);

        VerifyProcessRunOutcome(mockRunner, logger, "D:\\dummyWorkingDirectory", success, true);

        CheckArgExists("ConvertCoverage", mockRunner);
        CheckArgExists("d:\\propertiesFile.Path", mockRunner);
    }

    [TestMethod]
    public void WrapperError_Success_NoStdErr() =>
        TestWrapperErrorHandling(executeResult: true, addMessageToStdErr: false, expectedOutcome: true);

    [TestMethod]
    public void WrapperError_Success_StdErr() =>
        TestWrapperErrorHandling(executeResult: true, addMessageToStdErr: true, expectedOutcome: true);

    [TestMethod]
    public void WrapperError_Fail_NoStdErr() =>
        TestWrapperErrorHandling(executeResult: false, addMessageToStdErr: false, expectedOutcome: false);

    [TestMethod]
    public void WrapperError_Fail_StdErr() =>
        TestWrapperErrorHandling(executeResult: false, addMessageToStdErr: true, expectedOutcome: false);

    private static void TestWrapperErrorHandling(bool executeResult, bool addMessageToStdErr, bool expectedOutcome)
    {
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult);

        var config = new AnalysisConfig { SonarScannerWorkingDirectory = "C:\\working" };

        if (addMessageToStdErr)
        {
            logger.LogError("Dummy error");
        }

        var success = ExecuteTFSProcessorIgnoringAsserts(config, [], logger, "c:\\bar.exe", "c:\\props.xml", mockRunner);

        VerifyProcessRunOutcome(mockRunner, logger, "C:\\working", success, expectedOutcome);
    }

    private static bool ExecuteTFSProcessorIgnoringAsserts(
        AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger, string exeFileName, string propertiesFileName, IProcessRunner runner)
    {
        using (new AssertIgnoreScope())
        {
            var wrapper = new TfsProcessorWrapper(logger, Substitute.For<OperatingSystemProvider>(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()));
            return wrapper.ExecuteProcessorRunner(config, exeFileName, userCmdLineArguments, propertiesFileName, runner);
        }
    }

    private static void VerifyProcessRunOutcome(MockProcessRunner mockRunner, TestLogger testLogger, string expectedWorkingDir, bool actualOutcome, bool expectedOutcome)
    {
        actualOutcome.Should().Be(expectedOutcome);

        mockRunner.SuppliedArguments.WorkingDirectory.Should().Be(expectedWorkingDir);

        if (actualOutcome)
        {
            // Errors can still be logged when the process completes successfully, so
            // we don't check the error log in this case
            testLogger.AssertInfoMessageExists(Resources.MSG_TFSProcessorCompleted);
        }
        else
        {
            testLogger.AssertErrorsLogged();
            testLogger.AssertErrorLogged(Resources.ERR_TFSProcessorExecutionFailed);
        }
    }

    /// <summary>
    /// Checks that the argument exists, and returns the start position of the argument in the list of
    /// concatenated arguments so we can check that the arguments are passed in the correct order.
    /// </summary>
    private int CheckArgExists(string expectedArg, MockProcessRunner mockRunner)
    {
        var allArgs = string.Join(" ", mockRunner.SuppliedArguments.CmdLineArgs);
        var index = allArgs.IndexOf(expectedArg);
        index.Should().BeGreaterThan(-1, "Expected argument was not found. Arg: '{0}', all args: '{1}'", expectedArg, allArgs);
        return index;
    }
}
