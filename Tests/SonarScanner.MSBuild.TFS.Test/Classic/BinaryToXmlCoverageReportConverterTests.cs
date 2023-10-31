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
        public void Conv_OutputIsCaptured()
        {
            // Arrange
            var logger = new TestLogger();
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var outputFilePath = Path.Combine(testDir, "output.txt");

            var inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            var converterFilePath = Path.Combine(testDir, "converter.bat");
            File.WriteAllText(converterFilePath,
@"
echo Normal output...
echo Error output...>&2
echo Create a new file using the output parameter
echo foo > """ + outputFilePath + @"""");

            // Act
            var success = BinaryToXmlCoverageReportConverter.ConvertBinaryToXml(inputFilePath, outputFilePath, logger);

            // Assert
            success.Should().BeTrue("Expecting the process to succeed");

            File.Exists(outputFilePath).Should().BeTrue("Expecting the output file to exist");
            TestContext.AddResultFile(outputFilePath);

            logger.AssertInfoLogged("Normal output...");
            logger.AssertErrorLogged("Error output...");
        }

        [TestMethod]
        [WorkItem(72)] // Regression test for bug #72: fail the conversion if the output file is not created
        public void Conv_FailsIfFileNotFound()
        {
            // Arrange
            var logger = new TestLogger();
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var outputFilePath = Path.Combine(testDir, "output.txt");

            var inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            var converterFilePath = Path.Combine(testDir, "converter.bat");
            File.WriteAllText(converterFilePath, @"REM Do nothing - don't create a file");

            // Act
            var success = BinaryToXmlCoverageReportConverter.ConvertBinaryToXml(inputFilePath, outputFilePath, logger);

            // Assert
            success.Should().BeFalse("Expecting the process to fail");
            logger.AssertErrorsLogged();
            logger.AssertSingleErrorExists(outputFilePath); // error message should refer to the output file

            File.Exists(outputFilePath).Should().BeFalse("Not expecting the output file to exist");
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

            var converterFilePath = Path.Combine(testDir, "converter.bat");
            File.WriteAllText(converterFilePath, @"exit -1");

            // Act
            var success = BinaryToXmlCoverageReportConverter.ConvertBinaryToXml(inputFilePath, outputFilePath, logger);

            // Assert
            success.Should().BeFalse("Expecting the process to fail");
            logger.AssertErrorsLogged();
            logger.AssertSingleErrorExists(inputFilePath); // error message should refer to the input file

            File.Exists(outputFilePath).Should().BeFalse("Not expecting the output file to exist");
        }

        [TestMethod]
        public void Conv_HasThreeArguments()
        {
            // Arrange
            var logger = new TestLogger();
            var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

            var outputFilePath = Path.Combine(testDir, "output.txt");

            var inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            var converterFilePath = Path.Combine(testDir, "converter.bat");
            File.WriteAllText(converterFilePath,
@"
set argC=0
for %%x in (%*) do Set /A argC+=1

echo Converter called with %argC% args
echo success > """ + outputFilePath + @"""");

            // Act
            var success = BinaryToXmlCoverageReportConverter.ConvertBinaryToXml(inputFilePath, outputFilePath, logger);

            // Assert
            success.Should().BeTrue("Expecting the process to succeed");

            logger.AssertInfoLogged("Converter called with 3 args");
        }

        [TestMethod]
        public void Initialize_CanGetGetExeToolPathFromSetupConfiguration()
        {
            // Arrange
            var logger = new TestLogger();

            var factory = CreateVisualStudioSetupConfigurationFactory("Microsoft.VisualStudio.TestTools.CodeCoverage");

            var reporter = new BinaryToXmlCoverageReportConverter(factory, logger, new AnalysisConfig());

            // Act
            var result = reporter.Initialize();

            // Assert
            result.Should().BeTrue();

            logger.AssertDebugLogged("Code coverage command line tool: x:\\foo\\Team Tools\\Dynamic Code Coverage Tools\\CodeCoverage.exe");
        }

        [TestMethod]
        public void Initialize_CanGetGetExeToolPathFromEnvironmentVariable_FullPathToCodeCoverageToolGiven()
        {
            // Arrange
            var logger = new TestLogger();
            var config = new AnalysisConfig();
            var filePath = Path.Combine(Environment.CurrentDirectory, "CodeCoverage.exe");
            using var f = new TestFile(filePath);
            config.SetVsCoverageConverterToolPath(filePath);

            var reporter = new BinaryToXmlCoverageReportConverter(logger, config);

            // Act
            var result = reporter.Initialize();

            // Assert
            result.Should().BeTrue();

            logger.AssertDebugLogged($@"CodeCoverage.exe found at {filePath}.");
        }

        [TestMethod]
        public void Initialize_NoPath_ReturnsFalseAndLogsWarning()
        {
            var logger = new TestLogger();
            var reporter = new BinaryToXmlCoverageReportConverter(Mock.Of<IVisualStudioSetupConfigurationFactory>(), logger, new AnalysisConfig());

            var result = reporter.Initialize();

            result.Should().BeFalse();
            logger.AssertWarningLogged("Failed to find the code coverage command line tool. Possible cause: Visual Studio is not installed, or the installed version does not support code coverage.");
        }

        [DataTestMethod]
        [DoNotParallelize]
        [DataRow(@"tools\net451\Team Tools\Dynamic Code Coverage Tools\CodeCoverage.exe")]
        [DataRow(@"tools\net462\Team Tools\Dynamic Code Coverage Tools\CodeCoverage.exe")]
        public void Initialize_CanGetGetExeToolPathFromEnvironmentVariable_NoExeInThePath_ShouldSeekForStandardInstall(string standardPath)
        {
            // Arrange
            var logger = new TestLogger();
            var config = new AnalysisConfig();

            var filePath = Path.Combine(Environment.CurrentDirectory, standardPath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            using var f = new TestFile(filePath);
            config.SetVsCoverageConverterToolPath(Environment.CurrentDirectory);
            var reporter = new BinaryToXmlCoverageReportConverter(logger, config);

            // Act
            var result = reporter.Initialize();

            // Assert
            result.Should().BeTrue();
            logger.AssertDebugLogged($@"CodeCoverage.exe found at {filePath}.");
        }

        [TestMethod]
        public void Initialize_CanGetGetExeToolPathFromEnvironmentVariable_FileDoesntExist_ShouldFallback()
        {
            // Arrange
            var logger = new TestLogger();
            var config = new AnalysisConfig();
            var factory = CreateVisualStudioSetupConfigurationFactory("Microsoft.VisualStudio.TestTools.CodeCoverage");

            config.SetVsCoverageConverterToolPath(Path.GetTempPath());

            var reporter = new BinaryToXmlCoverageReportConverter(factory, logger, config);

            // Act
            var result = reporter.Initialize();

            // Assert
            result.Should().BeTrue();

            logger.Warnings.Contains("CodeCoverage.exe was not found in the standard locations. Please provide the full path of the tool using the VsTestToolsInstallerInstalledToolLocation variable.");
            logger.AssertDebugLogged("Code coverage command line tool: x:\\foo\\Team Tools\\Dynamic Code Coverage Tools\\CodeCoverage.exe");
        }

        [TestMethod]
        public void Initialize_CanGetGetExeToolPathFromSetupConfigurationForBuildAgent()
        {
            // Arrange
            var logger = new TestLogger();

            var factory = CreateVisualStudioSetupConfigurationFactory("Microsoft.VisualStudio.TestTools.CodeCoverage.Msi");

            var reporter = new BinaryToXmlCoverageReportConverter(factory, logger, new AnalysisConfig());

            // Act
            var result = reporter.Initialize();

            // Assert
            result.Should().BeTrue();

            logger.AssertDebugLogged("Code coverage command line tool: x:\\foo\\Team Tools\\Dynamic Code Coverage Tools\\CodeCoverage.exe");
        }

        [TestMethod]
        public void GetRegistryPath_When64BitProcess_Returns64BitPath() =>
            BinaryToXmlCoverageReportConverter.GetVsRegistryPath(true).Should().Be(@"SOFTWARE\Wow6432Node\Microsoft\VisualStudio");

        [TestMethod]
        public void GetRegistryPath_When32BitProcess_Returns32BitPath() =>
            BinaryToXmlCoverageReportConverter.GetVsRegistryPath(false).Should().Be(@"SOFTWARE\Microsoft\VisualStudio");

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
