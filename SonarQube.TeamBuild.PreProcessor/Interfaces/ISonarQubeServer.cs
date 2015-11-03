//-----------------------------------------------------------------------
// <copyright file="ISonarQubeServer.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor
{

    /// <summary>
    /// Provides an abstraction for the interactions with the SonarQube server
    /// </summary>
    public interface ISonarQubeServer
    {
        /// <summary>
        /// Get all the active rules (of the given language and repository) in the given quality profile name
        /// </summary>
        IEnumerable<string> GetActiveRuleKeys(string qualityProfile, string language, string repository);

        /// <summary>
        /// Get all keys of all installed plugins
        /// </summary>
        IEnumerable<string> GetInstalledPlugins();

        /// <summary>
        /// Get the key -> internal keys mapping (of the given language and repository)
        /// </summary>
        IDictionary<string, string> GetInternalKeys(string repository);

        /// <summary>
        /// Get all the properties of a project
        /// </summary>
        IDictionary<string, string> GetProperties(string projectKey, ILogger logger);

        /// <summary>
        /// Get the name of the quality profile (of the given language) to be used by the given project key
        /// </summary>
        bool TryGetQualityProfile(string projectKey, string language, out string qualityProfile);
    }
}