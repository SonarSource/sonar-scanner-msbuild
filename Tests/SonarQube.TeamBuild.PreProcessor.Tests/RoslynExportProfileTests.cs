//-----------------------------------------------------------------------
// <copyright file="RoslynExportProfileTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class RoslynExportProfileTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void RoslynProfile_LoadValidProfile_Succeeds()
        {
            string validXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RoslynExportProfile Version=""1.0"">
  <Configuration>
    <RuleSet Name=""Rules for SonarQube"" Description=""This rule set was automatically generated from SonarQube."" ToolsVersion=""14.0"">
      <Rules>
        <Rule Id=""Foo""/>
      </Rules>
    </RuleSet>

    <AdditionalFiles>
      <AdditionalFile FileName=""SonarLint.xml"" >PHRlc3Q+PHN1Yi8+PC90ZXN0</AdditionalFile>
      <AdditionalFile FileName=""MyAnalyzer.xml"" >PEZvby8+</AdditionalFile>
    </AdditionalFiles>
  </Configuration>

  <Deployment>
    <Plugins>
      <Plugin Key=""csharp"" Version=""4.5-SNAPSHOT"" StaticResourceName=""SonarLint.zip"" />
      <Plugin Key=""roslyn.wintellect.analyzers"" Version=""1.0.5.0"" StaticResourceName=""Wintellect.Analyzers.1.0.zip"" />
    </Plugins>

    <NuGetPackages>
      <NuGetPackage Id=""SonarLint"" Version=""1.3.0""/>
      <NuGetPackage Id=""My.Analyzers"" Version=""1.0.5.0-rc1""/>
    </NuGetPackages>
  </Deployment>
</RoslynExportProfile>";

            RoslynExportProfile profile = LoadAndCheckXml(validXml);

            AssertExpectedAdditionalFileExists("SonarLint.xml", profile);
            AssertExpectedAdditionalFileExists("MyAnalyzer.xml", profile);

            AssertExpectedPackageExists("SonarLint", "1.3.0", profile);
            AssertExpectedPackageExists("My.Analyzers", "1.0.5.0-rc1", profile);

            AssertExpectedPluginExists("csharp", "4.5-SNAPSHOT", "SonarLint.zip", profile);
            AssertExpectedPluginExists("roslyn.wintellect.analyzers", "1.0.5.0", "Wintellect.Analyzers.1.0.zip", profile);
        }

        [TestMethod]
        public void RoslynProfile_LoadRealExample_Succeeds()
        {
            RoslynExportProfile profile = LoadAndCheckXml(SampleExportXml.RoslynExportedValidSonarLintXml);

            AssertExpectedAdditionalFileExists(SampleExportXml.RoslynExportedAdditionalFileName, profile);
            AssertExpectedPackageExists(SampleExportXml.RoslynExportedPackageId, SampleExportXml.RoslynExportedPackageVersion, profile);

            Assert.Inconclusive("TODO: update test once an updated version of the real roslyn-cs exporter xml is available");
            // TODO: update the real XML once the C# plugin has been changed
            //AssertExpectedPluginExists(SampleExportXml.RoslynExportedPluginKey, SampleExportXml.RoslynExportedPackageVersion, SampleExportXml.RoslynExportedAdditionalFileName, profile);
        }

        #endregion

        #region Checks

        private static RoslynExportProfile LoadAndCheckXml(string xml)
        {
            RoslynExportProfile profile = null;
            using (StringReader reader = new StringReader(xml))
            {
                profile = RoslynExportProfile.Load(reader);
            }

            Assert.IsNotNull(profile);
            Assert.IsNotNull(profile.Configuration);
            Assert.IsNotNull(profile.Configuration.RuleSet);

            return profile;
        }

        private static void AssertExpectedAdditionalFileExists(string fileName, RoslynExportProfile profile)
        {
            Assert.IsNotNull(profile.Configuration.AdditionalFiles);
            AdditionalFile[] matches = profile.Configuration.AdditionalFiles.Where(f => string.Equals(fileName, f.FileName, System.StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.AreNotEqual(0, matches.Length, "Expected additional file was not found. File name: {0}", fileName);
            Assert.AreEqual(1, matches.Length, "Expecting only one matching file. File name: {0}", fileName);
            Assert.IsNotNull(matches[0].Content, "File content should not be null. File name: {0}", fileName);
        }

        private static void AssertExpectedPackageExists(string packageId, string version, RoslynExportProfile profile)
        {
            Assert.IsNotNull(profile.Deployment);
            NuGetPackageInfo[] matches = profile.Deployment.NuGetPackages.Where(
                p => string.Equals(packageId, p.Id, System.StringComparison.Ordinal) &&
                        string.Equals(version, p.Version, System.StringComparison.Ordinal)).ToArray();
            Assert.AreNotEqual(0, matches.Length, "Expected package was not found. Package: {0}, Version: {1}", packageId, version);
            Assert.AreEqual(1, matches.Length, "Expecting only one matching package. Package: {0}, Version: {1}", packageId, version);
        }

        private static void AssertExpectedPluginExists(string pluginKey, string version, string staticResourceName, RoslynExportProfile profile)
        {
            Assert.IsNotNull(profile.Deployment);
            Plugin[] matches = profile.Deployment.Plugins.Where(
                p => string.Equals(pluginKey, p.Key, System.StringComparison.Ordinal) &&
                        string.Equals(version, p.Version, System.StringComparison.Ordinal) &&
                        string.Equals(staticResourceName, p.StaticResourceName, System.StringComparison.Ordinal)).ToArray();
            Assert.AreNotEqual(0, matches.Length, "Expected plugin entry was not found. Plugin: {0}, Version: {1}, Resource: {2}", pluginKey, version, staticResourceName);
            Assert.AreEqual(1, matches.Length, "Expecting only one matching plugin. Package: {0}, Version: {1}, Resource: {2}", pluginKey, version, staticResourceName);
        }

        #endregion
    }
}
