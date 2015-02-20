using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarProjectPropertiesGenerator.Tests
{
    [TestClass]
    public class PropertiesWriterTest
    {
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
            List<string> productFiles = new List<string>();
            productFiles.Add(@"C:\MyProduct\File1.cs");
            productFiles.Add(@"C:\MyProduct\你好.cs");
            productFiles.Add(@"C:\Somewhere\Foo.cs");
            Project product = new Project("你好", Guid.Parse("DB2E5521-3172-47B9-BA50-864F12E6DFFF"), @"C:\MyProduct\MyProduct.csproj", false, productFiles);

            List<string> testFiles = new List<string>();
            testFiles.Add(@"C:\MyTest\File1.cs");
            Project test = new Project("my_test_project", Guid.Parse("DA0FCD82-9C5C-4666-9370-C7388281D49B"), @"C:\MyTest\MyTest.csproj", true, testFiles);

            List<Project> projects = new List<Project>();
            projects.Add(product);
            projects.Add(test);

            string actual = SonarProjectPropertiesGenerator.PropertiesWriter.ToString("my_project_key", "my_project_name", "1.0", projects);

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
            expected.AppendLine(@"DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.projectBaseDir=C:\\MyProduct");
            expected.AppendLine(@"DB2E5521-3172-47B9-BA50-864F12E6DFFF.sonar.sources=\");
            expected.AppendLine(@"C:\\MyProduct\\File1.cs,\");
            expected.AppendLine(@"C:\\MyProduct\\\u4F60\u597D.cs");
            expected.AppendLine();

            expected.AppendLine("DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectKey=my_project_key:DA0FCD82-9C5C-4666-9370-C7388281D49B");
            expected.AppendLine("DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectName=my_test_project");
            expected.AppendLine(@"DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.projectBaseDir=C:\\MyTest");
            expected.AppendLine("DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.sources=");
            expected.AppendLine(@"DA0FCD82-9C5C-4666-9370-C7388281D49B.sonar.tests=\");
            expected.AppendLine(@"C:\\MyTest\\File1.cs");
            expected.AppendLine();

            Assert.AreEqual(expected.ToString(), actual);
        }
    }
}
