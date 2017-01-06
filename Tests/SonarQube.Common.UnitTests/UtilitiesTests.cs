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

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class UtilitiesTests
    {
        [TestMethod]
        public void VersionDisplayString()
        {
            CheckVersionString("1.2.0.0", "1.2");
            CheckVersionString("1.0.0.0", "1.0");
            CheckVersionString("0.0.0.0", "0.0");
            CheckVersionString("1.2.3.0", "1.2.3");

            CheckVersionString("1.2.0.4", "1.2.0.4");
            CheckVersionString("1.2.3.4", "1.2.3.4");
            CheckVersionString("0.2.3.4", "0.2.3.4");
            CheckVersionString("0.0.3.4", "0.0.3.4");
        }

        private static void CheckVersionString(string version, string expectedDisplayString)
        {
            Version actualVersion = new Version(version);
            string actualVersionString = actualVersion.ToDisplayString();

            Assert.AreEqual(expectedDisplayString, actualVersionString);
        }
    }
}
