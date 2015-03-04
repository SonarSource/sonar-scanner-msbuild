//-----------------------------------------------------------------------
// <copyright file="ISonarPropertyProvider.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Sonar.Common
{
    /// <summary>
    /// Provides Sonar property settings
    /// </summary>
    public interface ISonarPropertyProvider
    {
        /// <summary>
        /// Returns the property value to use for the specified property
        /// </summary>
        /// <param name="propertyName">The name of the property. Required.</param>
        /// <param name="defaultValue">The default value to use if the property name does not have a value</param>
        /// <returns>The property value to use</returns>
        string GetProperty(string propertyName, string defaultValue);
    }
}
