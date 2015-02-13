using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarProjectPropertiesGenerator.Tests
{
    [TestClass]
    public class ProjectTest
    {
        [TestMethod]
        public void Project()
        {
            List<string> files = new List<string>();
            files.Add(@"C:\Test\Foo.cs");
            files.Add(@"C:\Test\Bar.cs");
            Project project = new Project("test", Guid.Parse("DB2E5521-3172-47B9-BA50-864F12E6DFFF"), @"C:\Test\Test.csproj", true, files);

            Assert.AreEqual("test", project.Name);
            Assert.AreEqual(Guid.Parse("DB2E5521-3172-47B9-BA50-864F12E6DFFF"), project.Guid);
            Assert.AreEqual(@"C:\Test\Test.csproj", project.MsBuildProject);
            Assert.AreEqual(true, project.IsTest);
            Assert.AreSame(files, project.Files);

            Assert.AreEqual("DB2E5521-3172-47B9-BA50-864F12E6DFFF", project.GuidAsString());
            Assert.AreEqual(@"C:\Test", project.BaseDir());
        }
    }
}
