/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

    [TestMethod]
    public void PostArgProc_Null() =>
        FluentActions.Invoking(() => ArgumentProcessor.TryProcessArgs(null, new TestLogger(), out _)).Should().ThrowExactly<ArgumentNullException>();

    [TestMethod]
    public void PostArgProc_NoArgs() =>
        CheckProcessingSucceeds(new TestLogger(), []).GetAllProperties().Should().BeEmpty();

    [TestMethod]
    public void PostArgProc_Unrecognised()
    {
        // bootstrapper verbs aren't meaningful to the post-processor
        CheckProcessingFails("begin").Should().HaveErrorOnce("Unrecognized command line argument: begin");
        CheckProcessingFails("end").Should().HaveErrorOnce("Unrecognized command line argument: end");
        CheckProcessingFails("AAA", "BBB", "CCC").Should().HaveErrors(
            "Unrecognized command line argument: AAA",
            "Unrecognized command line argument: BBB",
            "Unrecognized command line argument: CCC");
    }

    [TestMethod]
    public void PostArgProc_PermittedArguments()
    {
        var args = new[]
        {
            "/d:sonar.token=token",
            "/d:sonar.login=user name",
            "/d:sonar.password=pwd",
        };
        CheckProcessingSucceeds(new TestLogger(), args).GetAllProperties().Should().BeEquivalentTo([
            new Property("sonar.token", "token"),
            new Property("sonar.login", "user name"),
            new Property("sonar.password", "pwd")]);
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

    private static IAnalysisPropertyProvider CheckProcessingSucceeds(TestLogger logger, string[] input)
    {
        ArgumentProcessor.TryProcessArgs(input, logger, out var provider).Should().BeTrue("Expecting processing to have succeeded");
        provider.Should().NotBeNull("Returned provider should not be null");
        logger.Should().HaveNoErrors();
        return provider;
    }

    private static TestLogger CheckProcessingFails(params string[] input)
    {
        var logger = new TestLogger();
        ArgumentProcessor.TryProcessArgs(input, logger, out var provider).Should().BeFalse("Not expecting processing to have succeeded");
        provider.Should().BeNull("Provider should be null if processing fails");
        logger.Should().HaveErrors(); // expecting errors if processing failed
        return logger;
    }
}
