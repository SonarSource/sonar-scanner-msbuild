//-----------------------------------------------------------------------
// <copyright file="ConfigurablePropertyProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;

namespace TestUtilities
{
    /// <summary>
    /// Simple implementation of <see cref="ISonarPropertyProvider"/> for testing
    /// </summary>
    public sealed class ConfigurablePropertyProvider : ISonarPropertyProvider
    {
        public ConfigurablePropertyProvider()
        {
            this.PropertyBag = new Dictionary<string, string>(StringComparer.InvariantCulture);
        }

        #region Test helper methods

        public IDictionary<string, string> PropertyBag { get; private set; }

        #endregion

        #region ISonarPropertyProvider interface

        string ISonarPropertyProvider.GetProperty(string propertyName)
        {
            string value;
            if (!this.PropertyBag.TryGetValue(propertyName, out value))
            {
                throw new ArgumentException(propertyName);
            }
            return value;
        }

        string ISonarPropertyProvider.GetProperty(string propertyName, string defaultValue)
        {
            string value;
            if (!this.PropertyBag.TryGetValue(propertyName, out value))
            {
                value = defaultValue;
            }
            return value;
        }

        #endregion
    }
}
