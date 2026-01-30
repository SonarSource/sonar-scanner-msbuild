/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

    public IEnumerable<Property> GetAllProperties() =>
        GetAllPropertiesWithProvider().Select(x => x.Key);

    public IEnumerable<KeyValuePair<Property, IAnalysisPropertyProvider>> GetAllPropertiesWithProvider()
    {
        var allKeys = new HashSet<string>(providers.SelectMany(x => x.GetAllProperties().Select(x => x.Id)));

        IList<KeyValuePair<Property, IAnalysisPropertyProvider>> allProperties = [];
        foreach (var key in allKeys)
        {
            var match = TryGetProperty(key, out var property, out var provider);
            Debug.Assert(match, "Expecting to find value for all keys. Key: " + key);
            allProperties.Add(new(property, provider));
        }

        return allProperties;
    }

    public bool TryGetProperty(string key, out Property property) =>
        TryGetProperty(key, out property, out _);

    public bool TryGetProperty(string key, out Property property, out IAnalysisPropertyProvider provider)
    {
        property = null;
        provider = null;

        foreach (var current in providers)
        {
            if (current.TryGetProperty(key, out property))
            {
                provider = UnwrapNestedProvider(current, key);
                return true;
            }
        }

        return false;
    }

    private static IAnalysisPropertyProvider UnwrapNestedProvider(IAnalysisPropertyProvider provider, string key) =>
        provider is AggregatePropertiesProvider aggregate
            ? UnwrapNestedProvider(aggregate.providers.First(x => x.HasProperty(key)), key)
            : provider;

    #endregion IAnalysisPropertyProvider interface
}
