using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarProjectPropertiesGenerator.Tests
{
    [TestClass]
    public class ProjecLoadertTest
    {
        [TestMethod]
        public void ProjectLoader()
        {
            List<Project> projects = SonarProjectPropertiesGenerator.ProjectLoader.LoadFrom(@"ProjectLoaderTest\");

            Assert.AreEqual(2, projects.Count);

            var product = projects.Where(p => "ProductProject".Equals(p.Name)).Single();
            Assert.AreEqual("ProductProject", product.Name);
            Assert.AreEqual(Guid.Parse("DB2E5521-3172-47B9-BA50-864F12E6DFFF"), product.Guid);
            Assert.AreEqual(@"C:\ProductProject\ProductProject.csproj", product.MsBuildProject);
            Assert.AreEqual(false, product.IsTest);
            Assert.AreEqual(1, product.Files.Count());
            Assert.AreEqual(@"C:\ProductProject\File1.cs", product.Files[0]);

            var test = projects.Where(p => "TestProject".Equals(p.Name)).Single();
            Assert.AreEqual("TestProject", test.Name);
            Assert.AreEqual(Guid.Parse("DA0FCD82-9C5C-4666-9370-C7388281D49B"), test.Guid);
            Assert.AreEqual(@"C:\TestProject\TestProject.csproj", test.MsBuildProject);
            Assert.AreEqual(true, test.IsTest);
            Assert.AreEqual(2, test.Files.Count());
            Assert.AreEqual(@"C:\TestProject\File1.cs", test.Files[0]);
            Assert.AreEqual(@"C:\TestProject\File2.cs", test.Files[1]);
        }
    }
}
