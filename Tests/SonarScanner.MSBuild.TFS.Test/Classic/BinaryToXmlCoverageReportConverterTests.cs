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
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS.Classic;
using TestUtilities;

namespace SonarScanner.MSBuild.TFS.Tests;

[TestClass]
[DoNotParallelize]
public class BinaryToXmlCoverageReportConverterTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Conv_Ctor_InvalidArgs_Throws()
    {
        Action op = () => new BinaryToXmlCoverageReportConverter(null);

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
    [WorkItem(72)] // Regression test for bug #72: CodeCoverage conversion - conversion errors should be detected and reported
    public void Conv_ConversionFailure_Success_False_And_ErrorLogged()
    {
        // Arrange
        var logger = new TestLogger();
        var sut = new BinaryToXmlCoverageReportConverter(logger);
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var outputFilePath = Path.Combine(testDir, "output.txt");

        var inputFilePath = Path.Combine(testDir, $"input_{nameof(Conv_ConversionFailure_Success_False_And_ErrorLogged)}.txt");
        File.WriteAllText(inputFilePath, "dummy input file");

        // Act
        var success = sut.ConvertToXml(inputFilePath, outputFilePath);

        // Assert
        success.Should().BeFalse();

        File.Exists(outputFilePath).Should().BeFalse("Conversion failed");

        logger.AssertErrorLogged(@$"Failed to convert the binary code coverage reports to XML. No code coverage information will be uploaded to the server (SonarQube/SonarCloud).
Check that the downloaded code coverage file ({inputFilePath}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.");
        logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    [WorkItem(145)] // Regression test for bug #145: Poor UX if the code coverage report could not be converted to XML
    public void Conv_FailsIfFileConverterReturnsAnErrorCode()
    {
        // Arrange
        var logger = new TestLogger();
        var sut = new BinaryToXmlCoverageReportConverter(logger);
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var outputFilePath = Path.Combine(testDir, "output.txt");

        var inputFilePath = Path.Combine(testDir, $"input_{nameof(Conv_FailsIfFileConverterReturnsAnErrorCode)}.txt");
        File.WriteAllText(inputFilePath, "dummy input file");

        // Act
        var success = sut.ConvertToXml(inputFilePath, outputFilePath);

        // Assert
        success.Should().BeFalse("Expecting the process to fail");
        logger.AssertErrorsLogged();
        logger.AssertSingleErrorExists(inputFilePath); // error message should refer to the input file

        File.Exists(outputFilePath).Should().BeFalse("Not expecting the output file to exist");
    }

    [TestMethod]
    public void Conv_FailsIfInputFileDoesNotExists()
    {
        // Arrange
        var logger = new TestLogger();
        var sut = new BinaryToXmlCoverageReportConverter(logger);
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        var outputFilePath = Path.Combine(testDir, "output.txt");

        var inputFilePath = Path.Combine(testDir, $"input_{nameof(Conv_FailsIfInputFileDoesNotExists)}.txt");

        // Act
        var success = sut.ConvertToXml(inputFilePath, outputFilePath);

        // Assert
        success.Should().BeFalse("Expecting the process to fail");
        logger.Errors.Should().ContainSingle().Which.Should().Be(@$"The binary coverage file {inputFilePath} could not be found. No coverage information will be uploaded to the Sonar server.");

        File.Exists(outputFilePath).Should().BeFalse("Not expecting the output file to exist");
    }

    [TestMethod]
    public void Conv_FailsIfInputFileIsLocked()
    {
        // Arrange
        var logger = new TestLogger();
        var sut = new BinaryToXmlCoverageReportConverter(logger);
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

            // Act
            var success = sut.ConvertToXml(inputFilePath, outputFilePath);

            // Assert
            success.Should().BeFalse("Expecting the process to fail");
            logger.Errors.Should().ContainSingle().Which.Should().Match(@$"Failed to convert the binary code coverage reports to XML. No code coverage information will be uploaded to the server (SonarQube/SonarCloud).*Check that the downloaded code coverage file ({inputFilePath}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.");

            File.Exists(outputFilePath).Should().BeFalse("Not expecting the output file to exist");
        }
        finally
        {
            File.Delete(inputFilePath);
        }
    }

    [TestMethod]
    [DeploymentItem(@"Resources\Sample.coverage")]
    [DeploymentItem(@"Resources\Expected.xmlcoverage")]
    public void Conv_ConvertToXml_ToolConvertsSampleFile()
    {
        // Arrange
        var logger = new TestLogger();
        var reporter = new BinaryToXmlCoverageReportConverter(logger);
        var inputFilePath = $"{Environment.CurrentDirectory}\\Sample.coverage";
        var outputFilePath = $"{Environment.CurrentDirectory}\\{nameof(Conv_ConvertToXml_ToolConvertsSampleFile)}.xmlcoverage";
        var expectedOutputFilePath = $"{Environment.CurrentDirectory}\\Expected.xmlcoverage";
        File.Exists(inputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeFalse();
        File.Exists(expectedOutputFilePath).Should().BeTrue();

        // Act
        var actual = reporter.ConvertToXml(inputFilePath, outputFilePath);

        // Assert
        actual.Should().BeTrue();
        File.Exists(outputFilePath).Should().BeTrue();
        var actualContent = XDocument.Load(outputFilePath);
        var expectedContent = XDocument.Load(expectedOutputFilePath);
        // All tags and attributes must appear in actual and expected. Comments, whitespace, ordering, and the like is ignored in the assertion.
        actualContent.Should().BeEquivalentTo(expectedContent);
        logger.DebugMessages.Should().ContainSingle().Which.Should().Match(@"Converting coverage file '*\Sample.coverage' to '*\Conv_ConvertToXml_ToolConvertsSampleFile.xmlcoverage'.");
    }

    [TestMethod]
    [DeploymentItem(@"Resources\Sample.coverage")]
    [DeploymentItem(@"Resources\Expected.xmlcoverage")]
    public void Conv_ConvertToXml_ToolConvertsSampleFile_ProblematicCulture()
    {
        // Arrange
        var logger = new TestLogger();
        var reporter = new BinaryToXmlCoverageReportConverter(logger);
        var inputFilePath = $"{Environment.CurrentDirectory}\\Sample.coverage";
        var outputFilePath = $"{Environment.CurrentDirectory}\\{nameof(Conv_ConvertToXml_ToolConvertsSampleFile_ProblematicCulture)}.xmlcoverage";
        var expectedOutputFilePath = $"{Environment.CurrentDirectory}\\Expected.xmlcoverage";
        File.Exists(inputFilePath).Should().BeTrue();
        File.Exists(outputFilePath).Should().BeFalse();
        File.Exists(expectedOutputFilePath).Should().BeTrue();
        using var _ = new ApplicationCultureInfo(CultureInfo.GetCultureInfo("de-DE")); // Serializes block_coverage="33.33" as block_coverage="33,33"

        // Act
        var actual = reporter.ConvertToXml(inputFilePath, outputFilePath);

        // Assert
        actual.Should().BeTrue();
        File.Exists(outputFilePath).Should().BeTrue();
        var actualContent = XDocument.Load(outputFilePath);
        var expectedContent = XDocument.Load(expectedOutputFilePath);
        // All tags and attributes must appear in actual and expected. Comments, whitespace, ordering, and the like is ignored in the assertion.
        actualContent.Should().BeEquivalentTo(expectedContent);
    }
}
