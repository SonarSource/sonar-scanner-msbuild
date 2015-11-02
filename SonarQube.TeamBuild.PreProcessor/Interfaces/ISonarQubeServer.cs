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
    public interface ISonarQubeServer
    {
        string Server { get; }

        IEnumerable<string> GetActiveRuleKeys(string qualityProfile, string language, string repository);

        IEnumerable<string> GetInstalledPlugins();

        IDictionary<string, string> GetInternalKeys(string repository);

        IDictionary<string, string> GetProperties(string projectKey, ILogger logger);

        bool TryGetQualityProfile(string projectKey, string language, out string qualityProfile);
    }
}