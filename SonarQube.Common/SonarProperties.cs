/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
 
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

        public const string Organization = "sonar.organization";
    }
}