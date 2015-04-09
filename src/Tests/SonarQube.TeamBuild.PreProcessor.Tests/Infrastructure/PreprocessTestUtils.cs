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
            scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.IsInTeamBuild, "true");
            scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.TfsCollectionUri_Legacy, tfsUri);
            scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildUri_Legacy, buildUri);
            scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildDirectory_Legacy, buildDir);
            return scope;
        }

    }
}
