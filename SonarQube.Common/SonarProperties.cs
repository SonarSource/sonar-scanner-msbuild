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
        public const string ProjectBranch = "sonar.branch";

        public const string ProjectName = "sonar.projectName";
        public const string ProjectVersion = "sonar.projectVersion";

        // Miscellaneous
        public const string SourceEncoding = "sonar.sourceEncoding";

        public const string ProjectBaseDir = "sonar.projectBaseDir";
        public const string WorkingDirectory = "sonar.working.directory";
        public const string Verbose = "sonar.verbose";
        public const string LogLevel = "sonar.log.level";

        // Default property values

        /// <summary>
        /// Regex that determines if a project is a test project or not based on its path. 
        /// This regular expression matches paths where the filename contains the 'test' token. 
        /// Regex breakdown: 
        /// [^\\]*  - everything except \
        /// test    - that contains 'test'
        /// [^\\]*$ - and it doesn't end in \
        /// </summary>
        public const string DefaultTestProjectPattern = @"[^\\]*test[^\\]*$";
    }
}