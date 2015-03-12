//-----------------------------------------------------------------------
// <copyright file="PropertiesWriterTest.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarProjectPropertiesGenerator.Tests
{
    [TestClass]
    public class PropertiesWriterTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void PropertiesWriterEscape()
        {
            Assert.AreEqual("foo", SonarProjectPropertiesGenerator.PropertiesWriter.Escape("foo"));
            Assert.AreEqual(@"C:\\File.cs", SonarProjectPropertiesGenerator.PropertiesWriter.Escape(@"C:\File.cs"));
            Assert.AreEqual(@"\u4F60\u597D", SonarProjectPropertiesGenerator.PropertiesWriter.Escape("你好"));
        }

        [TestMethod]
        public void PropertiesWriterToString()
        {
            var productBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_ProductBaseDir");
            var escapedProductBaseDir = productBaseDir.Replace(@"\", @"\\");
            var productProject = File.Create(Path.Combine(productBaseDir, "MyProduct.csproj")).Name;
            var productFile = File.Create(Path.Combine(productBaseDir, "File.cs")).Name;
            var productChineseFile = File.Create(Path.Combine(productBaseDir, "你好.cs")).Name;

            string otherDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_OtherDir");
            var somewhere = File.Create(Path.Combine(otherDir, "Somewhere.cs")).Name;

            List<string> productFiles = new List<string>();
            productFiles.Add(productFile);
            productFiles.Add(productChineseFile);
            productFiles.Add(somewhere);
            Project product = new Project("你好", Guid.Parse("DB2E5521-3172-47B9-BA50-864F12E6DFFF"), productProject, false, productFiles, @"C:\fxcop-report.xml", @"C:\visualstudio-coverage.xml");

            var testBaseDir = TestUtils.CreateTestSpecificFolder(TestContext, "PropertiesWriterTest_TestBaseDir");
            var escapedTestBaseDir = testBaseDir.Replace(@"\", @"\\");
            var testProject = File.Create(Path.Combine(testBaseDir, "MyTest.csproj")).Name;
            var testFile = File.Create(Path.Combine(testBaseDir, "File.cs")).Name;

            List<string> testFiles = new List<string>();
            testFiles.Add(testFile);
            Project test = new Project("my_test_project", Guid.Parse("DA0FCD82-9C5C-4666-9370-C7388281D49B"), testProject, true, testFiles, null, null);

            Project duplicatedProject1 = new Project("duplicated_project_1", Guid.Parse("C53C92C0-0A5A-4F89-A857-2BBD41CB4410"), @"C:\DuplicatedProject1.csproj", false, new List<string>(), null, null);
            Project duplicatedProject2 = new Project("duplicated_project_2", Guid.Parse("C53C92C0-0A5A-4F89-A857-2BBD41CB4410"), @"C:\DuplicatedProject2.csproj", false, new List<string>(), null, null);

            List<Project> projects = new List<Project>();
            projects.Add(product);
            projects.Add(test);
            projects.Add(duplicatedProject1);
            projects.Add(duplicatedProject2);

            var logger = new TestLogger();
            string actual = SonarProjectPropertiesGenerator.PropertiesWriter.ToString(logger, "my_project_key", "my_project_name", "1.0", projects);

            Assert.AreEqual(2, logger.Warnings.Count);
            Assert.AreEqual(@"The project has a non-unique GUID ""C53C92C0-0A5A-4F89-A857-2BBD41CB4410"". Analysis results for this project will not be uploaded to SonarQube. Project file: C:\DuplicatedProject1.csproj", logger.Warnings[0]);
            Assert.AreEqual(@"The project has a non-unique GUID ""C53C92C0-0A5A-4F89-A857-2BBD41CB4410"". Analysis results for this project will not be uploaded to SonarQube. Project file: C:\DuplicatedProject2.csproj", logger.Warnings[1]);

            StringBuilder expected = new StringBuilder();
            expected.AppendLine("sonar.projectKey=my_project_key");
            expected.AppendLine("sonar.projectName=my_project_name");
            expected.AppendLine("sonar.projectVersion=1.0");
            expected.AppendLine();
            expected.AppendLine("# FIXME: Encoding is hardcoded");
            expected.AppendLine("sonar.sourceEncoding=UTF-8");
            expected.AppendLine();
            expected.AppendLine("sonar.modules=DB2E5521-3172-47B9-BA50-864F12E6DFFF,DA0FCD82-9C5C-4666-9370-C7388281D49B");
            expected.AppendLine();

            expected.AppendLine("DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectKey=my_project_key:DB2E5521-3172-47B9-BA50-864F12E6DFFF");
            expected.AppendLine(@"DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectName=\u4F60\u597D");
            expected.AppendLine(@"DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectBaseDir=" + escapedProductBaseDir);
            expected.AppendLine(@"DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.fxcop.reportPath=C:\\fxcop-report.xml");
            expected.AppendLine(@"DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.cs.vscoveragexml.reportsPaths=C:\\visualstudio-coverage.xml");
            expected.AppendLine(@"DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sources=\");
            expected.AppendLine(escapedProductBaseDir + @"\\File.cs,\");
            expected.AppendLine(escapedProductBaseDir + @"\\\u4F60\u597D.cs");
            expected.AppendLine();

            expected.AppendLine("DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectKey=my_project_key:DA0FCD82-9C5C-4666-9370-C7388281D49B");
            expected.AppendLine("DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectName=my_test_project");
            expected.AppendLine(@"DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectBaseDir=" + escapedTestBaseDir);
            expected.AppendLine("DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sources=");
            expected.AppendLine(@"DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.tests=\");
            expected.AppendLine(escapedTestBaseDir + @"\\File.cs");
            expected.AppendLine();

            Assert.AreEqual(expected.ToString(), actual);
        }
    }
}
