/*
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
using System.Collections.Generic;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Simple settings provider that returns values from a list
/// </summary>
public class ListPropertiesProvider : IAnalysisPropertyProvider
{
    private readonly IList<Property> properties;

    public PropertyProviderKind ProviderType => PropertyProviderKind.SQ_SERVER_SETTINGS;

    #region Public methods

    public ListPropertiesProvider()
    {
        properties = [];
    }

    public ListPropertiesProvider(IEnumerable<Property> properties)
    {
        if (properties is null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        this.properties = [.. properties];
    }

    public ListPropertiesProvider(IDictionary<string, string> keyValuePairs)
        : this()
    {
        if (keyValuePairs is null)
        {
            throw new ArgumentNullException(nameof(keyValuePairs));
        }

        foreach (var kvp in keyValuePairs)
        {
            AddProperty(kvp.Key, kvp.Value);
        }
    }

    public Property AddProperty(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (TryGetProperty(key, out var existing))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        var newProperty = new Property(key, value);
        properties.Add(newProperty);
        return newProperty;
    }

    #endregion Public methods

    #region IAnalysisProperiesProvider interface

    public IEnumerable<Property> GetAllProperties() => properties;

    public bool TryGetProperty(string key, out Property property) =>
        string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentNullException(nameof(key))
            : Property.TryGetProperty(key, properties, out property);

    #endregion IAnalysisProperiesProvider interface
}
