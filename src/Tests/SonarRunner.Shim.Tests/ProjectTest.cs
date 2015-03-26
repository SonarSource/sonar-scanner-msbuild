//-----------------------------------------------------------------------
// <copyright file="ProjectTest.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarRunner.Shim.Tests
{
    [TestClass]
    public class ProjectTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Project()
        {
            string baseDir = TestUtils.CreateTestSpecificFolder(TestContext, "ProjectTest_BaseDir");
            string otherDir = TestUtils.CreateTestSpecificFolder(TestContext, "ProjectTest_OtherDir");

            var testProject = File.Create(Path.Combine(baseDir, "Test.csproj")).Name;
            var foo = File.Create(Path.Combine(baseDir, "Foo.cs")).Name;
            var bar = File.Create(Path.Combine(baseDir, "Bar.cs")).Name;
            var baz = File.Create(Path.Combine(otherDir, "Baz.cs")).Name;

            List<string> files = new List<string>();
            files.Add(foo);
            files.Add(bar);
            files.Add(baz);
            Project project = new Project("test", Guid.Parse("DB2E5521-3172-47B9-BA50-864F12E6DFFF"), testProject, true, files, @"C:\fxcop-report.xml", @"C:\visualstudio-coverage.xml");

            Assert.AreEqual("test", project.Name);
            Assert.AreEqual(Guid.Parse("DB2E5521-3172-47B9-BA50-864F12E6DFFF"), project.Guid);
            Assert.AreEqual(testProject, project.MsBuildProject);
            Assert.AreEqual(true, project.IsTest);
            Assert.AreSame(files, project.Files);
            Assert.AreEqual(@"C:\fxcop-report.xml", project.FxCopReport);
            Assert.AreEqual(@"C:\visualstudio-coverage.xml", project.VisualStudioCodeCoverageReport);

            Assert.AreEqual("DB2E5521-3172-47B9-BA50-864F12E6DFFF", project.GuidAsString());
            Assert.AreEqual(baseDir, project.BaseDir());

            var filesInBaseDir = project.FilesToAnalyze();
            Assert.AreEqual(2, filesInBaseDir.Count);
            Assert.AreEqual(foo, filesInBaseDir[0]);
            Assert.AreEqual(bar, filesInBaseDir[1]);
        }
    }
}
