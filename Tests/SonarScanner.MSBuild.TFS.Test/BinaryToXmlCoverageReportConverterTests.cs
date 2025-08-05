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

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace SonarScanner.MSBuild.TFS.Tests;

[TestClass]
[DoNotParallelize]
public class BinaryToXmlCoverageReportConverterTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void BinaryToXmlCoverageReportConverter_InvalidArgs_Throws()
    {
        Action op = () => _ = new BinaryToXmlCoverageReportConverter(null);

        op.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void ConvertToXml_InvalidArgs_Throws()
    {
        var testSubject = new BinaryToXmlCoverageReportConverter(Substitute.For<ILogger>());

        // 1. Null input path
        Action op = () => testSubject.ConvertToXml(null, "dummypath");
        op.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("inputFilePath");

        op = () => testSubject.ConvertToXml("\t\n", "dummypath");
        op.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("inputFilePath");

        // 2. Null output path
        op = () => testSubject.ConvertToXml("dummypath", null);
        op.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("outputFilePath");

        op = () => testSubject.ConvertToXml("dummypath", "   ");
        op.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("outputFilePath");
    }

    [TestMethod]
    public void ConvertToXml_ConversionFailure_SuccessFalseAndErrorLogged()
    {
        var context = new ConverterTestContext(TestContext);
        new BinaryToXmlCoverageReportConverter(context.Logger).ConvertToXml(context.InputFilePath, context.OutputFilePath).Should().BeFalse();
        File.Exists(context.OutputFilePath).Should().BeFalse("Conversion failed");
        context.Logger.AssertErrorLogged($"""
            Failed to convert the binary code coverage reports to XML. No code coverage information will be uploaded to the server (SonarQube/SonarCloud).
            Check that the downloaded code coverage file ({context.InputFilePath}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.
            """);
        context.Logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public void ConvertToXml_FileConverterReturnsAnErrorCode_Fails()
    {
        var context = new ConverterTestContext(TestContext);
        new BinaryToXmlCoverageReportConverter(context.Logger).ConvertToXml(context.InputFilePath, context.OutputFilePath).Should().BeFalse("Expecting the process to fail");
        context.Logger.AssertErrorsLogged();
        context.Logger.AssertSingleErrorExists(context.InputFilePath); // error message should refer to the input file
        File.Exists(context.OutputFilePath).Should().BeFalse("Not expecting the output file to exist");
    }

    [TestMethod]
    public void ConvertToXml_InputFileDoesNotExists_Fails()
    {
        var context = new ConverterTestContext(TestContext, fileContent: null);
        new BinaryToXmlCoverageReportConverter(context.Logger).ConvertToXml(context.InputFilePath, context.OutputFilePath).Should().BeFalse("Expecting the process to fail");
        context.Logger.Errors.Should().ContainSingle().Which.Should()
            .Be($"The binary coverage file {context.InputFilePath} could not be found. No coverage information will be uploaded to the Sonar server.");
        File.Exists(context.OutputFilePath).Should().BeFalse("Not expecting the output file to exist");
    }

    [TestMethod]
    public void ConvertToXml_InputFileIsLocked_Fails()
    {
        var context = new ConverterTestContext(TestContext);
        try
        {
            using var fs = new FileStream(context.InputFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); // lock the file with FileShare.None
            // FileShare.None will cause nested inner exceptions: AggregateException -> CoverageFileException -> IOException with messages
            // AggregateException: One or more errors occurred.
            // CoverageFileException: Failed to open coverage file "C:\Fullpath\input.txt".
            // IOException: The process cannot access the file 'C:\Fullpath\input.txt' because it is being used by another process.
            new BinaryToXmlCoverageReportConverter(context.Logger).ConvertToXml(context.InputFilePath, context.OutputFilePath).Should().BeFalse("Expecting the process to fail");
            context.Logger.Errors.Should().ContainSingle().Which.Should().Match("Failed to convert the binary code coverage reports to XML. "
                + "No code coverage information will be uploaded to the server (SonarQube/SonarCloud)."
                + $"*Check that the downloaded code coverage file ({context.InputFilePath}) is valid by opening it in Visual Studio. "
                + "If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.");
            File.Exists(context.OutputFilePath).Should().BeFalse("Not expecting the output file to exist");
        }
        finally
        {
            File.Delete(context.InputFilePath);
        }
    }

    [TestMethod]
    // DeploymentItem does not work on Linux for relative files: https://github.com/microsoft/testfx/issues/1460
    [DeploymentItem(@"Resources")] // Copy whole directory. Contains: Sample.coverage and Expected.xmlcoverage
    public void ConvertToXml_ConvertsSampleFile()
    {
        var logger = new TestLogger();
        var inputFilePath = Path.Combine(Environment.CurrentDirectory, "Sample.coverage");
        var outputFilePath = Path.Combine(Environment.CurrentDirectory, $"{nameof(ConvertToXml_ConvertsSampleFile)}.xmlcoverage");
        var expectedOutputFilePath = Path.Combine(Environment.CurrentDirectory, "Expected.xmlcoverage");

        File.Exists(inputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeFalse();
        File.Exists(expectedOutputFilePath).Should().BeTrue();
        new BinaryToXmlCoverageReportConverter(logger).ConvertToXml(inputFilePath, outputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeTrue();
        // All tags and attributes must appear in actual and expected. Comments, whitespace, ordering, and the like is ignored in the assertion.
        XDocument.Load(outputFilePath).Should().BeEquivalentTo(XDocument.Load(expectedOutputFilePath));
        logger.DebugMessages.Should().ContainSingle().Which.Should().Match($"Converting coverage file '*Sample.coverage' to '*{nameof(ConvertToXml_ConvertsSampleFile)}.xmlcoverage'.");
    }

    [TestMethod]
    // DeploymentItem does not work on Linux for relative files: https://github.com/microsoft/testfx/issues/1460
    [DeploymentItem(@"Resources")] // Copy whole directory. Contains: Sample.coverage and Expected.xmlcoverage
    public void ConvertToXml_ProblematicCulture_ConvertsSampleFile()
    {
        var logger = new TestLogger();
        var inputFilePath = Path.Combine(Environment.CurrentDirectory, "Sample.coverage");
        var outputFilePath = Path.Combine(Environment.CurrentDirectory, $"{nameof(ConvertToXml_ProblematicCulture_ConvertsSampleFile)}.xmlcoverage");
        var expectedOutputFilePath = Path.Combine(Environment.CurrentDirectory, "Expected.xmlcoverage");

        File.Exists(inputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeFalse();
        File.Exists(expectedOutputFilePath).Should().BeTrue();
        using var _ = new ApplicationCultureInfo(CultureInfo.GetCultureInfo("de-DE")); // Serializes block_coverage="33.33" as block_coverage="33,33"
        new BinaryToXmlCoverageReportConverter(logger).ConvertToXml(inputFilePath, outputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeTrue();
        // All tags and attributes must appear in actual and expected. Comments, whitespace, ordering, and the like is ignored in the assertion.
        XDocument.Load(outputFilePath).Should().BeEquivalentTo(XDocument.Load(expectedOutputFilePath));
    }

    private class ConverterTestContext
    {
        public TestLogger Logger { get; }
        public string InputFilePath { get; }
        public string OutputFilePath { get; }

        public ConverterTestContext(TestContext testContext, string fileContent = "dummy input file", [CallerMemberName]string testMethodName = null)
        {
            Logger = new TestLogger();
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(testContext);
            InputFilePath = Path.Combine(testDir, $"input_{testMethodName}.txt");
            OutputFilePath = Path.Combine(testDir, "output.txt");
            if (fileContent is not null)
            {
                File.WriteAllText(InputFilePath, fileContent);
            }
        }
    }
}
