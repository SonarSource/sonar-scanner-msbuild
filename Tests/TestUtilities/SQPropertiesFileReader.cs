/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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
 
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;

namespace TestUtilities
{
    /// <summary>
    /// Utility class that reads properties from a standard format SonarQube properties file (e.g. sonar-scanner.properties)
    /// </summary>
    public class SQPropertiesFileReader
    {


        /// <summary>
        /// Mapping of property names to values
        /// </summary>
        private JavaProperties properties;

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
            string actualValue = this.properties.GetProperty(key);
            bool found = actualValue != null;

            Assert.IsTrue(found, "Expected setting was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, actualValue, "Property does not have the expected value. Key: {0}", key);
        }

        public void AssertSettingDoesNotExist(string key)
        {
            string actualValue = this.properties.GetProperty(key);
            bool found = actualValue != null;

            Assert.IsFalse(found, "Not expecting setting to be found. Key: {0}, value: {1}", key, actualValue);
        }

        #endregion

        #region FilePropertiesProvider

        private void ExtractProperties(string fullPath)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(fullPath), "fullPath should be specified");

            this.properties = new JavaProperties();
            this.properties.Load(File.Open(fullPath, FileMode.Open));
        }

        #endregion
    }
}
