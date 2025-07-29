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

using System;
using System.Diagnostics;
using System.IO;
using FluentAssertions;

namespace TestUtilities;

/// <summary>
/// Utility class that reads properties from a standard format SonarQube properties file (e.g. sonar-scanner.properties)
/// </summary>
public class SQPropertiesFileReader
{
    /// <summary>
    /// Mapping of property names to values
    /// </summary>
    public JavaProperties Properties { get; }

    #region Public methods

    /// <summary>
    /// Creates a new provider that reads properties from the
    /// specified properties file
    /// </summary>
    /// <param name="fullPath">The full path to the SonarQube properties file. The file must exist.</param>
    public SQPropertiesFileReader(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            throw new ArgumentNullException(nameof(fullPath));
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException();
        }

        Properties = ExtractProperties(fullPath);
    }

    public void AssertSettingExists(string key, string expectedValue)
    {
        var actualValue = Properties.GetProperty(key);
        var found = actualValue != null;

        found.Should().BeTrue("Expected setting was not found. Key: {0}", key);
        actualValue.Should().Be(expectedValue, "Property does not have the expected value. Key: {0}", key);
    }

    public void AssertSettingDoesNotExist(string key)
    {
        var actualValue = Properties.GetProperty(key);
        var found = actualValue != null;

        found.Should().BeFalse("Not expecting setting to be found. Key: {0}, value: {1}", key, actualValue);
    }

    public string GetProperty(string key) =>
        Properties.GetProperty(key);

    #endregion Public methods

    #region FilePropertiesProvider

    private JavaProperties ExtractProperties(string fullPath)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(fullPath), "fullPath should be specified");

        var properties = new JavaProperties();
        using (var stream = File.Open(fullPath, FileMode.Open))
        {
            properties.Load(stream);
        }
        return properties;
    }

    #endregion FilePropertiesProvider
}
