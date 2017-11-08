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
 
using System;
using System.Collections.Generic;

namespace SonarQube.Common
{
    /// <summary>
    /// Simple settings provider that returns values from a list
    /// </summary>
    public class ListPropertiesProvider : IAnalysisPropertyProvider
    {
        private readonly IList<Property> properties;

        #region Public methods

        public ListPropertiesProvider()
        {
            this.properties = new List<Property>();
        }

        public ListPropertiesProvider(IEnumerable<Property> properties)
        {
            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            this.properties = new List<Property>(properties);
        }

        public Property AddProperty(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }

            if (this.TryGetProperty(key, out Property existing))
            {
                throw new ArgumentOutOfRangeException("key");
            }

            Property newProperty = new Property() { Id = key, Value = value };
            this.properties.Add(newProperty);
            return newProperty;
        }

        #endregion

        #region IAnalysisProperiesProvider interface

        public IEnumerable<Property> GetAllProperties()
        {
            return properties;
        }

        public bool TryGetProperty(string key, out Property property)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }

            return Property.TryGetProperty(key, this.properties, out property);
        }

        #endregion
    }
}