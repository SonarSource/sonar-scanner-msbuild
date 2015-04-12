//-----------------------------------------------------------------------
// <copyright file="TestLogger.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public void AssertErrorsLogged()
        {
            Assert.IsTrue(this.Errors.Count > 0, "Expecting at least one error to be logged");
        }

        public void AssertMessagesLogged()
        {
            Assert.IsTrue(this.Messages.Count > 0, "Expecting at least one message to be logged");
        }

        public void AssertErrorsLogged(int expectedCount)
        {
            Assert.AreEqual(expectedCount, this.Errors.Count, "Unexpected number of errors logged");
        }

        public void AssertWarningsLogged(int expectedCount)
        {
            Assert.AreEqual(expectedCount, this.Warnings.Count, "Unexpected number of warnings logged");
        }

        public void AssertMessageLogged(string expected)
        {
            bool found = this.Messages.Any(s => expected.Equals(s, System.StringComparison.InvariantCulture));
            Assert.IsTrue(found, "Expected message was not found: '{0}'", expected);
        }

        public void AssertErrorLogged(string expected)
        {
            bool found = this.Errors.Any(s => expected.Equals(s, System.StringComparison.InvariantCulture));
            Assert.IsTrue(found, "Expected error was not found: '{0}'", expected);
        }

        public void AssertMessageNotLogged(string message)
        {
            bool found = this.Messages.Any(s => message.Equals(s, System.StringComparison.InvariantCulture));
            Assert.IsFalse(found, "Not expecting the message to have been logged: '{0}'", message);
        }

        /// <summary>
        /// Checks that a warning exists that contains all of the specified strings
        /// </summary>
        public void AssertWarningExists(params string[] expected)
        {
            IEnumerable<string> matches = this.Warnings.Where(w => expected.All(e => w.Contains(e)));
            Assert.AreNotEqual(0, matches.Count(), "No warning contains the expected strings: {0}", string.Join(",", expected));
            Assert.AreEqual(1, matches.Count(), "More than one warning contains the expected strings: {0}", string.Join(",", expected));
        }


        #endregion

        #region ILogger interface

        public void LogMessage(string message, params object[] args)
        {
            Messages.Add(GetFormattedMessage(message, args));
            Console.WriteLine("MESSAGE: ", message, args);
        }

        public void LogWarning(string message, params object[] args)
        {
            Warnings.Add(GetFormattedMessage(message, args));
            Console.WriteLine("WARNING: ", message, args);
        }

        public void LogError(string message, params object[] args)
        {
            Errors.Add(GetFormattedMessage(message, args));
            Console.WriteLine("ERROR: ", message, args);
        }

        #endregion

        private string GetFormattedMessage(string message, params object[] args)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, message, args);
        }
    }
}
