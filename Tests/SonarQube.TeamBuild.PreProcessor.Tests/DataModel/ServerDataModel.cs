//-----------------------------------------------------------------------
// <copyright file="ServerDataModel.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class ServerDataModel
    {
        private readonly IList<Repository> repos;
        private readonly IList<QualityProfile> qualityProfiles;
        private readonly IDictionary<string, byte[]> embeddedFilesMap;

        public ServerDataModel()
        {
            this.repos = new List<Repository>();
            this.qualityProfiles = new List<QualityProfile>();
            this.ServerProperties = new Dictionary<string, string>();
            this.InstalledPlugins = new List<string>();
            this.embeddedFilesMap = new Dictionary<string, byte[]>();
        }

        public IEnumerable<Repository> Repositories {  get { return this.repos; } }

        public IEnumerable<QualityProfile> QualityProfiles { get { return this.qualityProfiles; } }

        public IDictionary<string, string> ServerProperties { get; set; }

        public IList<string> InstalledPlugins { get; set; }

        #region Builder methods

        public Repository AddRepository(string repositoryKey, string language)
        {
            Repository repo = this.FindRepository(repositoryKey, language);
            Assert.IsNull(repo, "A repository already exists. Key: {0}, language: {1}", repositoryKey, language);

            repo = new Repository(repositoryKey, language);
            this.repos.Add(repo);
            return repo;
        }

        public QualityProfile AddQualityProfile(string name, string language)
        {
            QualityProfile profile = this.FindProfile(name, language);
            Assert.IsNull(profile, "A quality profile already exists. Name: {0}, language: {1}", name, language);
                
            profile = new QualityProfile(name, language);
            this.qualityProfiles.Add(profile);
            return profile;
        }

        public void AddRuleToProfile(string ruleId, string profileName)
        {
            // We're assuming rule ids are unique across repositories
            Rule rule = this.repos.SelectMany(repo => repo.Rules.Where(r => string.Equals(ruleId, r.Key))).Single();

            QualityProfile profile = this.FindProfile(profileName, rule.Language);

            profile.AddRule(rule);
        }

        public void AddEmbeddedFile(string pluginKey, string embeddedFileName, byte[] content)
        {
            this.embeddedFilesMap.Add(GetEmbeddedFileKey(pluginKey, embeddedFileName), content);
        }

        public void AddEmbeddedZipFile(string pluginKey, string embeddedFileName, params string[] contentFileNames)
        {
            this.embeddedFilesMap.Add(GetEmbeddedFileKey(pluginKey, embeddedFileName), CreateDummyZipFile(contentFileNames));
        }

        #endregion

        #region Locator methods

        public Repository FindRepository(string repositoryKey, string language)
        {
            // Multiple profiles can have the same name; look for a profile where the language matches the rule language
            Repository repo = this.repos.SingleOrDefault(r => string.Equals(r.Key, repositoryKey) && string.Equals(r.Language, language));
            return repo;
        }

        public QualityProfile FindProfile(string name, string language)
        {
            // Multiple profiles can have the same name; look for a profile where the language matches the rule language
            QualityProfile profile = this.qualityProfiles.SingleOrDefault(qp => string.Equals(qp.Name, name) && string.Equals(qp.Language, language));
            return profile;
        }

        public byte[] FindEmbeddedFile(string pluginKey, string embeddedFileName)
        {
            byte[] content;
            this.embeddedFilesMap.TryGetValue(GetEmbeddedFileKey(pluginKey, embeddedFileName), out content);
            return content;
        }

        private static string GetEmbeddedFileKey(string pluginKey, string embeddedFileName)
        {
            return pluginKey + "___" + embeddedFileName;
        }
        #endregion


        #region Private methods

        private byte[] CreateDummyZipFile(params string[] contentFileNames)
        {
            string fileName = "dummy.zip";

            // Create a temporary directory structure
            string tempDir = Path.Combine(Path.GetTempPath(), "sqTestsTemp", System.Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            string zipDir = Path.Combine(tempDir, "zipDir");
            Directory.CreateDirectory(zipDir);
            string zippedFilePath = Path.Combine(tempDir, fileName);

            // Create and read the zip file           
            foreach (string contentFileName in contentFileNames)
            {
                TestUtils.CreateTextFile(zipDir, contentFileName, "dummy file content");
            }

            ZipFile.CreateFromDirectory(zipDir, zippedFilePath);
            byte[] zipData = File.ReadAllBytes(zippedFilePath);

            // Cleanup
            Directory.Delete(tempDir, true);

            return zipData;
        }

        #endregion
    }
}
