//-----------------------------------------------------------------------
// <copyright file="MockReporterConverter.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

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
