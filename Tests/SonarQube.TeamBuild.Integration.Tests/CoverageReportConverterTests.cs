/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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
 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using TestUtilities;

namespace SonarQube.TeamBuild.Integration.Tests
{
    [TestClass]
    public class CoverageReportConverterTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [WorkItem(72)] // Regression test for bug #72: CodeCoverage conversion - conversion errors should be detected and reported
        public void Conv_OutputIsCaptured()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string outputFilePath = Path.Combine(testDir, "output.txt");

            string inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            string converterFilePath = Path.Combine(testDir, "converter.bat");
            File.WriteAllText(converterFilePath,
@"
echo Normal output...
echo Error output...>&2
echo Create a new file using the output parameter
echo foo > """ + outputFilePath + @"""");

            // Act
            bool success = CoverageReportConverter.ConvertBinaryToXml(converterFilePath, inputFilePath, outputFilePath, logger);

            // Assert
            Assert.IsTrue(success, "Expecting the process to succeed");

            Assert.IsTrue(File.Exists(outputFilePath), "Expecting the output file to exist");
            this.TestContext.AddResultFile(outputFilePath);

            logger.AssertMessageLogged("Normal output...");
            logger.AssertErrorLogged("Error output...");
        }

        [TestMethod]
        [WorkItem(72)] // Regression test for bug #72: fail the conversion if the output file is not created
        public void Conv_FailsIfFileNotFound()
        {
            // Arrange
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string outputFilePath = Path.Combine(testDir, "output.txt");

            string inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            string converterFilePath = Path.Combine(testDir, "converter.bat");
            File.WriteAllText(converterFilePath, @"REM Do nothing - don't create a file");

            // Act
            bool success = CoverageReportConverter.ConvertBinaryToXml(converterFilePath, inputFilePath, outputFilePath, logger);

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
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string outputFilePath = Path.Combine(testDir, "output.txt");

            string inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            string converterFilePath = Path.Combine(testDir, "converter.bat");
            File.WriteAllText(converterFilePath, @"exit -1");

            // Act
            bool success = CoverageReportConverter.ConvertBinaryToXml(converterFilePath, inputFilePath, outputFilePath, logger);

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
            TestLogger logger = new TestLogger();
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);

            string outputFilePath = Path.Combine(testDir, "output.txt");

            string inputFilePath = Path.Combine(testDir, "input.txt");
            File.WriteAllText(inputFilePath, "dummy input file");

            string converterFilePath = Path.Combine(testDir, "converter.bat");
            File.WriteAllText(converterFilePath,
@"
set argC=0
for %%x in (%*) do Set /A argC+=1

echo Converter called with %argC% args
echo success > """ + outputFilePath + @"""");

            // Act
            bool success = CoverageReportConverter.ConvertBinaryToXml(converterFilePath, inputFilePath, outputFilePath, logger);

            // Assert
            Assert.IsTrue(success, "Expecting the process to succeed");

            logger.AssertMessageLogged("Converter called with 3 args");
        }

        #endregion
    }
}
