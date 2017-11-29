/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;

namespace SonarQube.TeamBuild.Integration.Tests.Infrastructure
{
    internal class MockReportConverter : ICoverageReportConverter
    {
        private int convertCallCount;

        #region Test helpers

        public bool CanConvert { get; set; }

        public Func<string, string, ILogger, bool> ConversionOp { get; set; }

        public bool ConversionResult { get; set; }

        #endregion Test helpers

        #region Assertions

        public void AssertExpectedNumberOfConversions(int expected)
        {
            Assert.AreEqual(expected, convertCallCount, "ConvertToXml called an unexpected number of times");
        }

        public void AssertConvertNotCalled()
        {
            Assert.AreEqual(0, convertCallCount, "Not expecting ConvertToXml to have been called");
        }

        #endregion Assertions

        #region ICoverageReportConverter interface

        bool ICoverageReportConverter.Initialize(ILogger logger)
        {
            Assert.IsNotNull(logger, "Supplied logger should not be null");

            return CanConvert;
        }

        bool ICoverageReportConverter.ConvertToXml(string fullBinaryFileName, string fullXmlFileName, ILogger logger)
        {
            Assert.IsNotNull(logger, "Supplied logger should not be null");

            convertCallCount++;

            if (ConversionOp != null)
            {
                return ConversionOp(fullBinaryFileName, fullBinaryFileName, logger);
            }

            return true;
        }

        #endregion ICoverageReportConverter interface
    }
}
