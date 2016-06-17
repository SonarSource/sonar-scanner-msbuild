//-----------------------------------------------------------------------
// <copyright file="ISonarQubeServer.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using SonarQube.Common;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;

namespace SonarQube.TeamBuild.PreProcessor
{

    /// <summary>
    /// Provides an abstraction for the interactions with the SonarQube server
    /// </summary>
    public interface ISonarQubeServer
    {
        IList<string> GetInactiveRules(string qprofile, string language);

        IList<ActiveRule> GetActiveRules(string qprofile);

        /// <summary>
        /// Get all keys of all installed plugins
        /// </summary>
        IEnumerable<string> GetInstalledPlugins();

        /// <summary>
        /// Get all the properties of a project
        /// </summary>
        IDictionary<string, string> GetProperties(string projectKey, string projectBranch);

        /// <summary>
        /// Get the name of the quality profile (of the given language) to be used by the given project key
        /// </summary>
        bool TryGetQualityProfile(string projectKey, string projectBranch, string language, out string qualityProfile);

        /// <summary>
        /// Attempts to download a file embedded in the "static" folder in a plugin jar
        /// </summary>
        /// <param name="pluginKey">The key of the plugin containing the file</param>
        /// <param name="embeddedFileName">The name of the file to download</param>
        /// <param name="targetDirectory">The directory to which the file should be downloaded</param>
        bool TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory);
    }
}