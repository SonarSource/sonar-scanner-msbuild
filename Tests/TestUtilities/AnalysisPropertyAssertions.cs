//-----------------------------------------------------------------------
// <copyright file="AnalysisPropertyAssertions.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using System.Collections.Generic;
using System.Linq;

namespace TestUtilities
{
    public static class AnalysisPropertyAssertions
    {
        public static void AssertExpectedPropertyCount(this IAnalysisPropertyProvider provider, int expected)
        {
            IEnumerable<Property> allProperties = provider.GetAllProperties();
            Assert.IsNotNull(allProperties, "Returned list of properties should not be null");
            Assert.AreEqual(expected, allProperties.Count(), "Unexpected number of properties returned");
        }

        public static void AssertExpectedPropertyValue(this IAnalysisPropertyProvider provider, string key, string expectedValue)
        {
            Property property;
            bool found = provider.TryGetProperty(key, out property);

            Assert.IsTrue(found, "Expected property was not found. Key: {0}", key);
            Assert.AreEqual(expectedValue, property.Value, "");
        }

        public static void AssertPropertyDoesNotExist(this IAnalysisPropertyProvider provider, string key)
        {
            Property property;
            bool found = provider.TryGetProperty(key, out property);

            Assert.IsFalse(found, "Not expecting the property to exist. Key: {0}, value: {1}", key);
        }
    }
}
