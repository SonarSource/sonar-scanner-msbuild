//-----------------------------------------------------------------------
// <copyright file="FilePropertiesProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace SonarQube.Common
{
    /// <summary>
    /// Implementation of <see cref="ISonarPropertyProvider"/> that reads properties from
    /// a standard format SonarQube properties file (e.g. sonar-runner.properties)
    /// </summary>
    public class FilePropertiesProvider : ISonarPropertyProvider
    {
        /// <summary>
        /// Mapping of property names to values
        /// </summary>
        private IDictionary<string, string> properties;

        /// <summary>
        /// Property name comparisons are be case-sensitive
        /// </summary>
        private static StringComparer PropertyKeyComparer = StringComparer.InvariantCulture;

        private string propertyFilePath;

        #region Public methods

        /// <summary>
        /// Creates a new provider that reads properties from the
        /// specified properties file
        /// </summary>
        /// <param name="fullPath">The full path to the SonarQube properties file. The file must exist.</param>
        public FilePropertiesProvider(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                throw new ArgumentNullException("fullPath");
            }
                        
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(Resources.MissingSonarPropertiesFileError, fullPath);
            }

            this.propertyFilePath = fullPath;
            this.ExtractProperties(fullPath);
        }

        #endregion

        #region ISonarPropertyProvider interface

        public string GetProperty(string propertyName)
        {
            string value;
            if (!this.properties.TryGetValue(propertyName, out value))
            {
                string message = string.Format(System.Globalization.CultureInfo.CurrentCulture, Resources.PropertyNotFoundInFileError, this.propertyFilePath, propertyName);
                throw new ArgumentOutOfRangeException(propertyName, message);
            }
            return value;
        }

        public string GetProperty(string propertyName, string defaultValue)
        {
            string value;
            if (!this.properties.TryGetValue(propertyName, out value))
            {
                value = defaultValue;
            }
            return value;
        }

        #endregion

        #region FilePropertiesProvider

        private void ExtractProperties(string fullPath)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(fullPath), "fullPath should be specified");

            this.properties = new Dictionary<string, string>(PropertyKeyComparer);
            string allText = File.ReadAllText(fullPath);

            // Regular expression pattern: we're looking for matches that:
            // * start at the beginning of a line
            // * start with a character or number
            // * consist of string of characters, numbers or "." without whitespace, followed by "=", followed by another string of characters up to the end of the line
            string pattern = @"^(?<key>[\w|\.]+)=(?<value>[^\r\n]*)";

            foreach (Match match in Regex.Matches(allText, pattern, RegexOptions.Multiline))
            {
                string key = match.Groups["key"].Value;
                string value = match.Groups["value"].Value;

                Debug.Assert(!string.IsNullOrWhiteSpace(key), "Regex error - matched property name name should not be null or empty");
                this.properties[key] = value;
            }
        }

        #endregion
    }
}
