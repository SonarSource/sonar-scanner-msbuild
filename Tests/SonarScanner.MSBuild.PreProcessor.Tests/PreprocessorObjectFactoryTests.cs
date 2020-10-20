/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2020 SonarSource SA
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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    [TestClass]
    public class PreprocessorObjectFactoryTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void Factory_ThrowsOnInvalidInput()
        {
            // Arrange
            var logger = new TestLogger();
            IPreprocessorObjectFactory testSubject = new PreprocessorObjectFactory(logger);

            // 1. Ctor
            Action act1 = () => new PreprocessorObjectFactory(null);
            act1.Should().ThrowExactly<ArgumentNullException>();

            // 1. CreateSonarQubeServer method
            Action act2 = () => testSubject.CreateSonarQubeServer(null);
            act2.Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void Factory_ValidCallSequence_ValidObjectReturned()
        {
            // Arrange
            var logger = new TestLogger();
            var validArgs = CreateValidArguments();
            IPreprocessorObjectFactory testSubject = new PreprocessorObjectFactory(logger);

            // 1. Create the SonarQube server...
            object actual = testSubject.CreateSonarQubeServer(validArgs);
            actual.Should().NotBeNull();

            // 2. Now create the targets provider
            actual = testSubject.CreateTargetInstaller();
            actual.Should().NotBeNull();

            // 3. Now create the analyzer provider
            actual = testSubject.CreateRoslynAnalyzerProvider();
            actual.Should().NotBeNull();
        }

        [TestMethod]
        public void Factory_InvalidCallSequence_Fails()
        {
            // Arrange
            var logger = new TestLogger();
            IPreprocessorObjectFactory testSubject = new PreprocessorObjectFactory(logger);

            // 2. Act and assert
            Action act = () => testSubject.CreateRoslynAnalyzerProvider();
            act.Should().ThrowExactly<InvalidOperationException>();
        }

        #endregion Tests

        #region Private methods

        private ProcessedArgs CreateValidArguments()
        {
            var cmdLineArgs = new Common.ListPropertiesProvider();
            cmdLineArgs.AddProperty(Common.SonarProperties.HostUrl, "http://foo");

            var validArgs = new ProcessedArgs("key", "name", "verions", "organization", false,
                cmdLineArgs,
                new Common.ListPropertiesProvider(),
                EmptyPropertyProvider.Instance);
            return validArgs;
        }

        #endregion Private methods
    }
}
