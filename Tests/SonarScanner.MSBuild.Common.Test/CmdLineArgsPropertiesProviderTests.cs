/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
 * mailto: info AT sonarsource DOT com
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
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Common.Test
{
    [TestClass]
    public class CmdLineArgsPropertiesProviderTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_InvalidArguments()
        {
            IAnalysisPropertyProvider provider;

            Action act = () => CmdLineArgPropertyProvider.TryCreateProvider(null, new TestLogger(), out provider);
            act.Should().ThrowExactly<ArgumentNullException>();

            act = () => CmdLineArgPropertyProvider.TryCreateProvider(Enumerable.Empty<ArgumentInstance>(), null, out provider);
            act.Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_NoArguments()
        {
            var provider = CheckProcessingSucceeds(Enumerable.Empty<ArgumentInstance>(), new TestLogger());

            provider.GetAllProperties().Should().BeEmpty("Not expecting any properties");
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_DynamicProperties()
        {
            // Arrange
            var logger = new TestLogger();
            IList<ArgumentInstance> args = new List<ArgumentInstance>();

            var dummyDescriptor = new ArgumentDescriptor("dummy", new string[] { "dummy prefix" }, false, "dummy desc", true);
            var dummyDescriptor2 = new ArgumentDescriptor("dummy2", new string[] { "dummy prefix 2" }, false, "dummy desc 2", true);

            args.Add(new ArgumentInstance(dummyDescriptor, "should be ignored"));
            args.Add(new ArgumentInstance(dummyDescriptor2, "should be ignored"));

            AddDynamicArguments(args, "key1=value1", "key2=value two with spaces");

            // Act
            var provider = CheckProcessingSucceeds(args, logger);

            // Assert
            provider.AssertExpectedPropertyValue("key1", "value1");
            provider.AssertExpectedPropertyValue("key2", "value two with spaces");

            provider.AssertExpectedPropertyCount(2);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_DynamicProperties_Invalid()
        {
            // Arrange
            // Act
            var logger = CheckProcessingFails(
                    "invalid1 =aaa",
                    "notkeyvalue",
                    " spacebeforekey=bb",
                    "missingvalue=",
                    "validkey=validvalue");

            // Assert
            logger.AssertSingleErrorExists("invalid1 =aaa");
            logger.AssertSingleErrorExists("notkeyvalue");
            logger.AssertSingleErrorExists(" spacebeforekey=bb");
            logger.AssertSingleErrorExists("missingvalue=");

            logger.AssertErrorsLogged(4);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_DynamicProperties_Duplicates()
        {
            // Arrange
            // Act
            var logger = CheckProcessingFails(
                    "dup1=value1", "dup1=value2",
                    "dup2=value3", "dup2=value4",
                    "unique=value5");

            // Assert
            logger.AssertSingleErrorExists("dup1=value2", "value1");
            logger.AssertSingleErrorExists("dup2=value4", "value3");
            logger.AssertErrorsLogged(2);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void CmdLineArgProperties_Disallowed_DynamicProperties()
        {
            // 0. Setup
            TestLogger logger;

            // 1. Named arguments cannot be overridden
            logger = CheckProcessingFails(
                "sonar.projectKey=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectKey, "/k");

            logger = CheckProcessingFails(
                "sonar.projectName=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectName, "/n");

            logger = CheckProcessingFails(
                "sonar.projectVersion=value1");
            logger.AssertSingleErrorExists(SonarProperties.ProjectVersion, "/v");

            // 2. Other values that can't be set
            logger = CheckProcessingFails(
                "sonar.working.directory=value1");
            logger.AssertSingleErrorExists(SonarProperties.WorkingDirectory);
        }

        [TestMethod]
        [Description("Test for https://jira.sonarsource.com/browse/SONARMSBRU-208")]
        public void SonarProjectBaseDir_IsAllowed()
        {
            var logger = new TestLogger();
            IList<ArgumentInstance> args = new List<ArgumentInstance>();

            // sonar.projectBaseDir used to be un-settable
            AddDynamicArguments(args, "sonar.projectBaseDir=value1");

            var provider = CheckProcessingSucceeds(args, logger);
            provider.AssertExpectedPropertyValue("sonar.projectBaseDir", "value1");
            provider.AssertExpectedPropertyCount(1);
        }

        #endregion Tests

        #region Private methods

        private static void AddDynamicArguments(IList<ArgumentInstance> args, params string[] argValues)
        {
            foreach (var argValue in argValues)
            {
                args.Add(new ArgumentInstance(CmdLineArgPropertyProvider.Descriptor, argValue));
            }
        }

        #endregion Private methods

        #region Checks

        private static TestLogger CheckProcessingFails(params string[] argValues)
        {
            IList<ArgumentInstance> args = new List<ArgumentInstance>();
            AddDynamicArguments(args, argValues);

            return CheckProcessingFails(args);
        }

        private static TestLogger CheckProcessingFails(IEnumerable<ArgumentInstance> args)
        {
            var logger = new TestLogger();

            var success = CmdLineArgPropertyProvider.TryCreateProvider(args, logger, out var provider);
            success.Should().BeFalse("Not expecting the provider to be created");
            provider.Should().BeNull("Expecting the provider to be null is processing fails");
            logger.AssertErrorsLogged();

            return logger;
        }

        private static IAnalysisPropertyProvider CheckProcessingSucceeds(IEnumerable<ArgumentInstance> args, TestLogger logger)
        {
            var success = CmdLineArgPropertyProvider.TryCreateProvider(args, logger, out var provider);

            success.Should().BeTrue("Expected processing to succeed");
            provider.Should().NotBeNull("Not expecting a null provider when processing succeeds");
            logger.AssertErrorsLogged(0);

            return provider;
        }

        #endregion Checks
    }
}
