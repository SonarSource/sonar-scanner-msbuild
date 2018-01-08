/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using SonarQube.Common;

namespace SonarQube.TeamBuild.Integration
{
    /// <summary>
    /// Helper class to provide strongly-typed extension methods to access TFS-specific analysis settings
    /// </summary>
    public static class TeamBuildAnalysisSettings
    {
        internal const string TfsUriSettingId = "TfsUri";
        internal const string BuildUriSettingId = "BuildUri";

        #region Public methods

        public static string GetTfsUri(this AnalysisConfig config)
        {
            return config.GetConfigValue(TfsUriSettingId, null);
        }

        public static void SetTfsUri(this AnalysisConfig config, string uri)
        {
            config.SetConfigValue(TfsUriSettingId, uri);
        }

        public static string GetBuildUri(this AnalysisConfig config)
        {
            return config.GetConfigValue(BuildUriSettingId, null);
        }

        public static void SetBuildUri(this AnalysisConfig config, string uri)
        {
            config.SetConfigValue(BuildUriSettingId, uri);
        }

        #endregion Public methods
    }
}
