//-----------------------------------------------------------------------
// <copyright file="OutputRecorder.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.Common.UnitTests
{

    /// <summary>
    /// Test implementation of <see cref="IOutputWriter"/> that records the output messages
    /// </summary>
    internal class OutputRecorder : IOutputWriter
    {
        private class OutputMessage
        {
            private readonly string message;
            private readonly ConsoleColor textColor;
            private readonly bool isError;

            public OutputMessage(string message, ConsoleColor textColor, bool isError)
            {
                this.message = message;
                this.textColor = textColor;
                this.isError = isError;
            }

            public string Message { get { return this.message; } }
            public ConsoleColor TextColor { get { return this.textColor; } }
            public bool IsError { get { return this.isError; } }
        }

        private readonly List<OutputMessage> outputMessages = new List<OutputMessage>();

        #region Checks

        public void AssertNoOutput()
        {
            Assert.AreEqual(0, this.outputMessages.Count, "Not expecting any output to have been written to the console");
        }

        public void AssertExpectedLastOutput(string message, ConsoleColor textColor, bool isError)
        {
            Assert.IsTrue(this.outputMessages.Any(), "Expecting some output to have been written to the console");

            OutputMessage lastMessage = this.outputMessages.Last();

            Assert.AreEqual(message, lastMessage.Message, "Unexpected message content");
            Assert.AreEqual(textColor, lastMessage.TextColor, "Unexpected text color");
            Assert.AreEqual(isError, lastMessage.IsError, "Unexpected output stream");
        }

        public void AssertExpectedOutputText(params string[] messages)
        {
            CollectionAssert.AreEqual(messages, this.outputMessages.Select(om => om.Message).ToArray(), "Unexpected output messages");
        }

        #endregion

        #region IOutputWriter methods

        public void WriteLine(string message, ConsoleColor textColor, bool isError)
        {
            this.outputMessages.Add(new OutputMessage(message, textColor, isError));

            // Dump to the console to assist debugging
            Console.WriteLine("IsError: {0}, TextColor: {1}, Message: {2}", isError, textColor.ToString(), message);
        }

        #endregion
    }
}
