//-----------------------------------------------------------------------
// <copyright file="AggregatePropertiesProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Properties provider that aggregates the properties from multiple "child" providers.
    /// The child providers are checked in order until one of them returns a value.
    /// </summary>
    public class AggregatePropertiesProvider : IAnalysisPropertyProvider
    {
        /// <summary>
        /// Ordered list of child providers
        /// </summary>
        private readonly IAnalysisPropertyProvider[] providers;

        #region Public methods

        public AggregatePropertiesProvider(params IAnalysisPropertyProvider[] providers)
        {
            if (providers == null)
            {
                throw new ArgumentNullException("providers");
            }

            this.providers = providers;
        }

        #endregion

        #region IAnalysisPropertyProvider interface

        public IEnumerable<Property> GetAllProperties()
        {
            HashSet<string> allKeys = new HashSet<string>(this.providers.SelectMany(p => p.GetAllProperties().Select(s => s.Id)));

            IList<Property> allProperties = new List<Property>();
            foreach (string key in allKeys)
            {
                Property property;
                bool match = this.TryGetProperty(key, out property);

                Debug.Assert(match, "Expecting to find value for all keys. Key: " + key);
                allProperties.Add(property);
            }

            return allProperties;
        }

        public bool TryGetProperty(string key, out Property property)
        {
            property = null;
            bool found = false;

            foreach (IAnalysisPropertyProvider current in this.providers)
            {
                if (current.TryGetProperty(key, out property))
                {
                    found = true;
                    break;
                }
            }

            return found;
        }

        #endregion
    }
}