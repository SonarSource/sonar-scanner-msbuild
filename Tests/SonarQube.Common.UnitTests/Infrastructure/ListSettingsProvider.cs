//-----------------------------------------------------------------------
// <copyright file="ListPropertiesProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace SonarQube.Common.UnitTests
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

            Property existing;
            if (this.TryGetProperty(key, out existing))
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