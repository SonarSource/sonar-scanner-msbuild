/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.TFS.Classic;
using TestUtilities;

namespace SonarScanner.MSBuild.TFS.Tests
{
    [TestClass]
    public class BinaryToXmlCoverageReportConverterTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void Conv_Ctor_InvalidArgs_Throws()
        {
            Action op = () => new BinaryToXmlCoverageReportConverter(null, new AnalysisConfig());

            op.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void Conv_ConvertToXml_InvalidArgs_Throws()
        {
            ILogger loggerMock = new Mock<ILogger>().Object;
            var testSubject = new BinaryToXmlCoverageReportConverter(loggerMock, new AnalysisConfig());

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
        public void Conv_ConvertionFailure_Success_False_And_ErrorLogged()
        {
            // Arrange
            var logger = new TestLogger();
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var outputFilePath = Path.Combine(testDir, "output.txt");

            var inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            // Act
            var success = BinaryToXmlCoverageReportConverter.ConvertBinaryToXml(inputFilePath, outputFilePath, logger);

            // Assert
            success.Should().BeFalse();

            File.Exists(outputFilePath).Should().BeFalse("Conversion failed");

            logger.AssertErrorLogged(@$"Failed to convert the downloaded code coverage tool to XML. No code coverage information will be uploaded to SonarQube.
Check that the downloaded code coverage file ({inputFilePath}) is valid by opening it in Visual Studio. If it is not, check that the internet security settings on the build machine allow files to be downloaded from the Team Foundation Server machine.");
            logger.AssertNoWarningsLogged();
        }

        [TestMethod]
        [WorkItem(145)] // Regression test for bug #145: Poor UX if the code coverage report could not be converted to XML
        public void Conv_FailsIfFileConverterReturnsAnErrorCode()
        {
            // Arrange
            var logger = new TestLogger();
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var outputFilePath = Path.Combine(testDir, "output.txt");

            var inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            // Act
            var success = BinaryToXmlCoverageReportConverter.ConvertBinaryToXml(inputFilePath, outputFilePath, logger);

            // Assert
            success.Should().BeFalse("Expecting the process to fail");
            logger.AssertErrorsLogged();
            logger.AssertSingleErrorExists(inputFilePath); // error message should refer to the input file

            File.Exists(outputFilePath).Should().BeFalse("Not expecting the output file to exist");
        }

        [CodeCoverageExeTestMethod]
        [DeploymentItem(@"Resources\Sample.coverage")]
        [DeploymentItem(@"Resources\Expected.xmlcoverage")]
        public void Conv_ConvertToXml_ToolConvertsSampleFile()
        {
            // Arrange
            var logger = new TestLogger();
            var config = new AnalysisConfig();
            config.SetVsCoverageConverterToolPath(CodeCoverageExeTestMethodAttribute.FindCodeCoverageExe());
            var reporter = new BinaryToXmlCoverageReportConverter(logger, config);
            reporter.Initialize();
            var inputFilePath = $"{Environment.CurrentDirectory}\\Sample.coverage";
            var outputFilePath = $"{Environment.CurrentDirectory}\\Sample.xmlcoverage";
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
            // All tags and attributes must appear in the same order for actual and expected. Comments, whitespace, and the like is ignored in the assertion.
            actualContent.Should().BeEquivalentTo(expectedContent);
        }

        private static IVisualStudioSetupConfigurationFactory CreateVisualStudioSetupConfigurationFactory(string packageId)
        {
            var calls = 0;
            var fetched = 1;
            var noFetch = 0;

            // We need to do this kind of trickery because Moq cannot setup a callback for a method with an out parameter.
            Func<ISetupInstance[], bool> setupInstance = instances =>
            {
                if (calls > 0)
                {
                    return false;
                }

                var package = Mock.Of<ISetupPackageReference>();
                Mock.Get(package)
                    .Setup(x => x.GetId())
                    .Returns(packageId);

                var instanceMock = new Mock<ISetupInstance2>();
                instanceMock
                    .Setup(_ => _.GetPackages())
                    .Returns(new[] { package });
                instanceMock
                    .Setup(_ => _.GetInstallationVersion())
                    .Returns("42");
                instanceMock
                    .Setup(_ => _.GetInstallationPath())
                    .Returns("x:\\foo");

                instances[0] = instanceMock.Object;
                calls++;

                return true;
            };

            Func<ISetupInstance[], bool> isSecondCall = _ => (calls > 0);

            var enumInstances = Mock.Of<IEnumSetupInstances>();
            Mock.Get(enumInstances)
                .Setup(_ => _.Next(It.IsAny<int>(), It.Is<ISetupInstance[]>(x => isSecondCall(x)), out noFetch));
            Mock.Get(enumInstances)
                .Setup(_ => _.Next(It.IsAny<int>(), It.Is<ISetupInstance[]>(x => setupInstance(x)), out fetched));

            var configuration = Mock.Of<ISetupConfiguration>();
            Mock.Get(configuration)
                .Setup(_ => _.EnumInstances())
                .Returns(enumInstances);

            var factory = Mock.Of<IVisualStudioSetupConfigurationFactory>();
            Mock.Get(factory)
                .Setup(_ => _.GetSetupConfigurationQuery())
                .Returns(configuration);

            return factory;
        }

        #endregion Tests

        private sealed class TestFile : IDisposable
        {
            private readonly string filePath;

            public TestFile(string filePath)
            {
                this.filePath = filePath;
                using var f = File.Create(filePath);
            }

            public void Dispose() =>
                File.Delete(filePath);
        }
    }
}
