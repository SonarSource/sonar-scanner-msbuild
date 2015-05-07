//-----------------------------------------------------------------------
// <copyright file="ConsoleLogger.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Globalization;

namespace SonarQube.Common
{
    /// <summary>
    /// Simple logger implementation that logs output to the console
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        #region Public methods

        public ConsoleLogger() : this(includeTimestamp: false)
        {
        }

        public ConsoleLogger(bool includeTimestamp)
        {
            this.IncludeTimestamp = includeTimestamp;
        }

        /// <summary>
        /// Indicates whether logged messages should be prefixed with timestamps or not
        /// </summary>
        public bool IncludeTimestamp { get; private set; }

        #endregion

        #region ILogger interface

        public void LogMessage(string message, params object[] args)
        {
            string finalMessage = this.GetFormattedMessage(message, args);
            Console.WriteLine(finalMessage);
        }

        public void LogWarning(string message, params object[] args)
        {
            string finalMessage = this.GetFormattedMessage(Resources.Logger_WarningPrefix + message, args);
            Console.WriteLine(finalMessage);
        }

        public void LogError(string message, params object[] args)
        {
            string finalMessage = this.GetFormattedMessage(message, args);
            Console.Error.WriteLine(finalMessage);
        }

        #endregion

        #region Private methods

        private string GetFormattedMessage(string message, params object[] args)
        {
            string finalMessage = message;
            if (args != null && args.Length > 0)
            {
                finalMessage = string.Format(CultureInfo.CurrentCulture, finalMessage ?? string.Empty, args);
            }

            if (this.IncludeTimestamp)
            {
                finalMessage = string.Format(CultureInfo.CurrentCulture, "{0}  {1}", System.DateTime.Now.ToString("T", CultureInfo.CurrentCulture), finalMessage);
            }
            return finalMessage;
        }

        #endregion
    }
}
