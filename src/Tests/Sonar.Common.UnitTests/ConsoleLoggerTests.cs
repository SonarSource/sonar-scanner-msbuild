//-----------------------------------------------------------------------
// <copyright file="ConsoleLoggerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Sonar.Common.UnitTests
{
    [TestClass]
    public class ConsoleLoggerTests
    {
        #region Tests

        [TestMethod]
        [Description("Regression test: checks the logger does not fail on null message")]
        public void CLogger_NoExceptionOnNullMessage()
        {
            // 1. Logger without timestamps
            ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogMessage(null);
            logger.LogMessage(null, null);
            logger.LogMessage(null, "abc");

            logger.LogWarning(null);
            logger.LogWarning(null, null);
            logger.LogWarning(null, "abc");

            logger.LogError(null);
            logger.LogError(null, null);
            logger.LogError(null, "abc");

            // 2. Logger without timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogMessage(null);
            logger.LogMessage(null, null);
            logger.LogMessage(null, "abc");

            logger.LogWarning(null);
            logger.LogWarning(null, null);
            logger.LogWarning(null, "abc");

            logger.LogError(null);
            logger.LogError(null, null);
            logger.LogError(null, "abc");

        }

        [TestMethod]
        [Description("Regression test: checks the logger does not fail on null arguments")]
        public void CLogger_NoExceptionOnNullArgs()
        {
            // 1. Logger without timestamps
            ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogMessage(null, null);
            logger.LogMessage("123", null);

            logger.LogWarning(null, null);
            logger.LogWarning("123", null);

            logger.LogError(null, null);
            logger.LogError("123", null);

            // 2. Logger without timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogMessage(null, null);
            logger.LogMessage("123", null);

            logger.LogWarning(null, null);
            logger.LogWarning("123", null);

            logger.LogError(null, null);
            logger.LogError("123", null);
        }

        [TestMethod]
        public void CLogger_ExpectedMessages_Message()
        {
            StringWriter writer = new StringWriter();
            Console.SetOut(writer);

            // 1. Logger without timestamps
            ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogMessage("message1");
            AssertExpectedLastMessage("message1", writer);

            logger.LogMessage("message2", null);
            AssertExpectedLastMessage("message2", writer);

            logger.LogMessage("message3 {0}", "xxx");
            AssertExpectedLastMessage("message3 xxx", writer);

            // 2. Logger with timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogMessage("message4");
            AssertLastMessageEndsWith("message4", writer);

            logger.LogMessage("message5{0}{1}", null, null);
            AssertLastMessageEndsWith("message5", writer);

            logger.LogMessage("message6 {0}{1}", "xxx", "yyy", "zzz");
            AssertLastMessageEndsWith("message6 xxxyyy", writer);
        }

        [TestMethod]
        public void CLogger_ExpectedMessages_Warning()
        {
            // NOTE: we expect all warnings to be prefixed with a localised
            // "WARNING" prefix, so we're using "AssertLastMessageEndsWith"
            // even for warnings that do not have timestamps.

            StringWriter writer = new StringWriter();
            Console.SetError(writer);

            // 1. Logger without timestamps
            ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogWarning("warn1");
            AssertLastMessageEndsWith("warn1", writer);

            logger.LogWarning("warn2", null);
            AssertLastMessageEndsWith("warn2", writer);

            logger.LogWarning("warn3 {0}", "xxx");
            AssertLastMessageEndsWith("warn3 xxx", writer);

            // 2. Logger with timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogWarning("warn4");
            AssertLastMessageEndsWith("warn4", writer);

            logger.LogWarning("warn5{0}{1}", null, null);
            AssertLastMessageEndsWith("warn5", writer);

            logger.LogWarning("warn6 {0}{1}", "xxx", "yyy", "zzz");
            AssertLastMessageEndsWith("warn6 xxxyyy", writer);
        }

        [TestMethod]
        public void CLogger_ExpectedMessages_Error()
        {
            StringWriter writer = new StringWriter();
            Console.SetError(writer);

            // 1. Logger without timestamps
            ConsoleLogger logger = new ConsoleLogger(includeTimestamp: false);

            logger.LogError("simple error1");
            AssertExpectedLastMessage("simple error1", writer);

            logger.LogError("simple error2", null);
            AssertExpectedLastMessage("simple error2", writer);

            logger.LogError("simple error3 {0}", "xxx");
            AssertExpectedLastMessage("simple error3 xxx", writer);

            // 2. Logger with timestamps
            logger = new ConsoleLogger(includeTimestamp: true);

            logger.LogError("simple error4");
            AssertLastMessageEndsWith("simple error4", writer);

            logger.LogError("simple error5{0}{1}", null, null);
            AssertLastMessageEndsWith("simple error5", writer);

            logger.LogError("simple error6 {0}{1}", "xxx", "yyy", "zzz");
            AssertLastMessageEndsWith("simple error6 xxxyyy", writer);
        }

        #endregion

        #region Assertions

        private void AssertExpectedLastMessage(string expected, StringWriter writer)
        {
            string lastMessage = GetLastMessage(writer);
            Assert.AreEqual(expected, lastMessage, "Expected message was not logged");
        }

        private void AssertLastMessageEndsWith(string expected, StringWriter writer)
        {
            string lastMessage = GetLastMessage(writer);

            Assert.IsTrue(lastMessage.EndsWith(expected, StringComparison.CurrentCulture), "Message does not end with the expected string: '{0}'", lastMessage);
            Assert.IsTrue(lastMessage.Length > expected.Length, "Expecting the message to be prefixed with timestamp text");
        }

        private static string GetLastMessage(StringWriter writer)
        {
            writer.Flush();
            string allText = writer.GetStringBuilder().ToString();
            string[] lines = allText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            // There will always be at least one entry in the array, even in an empty string.
            // The last line should be an empty string that follows the final new line character.
            Assert.AreEqual(string.Empty, lines[lines.Length - 1], "Test logic error: expecting the last array entry to be an empty string");

            return lines[lines.Length - 2];
        }

        #endregion
    }
}
