/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
using FluentAssertions;
using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration.Classic;
using TestUtilities;

namespace SonarQube.TeamBuild.Integration.Tests
{
    [TestClass]
    public class BinaryToXmlCoverageReportConverterTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void Conv_Initialize_InvalidArgs_Throws()
        {
            var testSubject = new BinaryToXmlCoverageReportConverter();
            Action op = () => testSubject.Initialize(null);

            op.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void Conv_ConvertToXml_InvalidArgs_Throws()
        {
            ILogger loggerMock = new Mock<ILogger>().Object;
            var testSubject = new BinaryToXmlCoverageReportConverter();

            // 1. Null input path
            Action op = () => testSubject.ConvertToXml(null, "dummypath", loggerMock);
            op.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("inputFilePath");

            op = () => testSubject.ConvertToXml("\t\n", "dummypath", loggerMock);
            op.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("inputFilePath");

            // 2. Null output path
            op = () => testSubject.ConvertToXml("dummypath", null, loggerMock);
            op.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("outputFilePath");

            op = () => testSubject.ConvertToXml("dummypath", "   ", loggerMock);
            op.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("outputFilePath");

            // 3. Null logger
            op = () => testSubject.ConvertToXml("dummyInput", "dummyOutput", null);
            op.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        [WorkItem(72)] // Regression test for bug #72: CodeCoverage conversion - conversion errors should be detected and reported
        public void Conv_OutputIsCaptured()
        {
            // Arrange
            var logger = new TestLogger();
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);

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
            var success = BinaryToXmlCoverageReportConverter.ConvertBinaryToXml(converterFilePath, inputFilePath, outputFilePath, logger);

            // Assert
            Assert.IsTrue(success, "Expecting the process to succeed");

            Assert.IsTrue(File.Exists(outputFilePath), "Expecting the output file to exist");
            TestContext.AddResultFile(outputFilePath);

            logger.AssertMessageLogged("Normal output...");
            logger.AssertErrorLogged("Error output...");
        }

        [TestMethod]
        [WorkItem(72)] // Regression test for bug #72: fail the conversion if the output file is not created
        public void Conv_FailsIfFileNotFound()
        {
            // Arrange
            var logger = new TestLogger();
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);

            var outputFilePath = Path.Combine(testDir, "output.txt");

            var inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            var converterFilePath = Path.Combine(testDir, "converter.bat");
            File.WriteAllText(converterFilePath, @"REM Do nothing - don't create a file");

            // Act
            var success = BinaryToXmlCoverageReportConverter.ConvertBinaryToXml(converterFilePath, inputFilePath, outputFilePath, logger);

            // Assert
            Assert.IsFalse(success, "Expecting the process to fail");
            logger.AssertErrorsLogged();
            logger.AssertSingleErrorExists(outputFilePath); // error message should refer to the output file

            Assert.IsFalse(File.Exists(outputFilePath), "Not expecting the output file to exist");
        }

        [TestMethod]
        [WorkItem(145)] // Regression test for bug #145: Poor UX if the code coverage report could not be converted to XML
        public void Conv_FailsIfFileConverterReturnsAnErrorCode()
        {
            // Arrange
            var logger = new TestLogger();
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);

            var outputFilePath = Path.Combine(testDir, "output.txt");

            var inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            var converterFilePath = Path.Combine(testDir, "converter.bat");
            File.WriteAllText(converterFilePath, @"exit -1");

            // Act
            var success = BinaryToXmlCoverageReportConverter.ConvertBinaryToXml(converterFilePath, inputFilePath, outputFilePath, logger);

            // Assert
            Assert.IsFalse(success, "Expecting the process to fail");
            logger.AssertErrorsLogged();
            logger.AssertSingleErrorExists(inputFilePath); // error message should refer to the input file

            Assert.IsFalse(File.Exists(outputFilePath), "Not expecting the output file to exist");
        }

        [TestMethod]
        public void Conv_HasThreeArguments()
        {
            // Arrange
            var logger = new TestLogger();
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);

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
            var success = BinaryToXmlCoverageReportConverter.ConvertBinaryToXml(converterFilePath, inputFilePath, outputFilePath, logger);

            // Assert
            Assert.IsTrue(success, "Expecting the process to succeed");

            logger.AssertMessageLogged("Converter called with 3 args");
        }

        [TestMethod]
        public void Initialize_CanGetGetExeToolPathFromSetupConfiguration()
        {
            // Arrange
            var logger = new TestLogger();

            var factory = CreateVisualStudioSetupConfigurationFactory("Microsoft.VisualStudio.TestTools.CodeCoverage");

            var reporter = new BinaryToXmlCoverageReportConverter(factory);

            // Act
            var result = reporter.Initialize(logger);

            // Assert
            Assert.IsTrue(result);

            logger.AssertDebugLogged("Code coverage command line tool: x:\\foo\\Team Tools\\Dynamic Code Coverage Tools\\CodeCoverage.exe");
        }

        [TestMethod]
        public void Initialize_CanGetGetExeToolPathFromSetupConfigurationForBuildAgent()
        {
            // Arrange
            var logger = new TestLogger();

            var factory = CreateVisualStudioSetupConfigurationFactory("Microsoft.VisualStudio.TestTools.CodeCoverage.Msi");

            var reporter = new BinaryToXmlCoverageReportConverter(factory);

            // Act
            var result = reporter.Initialize(logger);

            // Assert
            Assert.IsTrue(result);

            logger.AssertDebugLogged("Code coverage command line tool: x:\\foo\\Team Tools\\Dynamic Code Coverage Tools\\CodeCoverage.exe");
        }

        private static IVisualStudioSetupConfigurationFactory CreateVisualStudioSetupConfigurationFactory(string packageId)
        {
            var calls = 0;
            var fetched = 1;
            var noFetch = 0;

            // We need to do this kind of trickery because Moq cannot setup a callback for a method with an out parameter.
            Func<ISetupInstance[], bool> setupInstance = (ISetupInstance[] instances) =>
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
                    .Returns(new ISetupPackageReference[] { package });
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

            Func<ISetupInstance[], bool> isSecondCall = (ISetupInstance[] instances) =>
            {
                return (calls > 0);
            };

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
    }
}
