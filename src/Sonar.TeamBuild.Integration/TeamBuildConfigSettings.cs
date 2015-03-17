//-----------------------------------------------------------------------
// <copyright file="TeamBuildConfigSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;

namespace Sonar.TeamBuild.Integration
{
    /// <summary>
    /// Helper class to provide strongly-typed extension methods to access TFS-specific analysis settings
    /// </summary>
    public static class TeamBuildConfigSettings
    {
        internal const string TfsUriSettingId = "TfsUri";
        internal const string BuildUriSettingId = "BuildUri";

        #region Public methods

        public static string GetTfsUri(this AnalysisConfig config)
        {
            return config.GetSetting(TfsUriSettingId, null);
        }

        public static void SetTfsUri(this AnalysisConfig config, string uri)
        {
            config.SetValue(TfsUriSettingId, uri);
        }

        public static string GetBuildUri(this AnalysisConfig config)
        {
            return config.GetSetting(BuildUriSettingId, null);
        }

        public static void SetBuildUri(this AnalysisConfig config, string uri)
        {
            config.SetValue(BuildUriSettingId, uri);
        }

        #endregion
    }
}
