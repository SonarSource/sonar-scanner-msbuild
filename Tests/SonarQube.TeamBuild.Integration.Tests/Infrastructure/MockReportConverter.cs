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
using SonarQube.Common;
using System;

namespace SonarQube.TeamBuild.Integration.Tests.Infrastructure
{
    internal class MockReportConverter : ICoverageReportConverter
    {
        private int convertCallCount;

        #region Test helpers

        public bool CanConvert { get; set; }

        public Func<string, string, ILogger, bool> ConversionOp { get; set; }

        public bool ConversionResult { get; set; }

        #endregion

        #region Assertions

        public void AssertExpectedNumberOfConversions(int expected)
        {
            Assert.AreEqual(expected, this.convertCallCount, "ConvertToXml called an unexpected number of times");
        }

        public void AssertConvertNotCalled()
        {
            Assert.AreEqual(0, this.convertCallCount, "Not expecting ConvertToXml to have been called");
        }

        #endregion

        #region ICoverageReportConverter interface

        bool ICoverageReportConverter.Initialize(ILogger logger)
        {
            Assert.IsNotNull(logger, "Supplied logger should not be null");

            return this.CanConvert;
        }

        bool ICoverageReportConverter.ConvertToXml(string fullBinaryFileName, string fullXmlFileName, ILogger logger)
        {
            Assert.IsNotNull(logger, "Supplied logger should not be null");

            this.convertCallCount++;

            if (this.ConversionOp != null)
            {
                return this.ConversionOp(fullBinaryFileName, fullBinaryFileName, logger);
            }

            return true;
        }

        #endregion
    }
}
