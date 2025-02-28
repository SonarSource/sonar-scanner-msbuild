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

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.Extensions;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.Shim.Test;

[TestClass]
public class TFSProcessorWrapperTest
{

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Execute_WhenConfigIsNull_Throws()
    {
        // Arrange
        var testSubject = new TfsProcessorWrapper(new TestLogger(), Substitute.For<IOperatingSystemProvider>());
        Action act = () => testSubject.Execute(null, new string[] { }, String.Empty);

        // Act & Assert
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("config");
    }

    [TestMethod]
    public void Execute_WhenUserCmdLineArgumentsIsNull_Throws()
    {
        // Arrange
        var testSubject = new TfsProcessorWrapper(new TestLogger(), Substitute.For<IOperatingSystemProvider>());
        Action act = () => testSubject.Execute(new AnalysisConfig(), null, String.Empty);

        // Act & Assert
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("userCmdLineArguments");
    }

    [TestMethod]
    public void Execute_ReturnTrue()
    {
        var testSubject = Substitute.ForPartsOf<TfsProcessorWrapper>(new TestLogger(), Substitute.For<IOperatingSystemProvider>());
        testSubject
            .Configure()
            .ExecuteProcessorRunner(Arg.Any<AnalysisConfig>(), Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<string>(), Arg.Any<IProcessRunner>())
            .Returns(true);
        var result = testSubject.Execute(new AnalysisConfig(), new List<string>(), "some/path");

        result.Should().BeTrue();
    }

    [TestMethod]
    public void Ctor_WhenLoggerIsNull_Throws()
    {
        // Arrange
        Action act = () => new TfsProcessorWrapper(null, Substitute.For<IOperatingSystemProvider>());

        // Act & Assert
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void Ctor_WhenOperatingSystemProviderIsNull_Throws()
    {
        // Arrange
        Action act = () => new TfsProcessorWrapper(new TestLogger(), null);

        // Act & Assert
        act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("operatingSystemProvider");
    }

    [TestMethod]
    public void TfsProcessor_StandardAdditionalArgumentsPassed()
    {
        // Arrange
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult: true);
        var config = new AnalysisConfig() { SonarScannerWorkingDirectory = "c:\\work" };

        // Act
        var success = ExecuteTFSProcessorIgnoringAsserts(config, Enumerable.Empty<string>(), logger, "c:\\exe.Path", "d:\\propertiesFile.Path", mockRunner);

        // Assert
        VerifyProcessRunOutcome(mockRunner, logger, "c:\\work", success, true);
    }

    [TestMethod]
    public void TfsProcessor_CmdLineArgsOrdering()
    {
        // Check that user arguments are passed through to the wrapper and that they appear first

        // Arrange
        var logger = new TestLogger();
        var args = new string[] { "ConvertCoverage", "d:\\propertiesFile.Path" };

        var mockRunner = new MockProcessRunner(executeResult: true);

        // Act
        var success = ExecuteTFSProcessorIgnoringAsserts(
            new AnalysisConfig() { SonarScannerWorkingDirectory = "D:\\dummyWorkingDirectory" },
            args,
            logger,
            "c:\\dummy.exe",
            "c:\\foo.properties",
            mockRunner);

        // Assert
        VerifyProcessRunOutcome(mockRunner, logger, "D:\\dummyWorkingDirectory", success, true);

        CheckArgExists("ConvertCoverage", mockRunner);
        CheckArgExists("d:\\propertiesFile.Path", mockRunner);
    }


    [TestMethod]
    public void WrapperError_Success_NoStdErr()
    {
        TestWrapperErrorHandling(executeResult: true, addMessageToStdErr: false, expectedOutcome: true);
    }

    [TestMethod]
    public void WrapperError_Success_StdErr()
    {
        TestWrapperErrorHandling(executeResult: true, addMessageToStdErr: true, expectedOutcome: true);
    }

    [TestMethod]
    public void WrapperError_Fail_NoStdErr()
    {
        TestWrapperErrorHandling(executeResult: false, addMessageToStdErr: false, expectedOutcome: false);
    }

    [TestMethod]
    public void WrapperError_Fail_StdErr()
    {
        TestWrapperErrorHandling(executeResult: false, addMessageToStdErr: true, expectedOutcome: false);
    }

    private void TestWrapperErrorHandling(bool executeResult, bool addMessageToStdErr, bool expectedOutcome)
    {
        // Arrange
        var logger = new TestLogger();
        var mockRunner = new MockProcessRunner(executeResult);

        var config = new AnalysisConfig() { SonarScannerWorkingDirectory = "C:\\working" };

        if (addMessageToStdErr)
        {
            logger.LogError("Dummy error");
        }

        // Act
        var success = ExecuteTFSProcessorIgnoringAsserts(config, Enumerable.Empty<string>(), logger, "c:\\bar.exe", "c:\\props.xml", mockRunner);

        // Assert
        VerifyProcessRunOutcome(mockRunner, logger, "C:\\working", success, expectedOutcome);
    }

    private static bool ExecuteTFSProcessorIgnoringAsserts(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger, string exeFileName, string propertiesFileName, IProcessRunner runner)
    {
        using (new AssertIgnoreScope())
        {
            var wrapper = new TfsProcessorWrapper(logger, Substitute.For<IOperatingSystemProvider>());
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
    /// concatenated arguments so we can check that the arguments are passed in the correct order
    /// </summary>
    private int CheckArgExists(string expectedArg, MockProcessRunner mockRunner)
    {
        var allArgs = string.Join(" ", mockRunner.SuppliedArguments.CmdLineArgs);
        var index = allArgs.IndexOf(expectedArg);
        index.Should().BeGreaterThan(-1, "Expected argument was not found. Arg: '{0}', all args: '{1}'", expectedArg, allArgs);
        return index;
    }
}
