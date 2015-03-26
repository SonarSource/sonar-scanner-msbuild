//-----------------------------------------------------------------------
// <copyright file="PreprocessTestUtils.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.TeamBuild.Integration;
using TestUtilities;

namespace Sonar.TeamBuild.PreProcessor.Tests
{
    internal static class PreprocessTestUtils
    {
        /// <summary>
        /// Creates and returns an environment scope that contains all of the required
        /// TeamBuild environment variables
        /// </summary>
        public static EnvironmentVariableScope CreateValidTeamBuildScope(string tfsUri, string buildUri, string buildDir)
        {
            EnvironmentVariableScope scope = new EnvironmentVariableScope();
            scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.TfsCollectionUri, tfsUri);
            scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildUri, buildUri);
            scope.AddVariable(TeamBuildSettings.TeamBuildEnvironmentVariables.BuildDirectory, buildDir);
            return scope;
        }

    }
}
