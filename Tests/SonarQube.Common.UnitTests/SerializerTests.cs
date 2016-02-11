//-----------------------------------------------------------------------
// <copyright file="SerializerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class SerializerTests
    {
        public TestContext TestContext { get; set; }

        public class MyDataClass
        {
            public string Value1 { get; set; }
            public int Value2 { get; set; }
        }

        #region Tests

        [TestMethod]
        public void Serializer_ArgumentValidation()
        {
            // Load
            AssertException.Expects<ArgumentNullException>(() => Serializer.LoadModel<MyDataClass>(null));

            // Save
            AssertException.Expects<ArgumentNullException>(() => Serializer.SaveModel<MyDataClass>(null, "c:\\data.txt"));
            AssertException.Expects<ArgumentNullException>(() => Serializer.SaveModel<MyDataClass>(new MyDataClass(), null));

            // ToString
            AssertException.Expects<ArgumentNullException>(() => Serializer.ToString<MyDataClass>(null));
        }

        [TestMethod]
        public void Serializer_RoundTrip_Succeeds()
        {
            // Arrange
            string testDir = TestUtils.CreateTestSpecificFolder(this.TestContext);
            string filePath = Path.Combine(testDir, "file1.txt");

            MyDataClass original = new MyDataClass() { Value1 = "val1", Value2 = 22 };


            // Act - save and reload
            Serializer.SaveModel(original, filePath);
            MyDataClass reloaded = Serializer.LoadModel<MyDataClass>(filePath);

            // Assert
            Assert.IsNotNull(reloaded);
            Assert.AreEqual(original.Value1, reloaded.Value1);
            Assert.AreEqual(original.Value2, reloaded.Value2);
        }

        [TestMethod]
        public void Serializer_ToString_Succeeds()
        {
            // Arrange
            MyDataClass inputData = new MyDataClass() { Value1 = "val1", Value2 = 22 };

            // Act
            string actual = Serializer.ToString(inputData);

            // Assert
            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>
<MyDataClass xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <Value1>val1</Value1>
  <Value2>22</Value2>
</MyDataClass>";

            Assert.AreEqual(expected, actual);
        }

        #endregion

    }
}
