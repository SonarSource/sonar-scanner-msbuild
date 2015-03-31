//-----------------------------------------------------------------------
// <copyright file="SonarProperties.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.Common
{
    /// <summary>
    /// Defines symbolic names for common SonarQube properties
    /// </summary>
    public static class SonarProperties
    {
        // SonarQube server settings
        public const string HostUrl = "sonar.host.url";
        public const string SonarUserName = "sonar.login";
        public const string SonarPassword = "sonar.password";

        // Database settings
        public const string DbConnectionString = "sonar.jdbc.url";
        public const string DbUserName = "sonar.jdbc.username";
        public const string DbPassword = "sonar.jdbc.password";

        // SonarQube project settings
        public const string ProjectKey = "sonar.projectKey";
        public const string ProjectName = "sonar.projectName";
        public const string ProjectVersion = "sonar.projectVersion";

        // Miscellaneous
        public const string SourceEncoding = "sonar.sourceEncoding";
    }
}
