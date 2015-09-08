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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace SonarQube.Common
{
    /// <summary>
    /// Simple logger implementation that logs output to the console
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private enum MessageType
        {
            Info,
            Debug,
            Warning,
            Error
        }

        private class Message
        {
            private readonly MessageType messageType;
            private readonly string finalMessage;

            public Message(MessageType messageType, string finalMessage)
            {
                this.messageType = messageType;
                this.finalMessage = finalMessage;
            }

            public MessageType MessageType { get; }
            public string FinalMessage { get; }
        }

        public const ConsoleColor DebugColor = ConsoleColor.DarkCyan;
        public const ConsoleColor WarningColor = ConsoleColor.Yellow;
        public const ConsoleColor ErrorColor = ConsoleColor.Red;

        private const LoggerVerbosity DefaultVerbosity = VerbosityCalculator.InitialLoggingVerbosity;

        private bool isOutputSuspended = false;

        /// <summary>
        /// List of messages that have not been output to the console
        /// </summary>
        private IList<Message> suspendedMessages;

        private readonly IOutputWriter outputWriter;

        #region Public methods

        /// <summary>
        /// Use only for testing
        public static ConsoleLogger CreateLoggerForTesting(bool includeTimestamp, IOutputWriter writer)
        {
            return new ConsoleLogger(includeTimestamp, writer);
        }

        public ConsoleLogger(bool includeTimestamp)
            : this(includeTimestamp, new ConsoleWriter())
        {
        }

        private ConsoleLogger(bool includeTimestamp, IOutputWriter writer)
        {
            this.IncludeTimestamp = includeTimestamp;
            this.Verbosity = DefaultVerbosity;
            this.outputWriter = writer;
        }

        /// <summary>
        /// Indicates whether logged messages should be prefixed with timestamps or not
        /// </summary>
        public bool IncludeTimestamp { get; set; }

        public void SuspendOutput()
        {
            if (!this.isOutputSuspended)
            {
                this.isOutputSuspended = true;
                this.suspendedMessages = new List<Message>();
            }
        }

        public void ResumeOutput()
        {
            if (this.isOutputSuspended)
            {
                this.FlushOutput();
            }
        }

        #endregion Public methods

        #region ILogger interface

        public void LogWarning(string message, params object[] args)
        {
            string finalMessage = this.GetFormattedMessage(Resources.Logger_WarningPrefix + message, args);

            this.Write(MessageType.Warning, finalMessage);
        }

        public void LogError(string message, params object[] args)
        {
            this.Write(MessageType.Error, message, args);
        }

        public void LogDebug(string message, params object[] args)
        {
            if (this.Verbosity == LoggerVerbosity.Debug)
            {
                this.Write(MessageType.Debug, message, args);
            }
        }

        public void LogInfo(string message, params object[] args)
        {
            this.Write(MessageType.Info, message, args);
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
                finalMessage = string.Format(CultureInfo.CurrentCulture, "{0}  {1}", System.DateTime.Now.ToString("HH:mm:ss.FFF", CultureInfo.InvariantCulture), finalMessage);
            }
            return finalMessage;
        }

        private void FlushOutput()
        {
            Debug.Assert(this.isOutputSuspended, "Not expecting FlushOutput to be called unless output is currently suspended");
            Debug.Assert(this.suspendedMessages != null);

            this.isOutputSuspended = false;

            foreach(Message message in this.suspendedMessages)
            {
                this.Write(message.MessageType, message.FinalMessage);
            }

            this.suspendedMessages = null;
        }

        /// <summary>
        /// Either writes the message to the output stream, or records it
        /// if output is currently suspended
        /// </summary>
        private void Write(MessageType messageType, string message, params object[] args)
        {
            string finalMessage = this.GetFormattedMessage(message, args);

            if (this.isOutputSuspended)
            {
                this.suspendedMessages.Add(new Message(messageType, finalMessage));
            }
            else
            {
                ConsoleColor textColor = GetConsoleColor(messageType);

                Debug.Assert(this.outputWriter != null, "OutputWriter should not be null");
                this.outputWriter.WriteLine(finalMessage, textColor, messageType == MessageType.Error);
            }
        }

        private ConsoleColor GetConsoleColor(MessageType messageType)
        {

            switch (messageType)
            {
                case MessageType.Debug:
                    return DebugColor;

                case MessageType.Warning:
                    return WarningColor;

                case MessageType.Error:
                    return ErrorColor;

                default:
                    return Console.ForegroundColor;
            }
        }

        #endregion Private methods
    }
}