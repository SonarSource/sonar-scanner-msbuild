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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarScanner.MSBuild.Common.UnitTests
{
    [TestClass]
    public class FilePropertyProviderTests
    {
        private static readonly ArgumentDescriptor DummyDescriptor = new ArgumentDescriptor("dummy", new string[] { "dummy predifx" }, false, "dummy desc", true);

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_InvalidArguments()
        {
            // 0. Setup
            IAnalysisPropertyProvider provider;

            // 1. Null command line arguments
            AssertException.Expects<ArgumentNullException>(() => FilePropertyProvider.TryCreateProvider(null, string.Empty, new TestLogger(), out provider));

            // 2. Null directory
            AssertException.Expects<ArgumentNullException>(() => FilePropertyProvider.TryCreateProvider(Enumerable.Empty<ArgumentInstance>(), null, new TestLogger(), out provider));

            // 3. Null logger
            AssertException.Expects<ArgumentNullException>(() => FilePropertyProvider.TryCreateProvider(Enumerable.Empty<ArgumentInstance>(), string.Empty, null, out provider));
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_NoFileArguments()
        {
            // Arrange
            IAnalysisPropertyProvider provider;
            var logger = new TestLogger();
            var defaultPropertiesDir = TestContext.TestDeploymentDir;

            // Act
            provider = CheckProcessingSucceeds(Enumerable.Empty<ArgumentInstance>(), defaultPropertiesDir, logger);

            // Assert
            Assert.IsNotNull(provider, "Expecting a provider to have been created");
            Assert.AreEqual(0, provider.GetAllProperties().Count(), "Not expecting the provider to return any properties");
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_UseDefaultPropertiesFile()
        {
            // Arrange
            var defaultPropertiesDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var validPropertiesFile = CreateValidPropertiesFile(defaultPropertiesDir, FilePropertyProvider.DefaultFileName, "key1", "value1");
            var logger = new TestLogger();

            IList<ArgumentInstance> args = new List<ArgumentInstance>();

            // Act
            var provider = CheckProcessingSucceeds(args, defaultPropertiesDir, logger);

            // Assert
            AssertExpectedPropertiesFile(validPropertiesFile, provider);
            provider.AssertExpectedPropertyValue("key1", "value1");
            AssertIsDefaultPropertiesFile(provider);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_UseSpecifiedPropertiesFile()
        {
            // Arrange
            var testDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var validPropertiesFile = CreateValidPropertiesFile(testDir, "myPropertiesFile.xml", "xxx", "value with spaces");

            var defaultPropertiesDir = TestUtils.CreateTestSpecificFolder(TestContext, "Default");
            CreateFile(defaultPropertiesDir, FilePropertyProvider.DefaultFileName, "invalid file - will error if this file is loaded");

            IList<ArgumentInstance> args = new List<ArgumentInstance>
            {
                new ArgumentInstance(FilePropertyProvider.Descriptor, validPropertiesFile)
            };

            var logger = new TestLogger();

            // Act
            var provider = CheckProcessingSucceeds(args, defaultPropertiesDir, logger);

            // Assert
            AssertExpectedPropertiesFile(validPropertiesFile, provider);
            provider.AssertExpectedPropertyValue("xxx", "value with spaces");
            AssertIsNotDefaultPropertiesFile(provider);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_MissingPropertiesFile()
        {
            // Arrange
            var logger = new TestLogger();
            var defaultPropertiesDir = TestContext.DeploymentDirectory;

            IList<ArgumentInstance> args = new List<ArgumentInstance>
            {
                new ArgumentInstance(FilePropertyProvider.Descriptor, "missingFile.txt")
            };

            // Act
            CheckProcessingFails(args, defaultPropertiesDir, logger);

            // Assert
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists("missingFile.txt");
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_InvalidDefaultPropertiesFile()
        {
            // Arrange
            var logger = new TestLogger();
            var defaultPropertiesDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var invalidFile = CreateFile(defaultPropertiesDir, FilePropertyProvider.DefaultFileName, "not a valid XML properties file");

            IList<ArgumentInstance> args = new List<ArgumentInstance>();

            // Act
            CheckProcessingFails(args, defaultPropertiesDir, logger);

            // Assert
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists(invalidFile);
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void FileProvider_InvalidSpecifiedPropertiesFile()
        {
            // Arrange
            var logger = new TestLogger();
            var defaultPropertiesDir = TestUtils.CreateTestSpecificFolder(TestContext);
            var invalidFile = CreateFile(defaultPropertiesDir, "invalidPropertiesFile.txt", "not a valid XML properties file");

            IList<ArgumentInstance> args = new List<ArgumentInstance>
            {
                new ArgumentInstance(FilePropertyProvider.Descriptor, invalidFile)
            };

            // Act
            CheckProcessingFails(args, defaultPropertiesDir, logger);

            // Assert
            logger.AssertErrorsLogged(1);
            logger.AssertSingleErrorExists(invalidFile);
        }

        #endregion Tests

        #region Private methods

        private static string CreateFile(string path, string fileName, string content)
        {
            var fullPath = Path.Combine(path, fileName);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        /// <summary>
        /// Creates a valid properties file with a single property
        /// </summary>
        private static string CreateValidPropertiesFile(string path, string fileName, string property, string value)
        {
            var fullPath = Path.Combine(path, fileName);

            var properties = new AnalysisProperties
            {
                new Property() { Id = property, Value = value }
            };

            properties.Save(fullPath);
            return fullPath;
        }

        private static void AddProperty(IList<Property> properties, string key, string value)
        {
            properties.Add(new Property() { Id = key, Value = value });
        }

        #endregion Private methods

        #region Checks

        private static IAnalysisPropertyProvider CheckProcessingSucceeds(IEnumerable<ArgumentInstance> cmdLineArgs, string defaultPropertiesDirectory, TestLogger logger)
        {
            var isValid = FilePropertyProvider.TryCreateProvider(cmdLineArgs, defaultPropertiesDirectory, logger, out IAnalysisPropertyProvider provider);

            Assert.IsTrue(isValid, "Expecting the provider to be initialized successfully");
            Assert.IsNotNull(provider, "Not expecting a null provider if the function returned true");
            logger.AssertErrorsLogged(0);

            return provider;
        }

        private static void CheckProcessingFails(IEnumerable<ArgumentInstance> cmdLineArgs, string defaultPropertiesDirectory, TestLogger logger)
        {
            var isValid = FilePropertyProvider.TryCreateProvider(cmdLineArgs, defaultPropertiesDirectory, logger, out IAnalysisPropertyProvider provider);

            Assert.IsFalse(isValid, "Not expecting the provider to be initialized successfully");
            Assert.IsNull(provider, "Not expecting a provider instance if the function returned true");
            logger.AssertErrorsLogged();
        }

        private static void AssertExpectedPropertiesFile(string expectedFilePath, IAnalysisPropertyProvider actualProvider)
        {
            var fileProvider = AssertIsFilePropertyProvider(actualProvider);

            Assert.IsNotNull(fileProvider.PropertiesFile, "Properties file object should not be null");
            Assert.AreEqual(expectedFilePath, fileProvider.PropertiesFile.FilePath, "Properties were not loaded from the expected location");
        }

        private static void AssertIsDefaultPropertiesFile(IAnalysisPropertyProvider actualProvider)
        {
            var fileProvider = AssertIsFilePropertyProvider(actualProvider);
            Assert.IsTrue(fileProvider.IsDefaultSettingsFile, "Expecting the provider to be marked as using the default properties file");
        }

        private static void AssertIsNotDefaultPropertiesFile(IAnalysisPropertyProvider actualProvider)
        {
            var fileProvider = AssertIsFilePropertyProvider(actualProvider);
            Assert.IsFalse(fileProvider.IsDefaultSettingsFile, "Not expecting the provider to be marked as using the default properties file");
        }

        private static FilePropertyProvider AssertIsFilePropertyProvider(IAnalysisPropertyProvider actualProvider)
        {
            Assert.IsNotNull(actualProvider, "Supplied provider should not be null");
            Assert.IsInstanceOfType(actualProvider, typeof(FilePropertyProvider), "Expecting a file provider");

            return (FilePropertyProvider)actualProvider;
        }

        #endregion Checks
    }
}
