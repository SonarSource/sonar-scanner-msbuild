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
using System.Xml.Linq;

namespace SonarScanner.MSBuild.TFS.Tests;

[TestClass]
[DoNotParallelize]
public class BinaryToXmlCoverageReportConverterTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Conv_Ctor_InvalidArgs_Throws()
    {
        Action op = () => _ = new BinaryToXmlCoverageReportConverter(null);

        op.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
    }

    [TestMethod]
    public void Conv_ConvertToXml_InvalidArgs_Throws()
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
    public void Conv_ConversionFailure_Success_False_And_ErrorLogged()
    {
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var outputFilePath = Path.Combine(testDir, "output.txt");
        var inputFilePath = Path.Combine(testDir, $"input_{nameof(Conv_ConversionFailure_Success_False_And_ErrorLogged)}.txt");
        File.WriteAllText(inputFilePath, "dummy input file");

        new BinaryToXmlCoverageReportConverter(logger).ConvertToXml(inputFilePath, outputFilePath).Should().BeFalse();
        File.Exists(outputFilePath).Should().BeFalse("Conversion failed");
        logger.AssertErrorLogged($"""
            Failed to convert the binary code coverage reports to XML. No code coverage information will be uploaded to the server (SonarQube/SonarCloud).
            Check that the downloaded code coverage file ({inputFilePath}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.
            """);
        logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public void Conv_FailsIfFileConverterReturnsAnErrorCode()
    {
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var outputFilePath = Path.Combine(testDir, "output.txt");
        var inputFilePath = Path.Combine(testDir, $"input_{nameof(Conv_FailsIfFileConverterReturnsAnErrorCode)}.txt");
        File.WriteAllText(inputFilePath, "dummy input file");

        new BinaryToXmlCoverageReportConverter(logger).ConvertToXml(inputFilePath, outputFilePath).Should().BeFalse("Expecting the process to fail");
        logger.AssertErrorsLogged();
        logger.AssertSingleErrorExists(inputFilePath); // error message should refer to the input file
        File.Exists(outputFilePath).Should().BeFalse("Not expecting the output file to exist");
    }

    [TestMethod]
    public void Conv_FailsIfInputFileDoesNotExists()
    {
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var outputFilePath = Path.Combine(testDir, "output.txt");
        var inputFilePath = Path.Combine(testDir, $"input_{nameof(Conv_FailsIfInputFileDoesNotExists)}.txt");

        new BinaryToXmlCoverageReportConverter(logger).ConvertToXml(inputFilePath, outputFilePath).Should().BeFalse("Expecting the process to fail");
        logger.Errors.Should().ContainSingle().Which.Should().Be(@$"The binary coverage file {inputFilePath} could not be found. No coverage information will be uploaded to the Sonar server.");
        File.Exists(outputFilePath).Should().BeFalse("Not expecting the output file to exist");
    }

    [TestMethod]
    public void Conv_FailsIfInputFileIsLocked()
    {
        var logger = new TestLogger();
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var outputFilePath = Path.Combine(testDir, "output.txt");
        var inputFilePath = Path.Combine(testDir, $"input_{nameof(Conv_FailsIfInputFileIsLocked)}.txt");
        File.WriteAllText(inputFilePath, "Some content");

        try
        {
            using var fs = new FileStream(inputFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); // lock the file with FileShare.None

            // FileShare.None will cause nested inner exceptions: AggregateException -> CoverageFileException -> IOException with messages
            // AggregateException: One or more errors occurred.
            // CoverageFileException: Failed to open coverage file "C:\Fullpath\input.txt".
            // IOException: The process cannot access the file 'C:\Fullpath\input.txt' because it is being used by another process.
            new BinaryToXmlCoverageReportConverter(logger).ConvertToXml(inputFilePath, outputFilePath).Should().BeFalse("Expecting the process to fail");
            logger.Errors.Should().ContainSingle().Which.Should().Match("Failed to convert the binary code coverage reports to XML. "
                + "No code coverage information will be uploaded to the server (SonarQube/SonarCloud)."
                + $"*Check that the downloaded code coverage file ({inputFilePath}) is valid by opening it in Visual Studio. "
                + "If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.");
            File.Exists(outputFilePath).Should().BeFalse("Not expecting the output file to exist");
        }
        finally
        {
            File.Delete(inputFilePath);
        }
    }

    [TestMethod]
    // DeploymentItem does not work on Linux for relative files: https://github.com/microsoft/testfx/issues/1460
    [DeploymentItem(@"Resources")] // Copy whole directory. Contains: Sample.coverage and Expected.xmlcoverage
    public void Conv_ConvertToXml_ToolConvertsSampleFile()
    {
        var logger = new TestLogger();
        var inputFilePath = Path.Combine(Environment.CurrentDirectory, "Sample.coverage");
        var outputFilePath = Path.Combine(Environment.CurrentDirectory, $"{nameof(Conv_ConvertToXml_ToolConvertsSampleFile)}.xmlcoverage");
        var expectedOutputFilePath = Path.Combine(Environment.CurrentDirectory, "Expected.xmlcoverage");

        File.Exists(inputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeFalse();
        File.Exists(expectedOutputFilePath).Should().BeTrue();
        new BinaryToXmlCoverageReportConverter(logger).ConvertToXml(inputFilePath, outputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeTrue();
        // All tags and attributes must appear in actual and expected. Comments, whitespace, ordering, and the like is ignored in the assertion.
        XDocument.Load(outputFilePath).Should().BeEquivalentTo(XDocument.Load(expectedOutputFilePath));
        logger.DebugMessages.Should().ContainSingle().Which.Should().Match(@"Converting coverage file '*Sample.coverage' to '*Conv_ConvertToXml_ToolConvertsSampleFile.xmlcoverage'.");
    }

    [TestMethod]
    // DeploymentItem does not work on Linux for relative files: https://github.com/microsoft/testfx/issues/1460
    [DeploymentItem(@"Resources")] // Copy whole directory. Contains: Sample.coverage and Expected.xmlcoverage
    public void Conv_ConvertToXml_ToolConvertsSampleFile_ProblematicCulture()
    {
        var logger = new TestLogger();
        var inputFilePath = Path.Combine(Environment.CurrentDirectory, "Sample.coverage");
        var outputFilePath = Path.Combine(Environment.CurrentDirectory, $"{nameof(Conv_ConvertToXml_ToolConvertsSampleFile_ProblematicCulture)}.xmlcoverage");
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
}
