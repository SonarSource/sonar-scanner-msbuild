//-----------------------------------------------------------------------
// <copyright file="IAnalysisPropertyProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace SonarQube.Common
{
    /// <summary>
    /// Provides analysis property properties
    /// </summary>
    /// <remarks>The properties could come from different sources e.g. a file, command line arguments, the SonarQube server</remarks>
    public interface IAnalysisPropertyProvider
    {
        IEnumerable<Property> GetAllProperties();

        bool TryGetProperty(string key, out Property property);
    }

    public static class AnalysisPropertyProviderExtensions
    {
        public static bool TryGetValue(this IAnalysisPropertyProvider provider, string name, out string value)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException("name");
            }

            Property property;
            if (provider.TryGetProperty(name, out property))
            {
                value = property.Value;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
    }
}
