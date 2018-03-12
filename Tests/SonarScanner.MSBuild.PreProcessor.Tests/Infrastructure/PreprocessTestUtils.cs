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

using SonarScanner.MSBuild.TFS;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Tests
{
    internal static class PreprocessTestUtils
    {
        /// <summary>
        /// Creates and returns an environment scope configured as if it
        /// is not running under TeamBuild
        /// </summary>
        public static EnvironmentVariableScope CreateValidNonTeamBuildScope()
        {
            var scope = new EnvironmentVariableScope();
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "false");

            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_Legacy, null);
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.TfsCollectionUri_TFS2015, null);

            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_Legacy, null);
            scope.SetVariable(TeamBuildSettings.EnvironmentVariables.BuildUri_TFS2015, null);

            return scope;
        }
    }
}
