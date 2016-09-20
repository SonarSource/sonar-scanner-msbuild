//-----------------------------------------------------------------------
// <copyright file="SQPropertiesFileReader.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace TestUtilities
{
    /// <summary>
    /// Utility class that reads properties from a standard format SonarQube properties file (e.g. sonar-scanner.properties)
    /// </summary>
    public class SQPropertiesFileReader
    {
        // NB: this expression only works for single-line values
        // Regular expression pattern: we're looking for matches that:
        // * start at the beginning of a line
        // * start with a character or number
        // * are in the form [key]=[value],
        // * where [key] can  
        //   - starts with an alpanumeric character.
        //   - can be followed by any number of alphanumeric characters or .
        //   - whitespace is not allowed
        // * [value] can contain anything
        public const string KeyValueSettingPattern = @"^(?<key>\w[\w\d\.-]*)=(?<value>[^\r\n]+)";

        /// <summary>
        /// Mapping of property names to values
        /// </summary>
        private IDictionary<string, string> properties;

        private readonly string propertyFilePath;

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
                throw new ArgumentNullException("fullPath");
            }
                        
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException();
            }

            this.propertyFilePath = fullPath;
            this.ExtractProperties(fullPath);
        }

        public void AssertSettingExists(string key, string expectedValue)
        {
            string actualValue;
            bool found = this.properties.TryGetValue(key, out actualValue);

            Assert.IsTrue(found, "Expected setting was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, actualValue, "Property does not have the expected value. Key: {0}", key);
        }

        public void AssertSettingDoesNotExist(string key)
        {
            string actualValue;
            bool found = this.properties.TryGetValue(key, out actualValue);

            Assert.IsFalse(found, "Not expecting setting to be found. Key: {0}, value: {1}", key, actualValue);
        }

        #endregion

        #region FilePropertiesProvider

        private void ExtractProperties(string fullPath)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(fullPath), "fullPath should be specified");

            this.properties = new Dictionary<string, string>(ConfigSetting.SettingKeyComparer);
            string allText = File.ReadAllText(fullPath);

            foreach (Match match in Regex.Matches(allText, KeyValueSettingPattern, RegexOptions.Multiline))
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
