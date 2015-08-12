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
        private const LoggerVerbosity DefaultVerbosity = LoggerVerbosity.Info;

        #region Public methods

        public ConsoleLogger() : this(includeTimestamp: false)
        {
        }

        public ConsoleLogger(bool includeTimestamp)
        {
            this.IncludeTimestamp = includeTimestamp;
            this.Verbosity = DefaultVerbosity;
        }

        /// <summary>
        /// Indicates whether logged messages should be prefixed with timestamps or not
        /// </summary>
        public bool IncludeTimestamp { get; private set; }

        #endregion Public methods

        #region ILogger interface

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

        public void LogDebug(string message, params object[] args)
        {
            LogMessage(LoggerVerbosity.Debug, message, args);
        }

        public void LogInfo(string message, params object[] args)
        {
            LogMessage(LoggerVerbosity.Info, message, args);
        }

        public LoggerVerbosity Verbosity
        {
            get; set;
        }

        #endregion ILogger interface

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

        private void LogMessage(LoggerVerbosity messageVerbosity, string message, params object[] args)
        {
            if ((messageVerbosity == LoggerVerbosity.Info || 
                (messageVerbosity == LoggerVerbosity.Debug && this.Verbosity == LoggerVerbosity.Debug)))
            {
                string finalMessage = this.GetFormattedMessage(message, args);
                Console.WriteLine(finalMessage);
            }
        }

        #endregion Private methods
    }
}