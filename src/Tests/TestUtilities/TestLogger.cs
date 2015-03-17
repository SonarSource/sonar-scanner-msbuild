//-----------------------------------------------------------------------
// <copyright file="TestLogger.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sonar.Common;
using System.Collections.Generic;

namespace TestUtilities
{
    public class TestLogger : ILogger
    {
        public List<string> Messages { get; private set; }
        public List<string> Warnings { get; private set; }
        public List<string> Errors { get; private set; }

        public TestLogger()
        {
            Messages = new List<string>();
            Warnings = new List<string>();
            Errors = new List<string>();
        }

        #region Public methods

        public void AssertErrorsLogged(int expectedCount)
        {
            Assert.AreEqual(expectedCount, this.Errors.Count, "Unexpected number of errors logged");
        }

        public void AssertWarningsLogged(int expectedCount)
        {
            Assert.AreEqual(expectedCount, this.Warnings.Count, "Unexpected number of warnings logged");
        }

        #endregion

        #region ILogger interface

        public void LogMessage(string message, params object[] args)
        {
            Messages.Add(GetFormattedMessage(message, args));
        }

        public void LogWarning(string message, params object[] args)
        {
            Warnings.Add(GetFormattedMessage(message, args));
        }

        public void LogError(string message, params object[] args)
        {
            Errors.Add(GetFormattedMessage(message, args));
        }

        #endregion

        private string GetFormattedMessage(string message, params object[] args)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, message, args);
        }
    }
}
