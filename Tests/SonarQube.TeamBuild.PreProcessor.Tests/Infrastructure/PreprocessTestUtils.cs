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
        /// Creates and returns an environment scope configured as if it
        /// is not running under TeamBuild
        /// </summary>
        public static EnvironmentVariableScope CreateValidNonTeamBuildScope()
        {
            EnvironmentVariableScope scope = new EnvironmentVariableScope();
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "false");

            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_Legacy, null);
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_TFS2015, null);

            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_Legacy, null);
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_TFS2015, null);

            return scope;
        }
    }
}
