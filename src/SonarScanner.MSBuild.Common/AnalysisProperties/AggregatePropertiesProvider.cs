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

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Properties provider that aggregates the properties from multiple "child" providers.
/// The child providers are checked in order until one of them returns a value.
/// </summary>
public class AggregatePropertiesProvider : IAnalysisPropertyProvider
{
    /// <summary>
    /// Ordered list of child providers.
    /// </summary>
    private readonly IAnalysisPropertyProvider[] providers;

    public PropertyProviderKind ProviderType => PropertyProviderKind.UNKNOWN;

    #region Public methods

    public AggregatePropertiesProvider(params IAnalysisPropertyProvider[] providers) =>
        this.providers = providers ?? throw new ArgumentNullException(nameof(providers));

    #endregion Public methods

    #region IAnalysisPropertyProvider interface

    public IEnumerable<Property> GetAllProperties()
    {
        var allKeys = new HashSet<string>(providers.SelectMany(x => x.GetAllProperties().Select(x => x.Id)));

        IList<Property> allProperties = [];
        foreach (var key in allKeys)
        {
            var match = TryGetProperty(key, out var property);

            Debug.Assert(match, "Expecting to find value for all keys. Key: " + key);
            allProperties.Add(property);
        }

        return allProperties;
    }

    public IDictionary<PropertyProviderKind, IList<Property>> GetAllPropertiesPerProvider()
    {
        var propertiesPerProvider = new Dictionary<PropertyProviderKind, IList<Property>>();
        foreach (var providerWithProperties in providers.ToDictionary(x => x.ProviderType, x => x.GetAllProperties()))
        {
            IList<Property> properties = [];
            foreach (var prop in providerWithProperties.Value)
            {
                var match = TryGetProperty(prop.Id, out var property);

                Debug.Assert(match, "Expecting to find value for all keys. Key: " + property.Id);
                properties.Add(property);
            }
            propertiesPerProvider.Add(providerWithProperties.Key, properties);
        }

        return propertiesPerProvider;
    }

    public bool TryGetProperty(string key, out Property property)
    {
        property = null;

        foreach (var current in providers)
        {
            if (current.TryGetProperty(key, out property))
            {
                return true;
            }
        }

        return false;
    }

    #endregion IAnalysisPropertyProvider interface
}
