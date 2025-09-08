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

namespace SonarScanner.MSBuild.PostProcessor.Test;

[TestClass]
public class ArgumentProcessorTests
{
    public TestContext TestContext { get; set; }

    #region Tests

    [TestMethod]
    public void PostArgProc_NoArgs()
    {
        // 0. Setup
        var logger = new TestLogger();
        IAnalysisPropertyProvider provider;

        // 1. Null input
        Action act = () => ArgumentProcessor.TryProcessArgs(null, logger, out provider); act.Should().ThrowExactly<ArgumentNullException>();

        // 2. Empty array input
        provider = CheckProcessingSucceeds(logger, new string[] { });
        provider.AssertExpectedPropertyCount(0);
    }

    [TestMethod]
    public void PostArgProc_Unrecognised()
    {
        // 0. Setup
        TestLogger logger;

        // 1. Unrecognized args
        logger = CheckProcessingFails("begin"); // bootstrapper verbs aren't meaningful to the post-processor
        logger.Should().HaveSingleError("Unrecognized command line argument: begin");

        logger = CheckProcessingFails("end");
        logger.Should().HaveSingleError("Unrecognized command line argument: end");

        logger = CheckProcessingFails("AAA", "BBB", "CCC");
        logger.Should().HaveErrors(
            "Unrecognized command line argument: AAA",
            "Unrecognized command line argument: BBB",
            "Unrecognized command line argument: CCC");
    }

    [TestMethod]
    public void PostArgProc_PermittedArguments()
    {
        var logger = new TestLogger();
        var args = new[]
        {
            "/d:sonar.token=token",
            "/d:sonar.login=user name",
            "/d:sonar.password=pwd",
        };

        var provider = CheckProcessingSucceeds(logger, args);

        provider.AssertExpectedPropertyCount(3);
        provider.AssertExpectedPropertyValue("sonar.token", "token");
        provider.AssertExpectedPropertyValue("sonar.login", "user name");
        provider.AssertExpectedPropertyValue("sonar.password", "pwd");
    }

    [TestMethod]
    [DataRow(new[] { "/d:sonar.visualstudio.enable=false" }, new[] { "sonar.visualstudio.enable" })] // 1. Valid /d: arguments, but not the permitted ones
    [DataRow(new[] { "/d:aaa=bbb", "/d:xxx=yyy" }, new[] { "aaa", "xxx" })]
    [DataRow(new[] { "/D:sonar.token=token" }, new[] { "sonar.token" })] // wrong case for "/d:"
    [DataRow(new[] { "/d:SONAR.login=user name" }, new[] { "SONAR.login" })] // wrong case for argument name
    public void PostArgProc_NotPermittedArguments(string[] arguments, string[] propertiesWithErrors)
    {
        var logger = CheckProcessingFails(arguments);

        foreach (var propertyWithError in propertiesWithErrors)
        {
            logger.Errors.Should().ContainSingle(x => x.Contains(propertyWithError));
        }
    }

    #endregion Tests

    #region Checks

    private static IAnalysisPropertyProvider CheckProcessingSucceeds(TestLogger logger, string[] input)
    {
        var success = ArgumentProcessor.TryProcessArgs(input, logger, out IAnalysisPropertyProvider provider);

        success.Should().BeTrue("Expecting processing to have succeeded");
        provider.Should().NotBeNull("Returned provider should not be null");
        logger.Should().HaveErrors(0);

        return provider;
    }

    private static TestLogger CheckProcessingFails(params string[] input)
    {
        var logger = new TestLogger();

        var success = ArgumentProcessor.TryProcessArgs(input, logger, out IAnalysisPropertyProvider provider);

        success.Should().BeFalse("Not expecting processing to have succeeded");
        provider.Should().BeNull("Provider should be null if processing fails");
        logger.Should().HaveErrors(); // expecting errors if processing failed

        return logger;
    }

    #endregion Checks
}
