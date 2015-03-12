//-----------------------------------------------------------------------
// <copyright file="TestLogger.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sonar.Common;

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

        private string GetFormattedMessage(string message, params object[] args)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, message, args);
        }
    }
}
