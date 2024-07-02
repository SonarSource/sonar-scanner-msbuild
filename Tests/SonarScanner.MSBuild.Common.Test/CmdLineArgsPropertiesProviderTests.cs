/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Common.Test
{
    [TestClass]
    public class CmdLineArgsPropertiesProviderTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void CmdLineArgProperties_InvalidArguments()
        {
            Action act = () => CmdLineArgPropertyProvider.TryCreateProvider(null, new TestLogger(), out _);
            act.Should().ThrowExactly<ArgumentNullException>();

            act = () => CmdLineArgPropertyProvider.TryCreateProvider([], null, out _);
            act.Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void CmdLineArgProperties_NoArguments()
        {
            var provider = CheckProcessingSucceeds([], new TestLogger());

            provider.GetAllProperties().Should().BeEmpty("Not expecting any properties");
        }

        [TestMethod]
        public void CmdLineArgProperties_DynamicProperties()
        {
            // Arrange
            var logger = new TestLogger();
            IList<ArgumentInstance> args = new List<ArgumentInstance>();

            var dummyDescriptor = new ArgumentDescriptor("dummy", ["dummy prefix"], false, "dummy desc", true);
            var dummyDescriptor2 = new ArgumentDescriptor("dummy2", ["dummy prefix 2"], false, "dummy desc 2", true);

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

        [DataTestMethod]
        [DataRow("sonar.projectBaseDir=value1")]
        [DataRow($"{SonarProperties.SonarcloudUrl}=value1")]
        [DataRow($"{SonarProperties.JavaExePath}=value1")]
        [DataRow($"{SonarProperties.ApiBaseUrl}=value1")]
        [DataRow($"{SonarProperties.ConnectTimeout }=value1")]
        [DataRow($"{SonarProperties.SocketTimeout }=value1")]
        [DataRow($"{SonarProperties.ResponseTimeout}=value1")]
        [DataRow($"{SonarProperties.VsCoverageXmlReportsPaths}=value1")]
        [DataRow($"{SonarProperties.OperatingSystem}=value1")]
        [DataRow($"{SonarProperties.Architecture}=value1")]
        public void SonarProperties_IsAllowed(string argument)
        {
            var logger = new TestLogger();
            IList<ArgumentInstance> args = new List<ArgumentInstance>();

            AddDynamicArguments(args, argument);
            var expectedValues = argument.Split('=');
            var propertyName = expectedValues[0];
            var propertyValue = expectedValues[1];
            var provider = CheckProcessingSucceeds(args, logger);
            provider.AssertExpectedPropertyValue(propertyName, propertyValue);
            provider.AssertExpectedPropertyCount(1);
        }

        private static void AddDynamicArguments(IList<ArgumentInstance> args, params string[] argValues)
        {
            foreach (var argValue in argValues)
            {
                args.Add(new ArgumentInstance(CmdLineArgPropertyProvider.Descriptor, argValue));
            }
        }

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
    }
}
