//-----------------------------------------------------------------------
// <copyright file="ISonarPropertyProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace Sonar.Common
{
    /// <summary>
    /// Provides SonarQube property settings
    /// </summary>
    public interface ISonarPropertyProvider
    {
        /// <summary>
        /// Returns the value for the supplied property.
        /// Throws if the property does not have a value.
        /// </summary>
        string GetProperty(string propertyName);

        /// <summary>
        /// Returns the property value to use for the specified property
        /// </summary>
        /// <param name="propertyName">The name of the property. Required.</param>
        /// <param name="defaultValue">The default value to use if the property name does not have a value</param>
        /// <returns>The property value to use</returns>
        string GetProperty(string propertyName, string defaultValue);
    }
}
