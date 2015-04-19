//-----------------------------------------------------------------------
// <copyright file="PreprocessTestUtils.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.TeamBuild.Integration;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal static class PreprocessTestUtils
    {
        /// <summary>
        /// Creates and returns an environment scope that contains all of the required
        /// legacy TeamBuild environment variables
        /// </summary>
        public static EnvironmentVariableScope CreateValidLegacyTeamBuildScope(string tfsUri, string buildUri, string buildDir)
        {
            EnvironmentVariableScope scope = new EnvironmentVariableScope();
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamBuild, "true");
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_Legacy, tfsUri);
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_Legacy, buildUri);
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_Legacy, buildDir);

            // Make sure the other variables are not set
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildDirectory_TFS2015, null);
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.SQAnalysisRootPath, null);
            return scope;
        }

    }
}
