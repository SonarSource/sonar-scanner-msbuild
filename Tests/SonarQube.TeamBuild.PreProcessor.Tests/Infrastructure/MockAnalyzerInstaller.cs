//-----------------------------------------------------------------------
// <copyright file="MockAnalyzerInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System.Collections.Generic;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class MockAnalyzerInstaller : IAnalyzerInstaller
    {
        #region Test helpers

        public ISet<string> AssemblyPathsToReturn { get; set; }
        
        public IEnumerable<NuGetPackageInfo> SuppliedPackages { get; private set; }

        #endregion

        #region Checks

        public void AssertExpectedPackagesRequested(IDictionary<string, string> packages)
        {
            foreach(KeyValuePair<string, string> kvp in packages)
            {
                AssertExpectedPackageRequested(kvp.Key, kvp.Value);
            }
            Assert.AreEqual(packages.Count, this.SuppliedPackages.Count(), "Unexpected number of packages requested");
        }

        public void AssertExpectedPackageRequested(string id, string version)
        {
            Assert.IsNotNull(this.SuppliedPackages, "No packages have been requested");
            bool found = this.SuppliedPackages.Any(p => string.Equals(id, p.Id, System.StringComparison.Ordinal) && string.Equals(version, p.Version, System.StringComparison.Ordinal));
            Assert.IsTrue(found, "Expected package was not requested. Id: {0}, version: {1}", id, version);
        }

        #endregion

        #region IAnalyzerInstaller methods

        IEnumerable<string> IAnalyzerInstaller.InstallAssemblies(IEnumerable<NuGetPackageInfo> packages)
        {
            Assert.IsNotNull(packages, "Supplied list of packages should not be null");

            this.SuppliedPackages = packages;

            return this.AssemblyPathsToReturn;
        }

        #endregion
    }
}
