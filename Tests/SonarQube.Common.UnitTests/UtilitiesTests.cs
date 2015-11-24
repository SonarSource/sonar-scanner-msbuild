//-----------------------------------------------------------------------
// <copyright file="ProjectInfoTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class UtilitiesTests
    {
        [TestMethod]
        public void VersionDisplayString()
        {
            CheckVersionString("1.2.0.0", "1.2");
            CheckVersionString("1.0.0.0", "1.0");
            CheckVersionString("0.0.0.0", "0.0");
            CheckVersionString("1.2.3.0", "1.2.3");

            CheckVersionString("1.2.0.4", "1.2.0.4");
            CheckVersionString("1.2.3.4", "1.2.3.4");
            CheckVersionString("0.2.3.4", "0.2.3.4");
            CheckVersionString("0.0.3.4", "0.0.3.4");
        }

        private static void CheckVersionString(string version, string expectedDisplayString)
        {
            Version actualVersion = new Version(version);
            string actualVersionString = actualVersion.ToDisplayString();

            Assert.AreEqual(expectedDisplayString, actualVersionString);
        }
    }
}
