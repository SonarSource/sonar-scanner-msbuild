//-----------------------------------------------------------------------
// <copyright file="ProcessedArgsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class ProcessedArgsTests
    {
        [TestMethod]
        public void ProcArgs_GetSetting()
        {
            // 0. Setup
            ListPropertiesProvider cmdLineProps = new ListPropertiesProvider();
            cmdLineProps.AddProperty("cmd.key.1", "cmd value 1");
            cmdLineProps.AddProperty("shared.key.1", "shared cmd value");

            ListPropertiesProvider fileProps = new ListPropertiesProvider();
            fileProps.AddProperty("file.key.1", "file value 1");
            fileProps.AddProperty("shared.key.1", "shared file value");

            ProcessedArgs args = new ProcessedArgs("key", "name", "ver", cmdLineProps, fileProps);

            // 1. Throws on missing value
            AssertException.Expects<InvalidOperationException>(() => args.GetSetting("missing.property"));

            // 2. Returns existing values
            Assert.AreEqual("cmd value 1", args.GetSetting("cmd.key.1"));
            Assert.AreEqual("file value 1", args.GetSetting("file.key.1"));

            // 3. Precedence - command line properties should win
            Assert.AreEqual("shared cmd value", args.GetSetting("shared.key.1"));
        }

        [TestMethod]
        public void ProcArgs_TryGetSetting()
        {
            // 0. Setup
            ListPropertiesProvider cmdLineProps = new ListPropertiesProvider();
            cmdLineProps.AddProperty("cmd.key.1", "cmd value 1");
            cmdLineProps.AddProperty("shared.key.1", "shared cmd value");

            ListPropertiesProvider fileProps = new ListPropertiesProvider();
            fileProps.AddProperty("file.key.1", "file value 1");
            fileProps.AddProperty("shared.key.1", "shared file value");

            ProcessedArgs args = new ProcessedArgs("key", "name", "ver", cmdLineProps, fileProps);

            // 1. Missing key -> null
            string result;
            Assert.IsFalse(args.TryGetSetting("missing.property", out result), "Expecting false when the specified key does not exist");
            Assert.IsNull(result, "Expecting the value to be null when the specified key does not exist");

            // 2. Returns existing values
            Assert.IsTrue(args.TryGetSetting("cmd.key.1", out result));
            Assert.AreEqual("cmd value 1", result);

            // 3. Precedence - command line properties should win
            Assert.AreEqual("shared cmd value", args.GetSetting("shared.key.1"));
        }

        [TestMethod]
        public void ProcArgs_GetSettingOrDefault()
        {
            // 0. Setup
            ListPropertiesProvider cmdLineProps = new ListPropertiesProvider();
            cmdLineProps.AddProperty("cmd.key.1", "cmd value 1");
            cmdLineProps.AddProperty("shared.key.1", "shared cmd value");

            ListPropertiesProvider fileProps = new ListPropertiesProvider();
            fileProps.AddProperty("file.key.1", "file value 1");
            fileProps.AddProperty("shared.key.1", "shared file value");

            ProcessedArgs args = new ProcessedArgs("key", "name", "ver", cmdLineProps, fileProps);

            // 1. Missing key -> default returned
            string result = args.GetSetting("missing.property", "default value");
            Assert.AreEqual("default value", result);

            // 2. Returns existing values
            result = args.GetSetting("file.key.1", "default value");
            Assert.AreEqual("file value 1", result);

            // 3. Precedence - command line properties should win
            Assert.AreEqual("shared cmd value", args.GetSetting("shared.key.1", "default ValueType"));
        }

    }
}
