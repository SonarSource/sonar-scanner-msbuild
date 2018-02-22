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
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class to describe an additional analysis configuration property
    /// /// </summary>
    /// <remarks>The class is XML-serializable</remarks>
    public class Property
    {
        #region Data

        /// <summary>
        /// The identifier for the property
        /// </summary>
        /// <remarks>Each type </remarks>
        [XmlAttribute("Name")]
        public string Id { get; set; }

        /// <summary>
        /// The value of the property
        /// </summary>
        [XmlText]
        public string Value { get; set; }

        #endregion Data

        #region Public methods

        /// <summary>
        /// Returns whether the property contains any sensitive data
        /// </summary>
        public bool ContainsSensitiveData()
        {
            return ProcessRunnerArguments.ContainsSensitiveData(Id) || ProcessRunnerArguments.ContainsSensitiveData(Value);
        }

        /// <summary>
        /// Returns the property formatted as a sonar-scanner "-D" argument
        /// </summary>
        public string AsSonarScannerArg()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "-D{0}={1}", Id, Value);
        }

        #endregion Public methods

        #region Static helper methods

        //TODO: this expression only works for single-line values
        // Regular expression pattern: we're looking for matches that:
        // * start at the beginning of a line
        // * start with a character or number
        // * are in the form [key]=[value],
        // * where [key] can
        //   - starts with an alpanumeric character.
        //   - can be followed by any number of alphanumeric characters or .
        //   - whitespace is not allowed
        // * [value] can contain anything
        public const string KeyValuePropertyPattern = @"^(?<key>\w[\w\d\.-]*)=(?<value>[^\r\n]+)";

        private static readonly Regex SingleLinePropertyRegEx = new Regex(KeyValuePropertyPattern, RegexOptions.Compiled);

        /// <summary>
        /// Regular expression to validate setting ids.
        /// </summary>
        /// <remarks>
        /// Validation rules:
        /// Must start with an alpanumeric character.
        /// Can be followed by any number of alphanumeric characters or .
        /// Whitespace is not allowed
        /// </remarks>
        private static readonly Regex ValidSettingKeyRegEx = new Regex(@"^\w[\w\d\.-]*$", RegexOptions.Compiled);

        /// <summary>
        /// Comparer to use when comparing keys of analysis properties
        /// </summary>
        private static readonly IEqualityComparer<string> PropertyKeyComparer = StringComparer.Ordinal;

        /// <summary>
        /// Returns true if the supplied string is a valid key for a sonar-XXX.properties file, otherwise false
        /// </summary>
        public static bool IsValidKey(string key)
        {
            var isValid = ValidSettingKeyRegEx.IsMatch(key);
            return isValid;
        }

        public static bool AreKeysEqual(string key1, string key2)
        {
            return PropertyKeyComparer.Equals(key1, key2);
        }

        /// <summary>
        /// Attempts to parse the supplied string into a key and value
        /// </summary>
        public static bool TryParse(string input, out Property property)
        {
            property = null;

            var match = SingleLinePropertyRegEx.Match(input);

            if (match.Success)
            {
                var key = match.Groups["key"].Value;
                var value = match.Groups["value"].Value;

                property = new Property() { Id = key, Value = value };
            }
            return property != null;
        }

        /// <summary>
        /// Returns the first property with the supplied key, or null if there is no match
        /// </summary>
        public static bool TryGetProperty(string key, IEnumerable<Property> properties, out Property property)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            property = properties.FirstOrDefault(s => PropertyKeyComparer.Equals(s.Id, key));
            return property != null;
        }

        #endregion Static helper methods
    }
}
