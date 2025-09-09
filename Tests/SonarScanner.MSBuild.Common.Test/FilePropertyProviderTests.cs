﻿/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

namespace SonarScanner.MSBuild.Common.Test;

[TestClass]
public class FilePropertyProviderTests
{
    public TestContext TestContext { get; set; }

    #region Tests

    [TestMethod]
    public void FileProvider_InvalidArguments()
    {
        // 0. Setup
        IAnalysisPropertyProvider provider;

        // 1. Null command line arguments
        Action act = () => FilePropertyProvider.TryCreateProvider(null, string.Empty, new TestLogger(), out provider);
        act.Should().ThrowExactly<ArgumentNullException>();

        // 2. Null directory
        act = () => FilePropertyProvider.TryCreateProvider(Enumerable.Empty<ArgumentInstance>(), null, new TestLogger(), out provider);
        act.Should().ThrowExactly<ArgumentNullException>();

        // 3. Null logger
        act = () => FilePropertyProvider.TryCreateProvider(Enumerable.Empty<ArgumentInstance>(), string.Empty, null, out provider);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestMethod]
    public void FileProvider_NoFileArguments()
    {
        // Arrange
        IAnalysisPropertyProvider provider;
        var logger = new TestLogger();
        var defaultPropertiesDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        // Act
        provider = CheckProcessingSucceeds(Enumerable.Empty<ArgumentInstance>(), defaultPropertiesDir, logger);

        // Assert
        provider.Should().NotBeNull("Expecting a provider to have been created");
        provider.GetAllProperties().Should().BeEmpty("Not expecting the provider to return any properties");
    }

    [TestMethod]
    public void FileProvider_UseDefaultPropertiesFile()
    {
        // Arrange
        var defaultPropertiesDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
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
    public void FileProvider_UseSpecifiedPropertiesFile()
    {
        // Arrange
        var testDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var validPropertiesFile = CreateValidPropertiesFile(testDir, "myPropertiesFile.xml", "xxx", "value with spaces");

        var defaultPropertiesDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext, "Default");
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
    public void FileProvider_MissingPropertiesFile()
    {
        // Arrange
        var logger = new TestLogger();
        var defaultPropertiesDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);

        IList<ArgumentInstance> args = new List<ArgumentInstance>
        {
            new ArgumentInstance(FilePropertyProvider.Descriptor, "missingFile.txt")
        };

        // Act
        CheckProcessingFails(args, defaultPropertiesDir, logger);

        // Assert
        logger.Should().HaveErrors(1);
        // The error should contain the full path of the file
        logger.Should().HaveSingleError($"Unable to find the analysis settings file '{Path.Combine(Directory.GetCurrentDirectory(), "missingFile.txt")}'. Please fix the path to this settings file.");
    }

    [TestMethod]
    public void FileProvider_InvalidDefaultPropertiesFile()
    {
        // Arrange
        var logger = new TestLogger();
        var defaultPropertiesDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var invalidFile = CreateFile(defaultPropertiesDir, FilePropertyProvider.DefaultFileName, "not a valid XML properties file");

        IList<ArgumentInstance> args = new List<ArgumentInstance>();

        // Act
        CheckProcessingFails(args, defaultPropertiesDir, logger);

        // Assert
        logger.Should().HaveErrors(1);
        logger.Should().HaveSingleError($"Unable to read the analysis settings file '{invalidFile}'. Please fix the content of this file.");
    }

    [TestMethod]
    public void FileProvider_InvalidSpecifiedPropertiesFile()
    {
        // Arrange
        var logger = new TestLogger();
        var defaultPropertiesDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        var invalidFile = CreateFile(defaultPropertiesDir, "invalidPropertiesFile.txt", "not a valid XML properties file");

        IList<ArgumentInstance> args = new List<ArgumentInstance>
        {
            new ArgumentInstance(FilePropertyProvider.Descriptor, invalidFile)
        };

        // Act
        CheckProcessingFails(args, defaultPropertiesDir, logger);

        // Assert
        logger.Should().HaveErrors(1);
        logger.Should().HaveSingleError($"Unable to read the analysis settings file '{invalidFile}'. Please fix the content of this file.");
    }

    [TestMethod]
    public void FileProvider_HasCorrectProviderType()
    {
        var defaultPropertiesDir = TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext);
        _ = CreateValidPropertiesFile(defaultPropertiesDir, FilePropertyProvider.DefaultFileName, "key1", "value1");
        var provider = CheckProcessingSucceeds(new List<ArgumentInstance>(), defaultPropertiesDir, new TestLogger());

        provider.ProviderType.Should().Be(PropertyProviderKind.SONARQUBE_ANALYSIS_XML);
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
        var properties = new AnalysisProperties { new(property, value) };
        properties.Save(fullPath);
        return fullPath;
    }

    #endregion Private methods

    #region Checks

    private static IAnalysisPropertyProvider CheckProcessingSucceeds(IEnumerable<ArgumentInstance> cmdLineArgs, string defaultPropertiesDirectory, TestLogger logger)
    {
        var isValid = FilePropertyProvider.TryCreateProvider(cmdLineArgs, defaultPropertiesDirectory, logger, out var provider);

        isValid.Should().BeTrue("Expecting the provider to be initialized successfully");
        provider.Should().NotBeNull("Not expecting a null provider if the function returned true");
        logger.Should().HaveErrors(0);

        return provider;
    }

    private static void CheckProcessingFails(IEnumerable<ArgumentInstance> cmdLineArgs, string defaultPropertiesDirectory, TestLogger logger)
    {
        var isValid = FilePropertyProvider.TryCreateProvider(cmdLineArgs, defaultPropertiesDirectory, logger, out var provider);

        isValid.Should().BeFalse("Not expecting the provider to be initialized successfully");
        provider.Should().BeNull("Not expecting a provider instance if the function returned true");
        logger.Should().HaveErrors();
    }

    private static void AssertExpectedPropertiesFile(string expectedFilePath, IAnalysisPropertyProvider actualProvider)
    {
        var fileProvider = AssertIsFilePropertyProvider(actualProvider);

        fileProvider.PropertiesFile.Should().NotBeNull("Properties file object should not be null");
        fileProvider.PropertiesFile.FilePath.Should().Be(expectedFilePath, "Properties were not loaded from the expected location");
    }

    private static void AssertIsDefaultPropertiesFile(IAnalysisPropertyProvider actualProvider)
    {
        var fileProvider = AssertIsFilePropertyProvider(actualProvider);
        fileProvider.IsDefaultSettingsFile.Should().BeTrue("Expecting the provider to be marked as using the default properties file");
    }

    private static void AssertIsNotDefaultPropertiesFile(IAnalysisPropertyProvider actualProvider)
    {
        var fileProvider = AssertIsFilePropertyProvider(actualProvider);
        fileProvider.IsDefaultSettingsFile.Should().BeFalse("Not expecting the provider to be marked as using the default properties file");
    }

    private static FilePropertyProvider AssertIsFilePropertyProvider(IAnalysisPropertyProvider actualProvider)
    {
        actualProvider.Should().NotBeNull("Supplied provider should not be null");
        actualProvider.Should().BeOfType<FilePropertyProvider>("Expecting a file provider");

        return (FilePropertyProvider)actualProvider;
    }

    #endregion Checks
}
