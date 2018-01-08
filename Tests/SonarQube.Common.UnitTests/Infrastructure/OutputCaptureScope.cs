/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarQube.Common.UnitTests
{
    /// <summary>
    /// Utility class to capture the standard console output and error streams.
    /// Disposing the class resets the console to use the standard streams.
    /// </summary>
    /// <remarks>Note: attempts to set Console.ForegroundColor and Console.BackgroundColor
    /// don't work if the console output is being redirected, so we have no easy way of
    /// capturing the colour in which the text is written.</remarks>
    public sealed class OutputCaptureScope : IDisposable
    {
        private StringWriter outputWriter;
        private StringWriter errorWriter;

        public OutputCaptureScope()
        {
            outputWriter = new StringWriter();
            Console.SetOut(outputWriter);

            errorWriter = new StringWriter();
            Console.SetError(errorWriter);
        }

        public string GetLastErrorMessage()
        {
            return GetLastMessage(errorWriter);
        }

        public string GetLastOutputMessage()
        {
            return GetLastMessage(outputWriter);
        }

        #region Assertions

        public void AssertExpectedLastMessage(string expected)
        {
            var lastMessage = GetLastMessage(outputWriter);
            Assert.AreEqual(expected, lastMessage, "Expected message was not logged");
        }

        public void AssertLastMessageEndsWith(string expected)
        {
            var lastMessage = GetLastMessage(outputWriter);

            Assert.IsTrue(lastMessage.EndsWith(expected, StringComparison.CurrentCulture), "Message does not end with the expected string: '{0}'", lastMessage);
            Assert.IsTrue(lastMessage.Length > expected.Length, "Expecting the message to be prefixed with timestamp text");
        }

        public void AssertExpectedLastError(string expected)
        {
            var last = GetLastMessage(errorWriter);
            Assert.AreEqual(expected, last, "Expected error was not logged");
        }

        public void AssertLastErrorEndsWith(string expected)
        {
            var last = GetLastMessage(errorWriter);

            Assert.IsTrue(last.EndsWith(expected, StringComparison.CurrentCulture), "Error does not end with the expected string: '{0}'", last);
            Assert.IsTrue(last.Length > expected.Length, "Expecting the error to be prefixed with timestamp text");
        }

        #endregion Assertions

        #region IDisposable implementation

        private bool disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;

                var standardError = new StreamWriter(Console.OpenStandardError())
                {
                    AutoFlush = true
                };
                Console.SetError(standardError);

                var standardOut = new StreamWriter(Console.OpenStandardOutput())
                {
                    AutoFlush = true
                };
                Console.SetOut(standardOut);

                outputWriter.Dispose();
                outputWriter = null;

                errorWriter.Dispose();
                errorWriter = null;
            }
        }

        #endregion IDisposable implementation

        #region Private methods

        private static string GetLastMessage(StringWriter writer)
        {
            writer.Flush();
            var allText = writer.GetStringBuilder().ToString();
            var lines = allText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            Assert.IsTrue(lines.Length > 1, "No output written");

            // There will always be at least one entry in the array, even in an empty string.
            // The last line should be an empty string that follows the final new line character.
            Assert.AreEqual(string.Empty, lines[lines.Length - 1], "Test logic error: expecting the last array entry to be an empty string");

            return lines[lines.Length - 2];
        }

        #endregion Private methods
    }
}
