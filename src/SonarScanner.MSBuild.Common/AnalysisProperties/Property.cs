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
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Data class to describe an additional analysis configuration property.
/// </summary>
/// <remarks>The class is XML-serializable.</remarks>
public class Property
{
    // Regular expression pattern: we're looking for matches that:
    // * start at the beginning of a line
    // * start with a character or number
    // * are in the form [key]=[value],
    // * where [key] can
    //   - starts with an alphanumeric character.
    //   - can be followed by any number of alphanumeric characters or .
    //   - whitespace is not allowed
    // * [value] can contain anything
    private static readonly Regex SingleLinePropertyRegEx = new(@"^(?<key>\w[\w\d\.-]*)=(?<value>[^\r\n]+)", RegexOptions.Compiled, RegexConstants.DefaultTimeout);
    // Regular expression to validate setting ids.
    // Must start with an alphanumeric character.
    // Can be followed by any number of alphanumeric characters or '.'.
    // Whitespace is not allowed.
    private static readonly Regex ValidSettingKeyRegEx = new(@"^\w[\w\d\.-]*$", RegexOptions.Compiled, RegexConstants.DefaultTimeout);
    private static readonly IEqualityComparer<string> PropertyKeyComparer = StringComparer.Ordinal;

    [XmlAttribute("Name")]
    public string Id { get; set; }

    [XmlText]
    public string Value { get; set; }

    private Property() { }   // For serialization

    public Property(string id, string value)
    {
        Id = id;
        Value = value;
    }

    public bool ContainsSensitiveData() =>
        ProcessRunnerArguments.ContainsSensitiveData(Id) || ProcessRunnerArguments.ContainsSensitiveData(Value);

    /// <summary>
    /// Returns the property formatted as a sonar-scanner "-D" argument.
    /// </summary>
    public string AsSonarScannerArg() =>
        $"-D{Id}={Value}";

    /// <summary>
    /// Returns true if the supplied string is a valid key for a sonar-XXX.properties file.
    /// </summary>
    public static bool IsValidKey(string key) =>
        ValidSettingKeyRegEx.IsMatch(key);

    public static bool AreKeysEqual(string key1, string key2) =>
        PropertyKeyComparer.Equals(key1, key2);

    public static Property Parse(string input) =>
        SingleLinePropertyRegEx.Match(input) is { Success: true } match
            ? new(match.Groups["key"].Value, match.Groups["value"].Value)
            : null;

    public static bool TryGetProperty(string key, IEnumerable<Property> properties, out Property property)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key));
        }
        _ = properties ?? throw new ArgumentNullException(nameof(properties));

        property = properties.FirstOrDefault(s => PropertyKeyComparer.Equals(s.Id, key));
        return property != null;
    }
}
