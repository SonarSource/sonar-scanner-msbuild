/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

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
