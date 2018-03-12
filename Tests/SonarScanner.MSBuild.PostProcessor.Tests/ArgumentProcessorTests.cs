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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PostProcessor.Tests
{
    [TestClass]
    public class ArgumentProcessorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PostArgProc_NoArgs()
        {
            // 0. Setup
            var logger = new TestLogger();
            IAnalysisPropertyProvider provider;

            // 1. Null input
            AssertException.Expects<ArgumentNullException>(() => ArgumentProcessor.TryProcessArgs(null, logger, out provider));

            // 2. Empty array input
            provider = CheckProcessingSucceeds(logger, new string[] { });
            provider.AssertExpectedPropertyCount(0);
        }

        [TestMethod]
        public void PostArgProc_Unrecognised()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Unrecognized args
            logger = CheckProcessingFails("begin"); // bootstrapper verbs aren't meaningful to the post-processor
            logger.AssertSingleErrorExists("begin");

            logger = CheckProcessingFails("end");
            logger.AssertSingleErrorExists("end");

            logger = CheckProcessingFails("AAA", "BBB", "CCC");
            logger.AssertSingleErrorExists("AAA");
            logger.AssertSingleErrorExists("BBB");
            logger.AssertSingleErrorExists("CCC");
        }

        [TestMethod]
        public void PostArgProc_PermittedArguments()
        {
            // 0. Setup
            var logger = new TestLogger();
            var args = new string[]
            {
                "/d:sonar.login=user name",
                "/d:sonar.password=pwd",
                "/d:sonar.jdbc.username=db user name",
                "/d:sonar.jdbc.password=db pwd"
            };

            // 1. All valid args
            var provider = CheckProcessingSucceeds(logger, args);

            provider.AssertExpectedPropertyCount(4);
            provider.AssertExpectedPropertyValue("sonar.login", "user name");
            provider.AssertExpectedPropertyValue("sonar.password", "pwd");
            provider.AssertExpectedPropertyValue("sonar.jdbc.username", "db user name");
            provider.AssertExpectedPropertyValue("sonar.jdbc.password", "db pwd");
        }

        [TestMethod]
        public void PostArgProc_NotPermittedArguments()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Valid /d: arguments, but not the permitted ones
            logger = CheckProcessingFails("/d:sonar.visualstudio.enable=false");
            logger.AssertSingleErrorExists("sonar.visualstudio.enable");

            logger = CheckProcessingFails("/d:aaa=bbb", "/d:xxx=yyy");
            logger.AssertSingleErrorExists("aaa");
            logger.AssertSingleErrorExists("xxx");

            // 2. Incorrect versions of permitted /d: arguments
            logger = CheckProcessingFails("/D:sonar.login=user name"); // wrong case for "/d:"
            logger.AssertSingleErrorExists("sonar.login");

            logger = CheckProcessingFails("/d:SONAR.login=user name"); // wrong case for argument name
            logger.AssertSingleErrorExists("SONAR.login");
        }

        #endregion Tests

        #region Checks

        private static IAnalysisPropertyProvider CheckProcessingSucceeds(TestLogger logger, string[] input)
        {
            var success = ArgumentProcessor.TryProcessArgs(input, logger, out IAnalysisPropertyProvider provider);

            Assert.IsTrue(success, "Expecting processing to have succeeded");
            Assert.IsNotNull(provider, "Returned provider should not be null");
            logger.AssertErrorsLogged(0);

            return provider;
        }

        private static TestLogger CheckProcessingFails(params string[] input)
        {
            var logger = new TestLogger();

            var success = ArgumentProcessor.TryProcessArgs(input, logger, out IAnalysisPropertyProvider provider);

            Assert.IsFalse(success, "Not expecting processing to have succeeded");
            Assert.IsNull(provider, "Provider should be null if processing fails");
            logger.AssertErrorsLogged(); // expecting errors if processing failed

            return logger;
        }

        #endregion Checks
    }
}
