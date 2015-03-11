//-----------------------------------------------------------------------
// <copyright file="ConsoleLogger.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Sonar.Common
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
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentNullException("message");
            }

            message = this.GetFormattedMessage(message, args);
            Console.WriteLine(message);
        }

        public void LogWarning(string message, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentNullException("message");
            }

            message = this.GetFormattedMessage("WARNING: " + message, args);
            Console.WriteLine(message);
        }


        public void LogError(string message, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentNullException("message");
            }

            message = this.GetFormattedMessage(message, args);
            Console.Error.WriteLine(message);
        }

        #endregion

        #region Private methods

        private string GetFormattedMessage(string message, params object[] args)
        {
            message = string.Format(System.Globalization.CultureInfo.CurrentCulture, message, args);

            if (this.IncludeTimestamp)
            {
                message = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}: {1}", System.DateTime.Now.ToString("hh:mm:ss"), message);
            }
            return message;
        }

        #endregion
    }
}
