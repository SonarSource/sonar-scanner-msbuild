//-----------------------------------------------------------------------
// <copyright file="AggregatePropertiesProviderTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class AggregatePropertiesProviderTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("Properties")]
        public void AggProperties_NullOrEmptyList()
        {
            // 1. Null -> error
            AssertException.Expects<ArgumentNullException>(() => new AggregatePropertiesProvider(null));

            // 2. Empty list of providers -> valid but returns nothing
            AggregatePropertiesProvider provider = new AggregatePropertiesProvider(new IAnalysisPropertyProvider[] { });

            Assert.AreEqual(0, provider.GetAllProperties().Count());
            Property actualProperty;
            bool success = provider.TryGetProperty("any key", out actualProperty);

            Assert.IsFalse(success, "Not expecting a property to be returned");
            Assert.IsNull(actualProperty, "Returned property should be null");
        }

        [TestMethod]
        [TestCategory("Properties")]
        public void AggProperties_Aggregation()
        {
            // Checks the aggregation works as expected

            // 0. Setup
            ListPropertiesProvider provider1 = new ListPropertiesProvider();
            provider1.AddProperty("shared.key.A", "value A from one");
            provider1.AddProperty("shared.key.B", "value B from one");
            provider1.AddProperty("p1.unique.key.1", "p1 unique value 1");

            ListPropertiesProvider provider2 = new ListPropertiesProvider();
            provider2.AddProperty("shared.key.A", "value A from two");
            provider2.AddProperty("shared.key.B", "value B from two");
            provider2.AddProperty("p2.unique.key.1", "p2 unique value 1");

            ListPropertiesProvider provider3 = new ListPropertiesProvider();
            provider3.AddProperty("shared.key.A", "value A from three"); // this provider only has one of the shared values
            provider3.AddProperty("p3.unique.key.1", "p3 unique value 1");


            // 1. Ordering
            AggregatePropertiesProvider aggProvider = new AggregatePropertiesProvider(provider1, provider2, provider3);

            AssertSettingsCount(5, aggProvider);

            AssertPropertyValue("shared.key.A", "value A from one", aggProvider);
            AssertPropertyValue("shared.key.B", "value B from one", aggProvider);

            AssertPropertyValue("p1.unique.key.1", "p1 unique value 1", aggProvider);
            AssertPropertyValue("p2.unique.key.1", "p2 unique value 1", aggProvider);
            AssertPropertyValue("p3.unique.key.1", "p3 unique value 1", aggProvider);

            // 2. Reverse the order and try again
            aggProvider = new AggregatePropertiesProvider(provider3, provider2, provider1);

            AssertSettingsCount(5, aggProvider);

            AssertPropertyValue("shared.key.A", "value A from three", aggProvider);
            AssertPropertyValue("shared.key.B", "value B from two", aggProvider);

            AssertPropertyValue("p1.unique.key.1", "p1 unique value 1", aggProvider);
            AssertPropertyValue("p2.unique.key.1", "p2 unique value 1", aggProvider);
            AssertPropertyValue("p3.unique.key.1", "p3 unique value 1", aggProvider);
        }

        #endregion

        #region Private methods

        #endregion

        #region Checks

        private static void AssertSettingsCount(int expected, IAnalysisPropertyProvider provider)
        {
            IEnumerable<Property> allProperties = provider.GetAllProperties();
            Assert.IsNotNull(allProperties, "Returned list of properties should not be null");
            Assert.AreEqual(expected, allProperties.Count(), "Unexpected number of properties returned");
        }

        private static void AssertPropertyValue(string key, string expectedValue, IAnalysisPropertyProvider actualProvider)
        {
            Property actualProperty;
            bool exists = actualProvider.TryGetProperty(key, out actualProperty);
            Assert.IsTrue(exists, "Specified property does not exist. Key: {0}", key);
            Assert.AreEqual(expectedValue, actualProperty.Value, "Property does not have the expected value. Key: {0}", key);
        }

        #endregion
    }
}