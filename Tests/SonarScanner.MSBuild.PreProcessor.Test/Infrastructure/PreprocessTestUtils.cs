/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
 * mailto: info AT sonarsource DOT com
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

using SonarScanner.MSBuild.Common;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

internal static class PreprocessTestUtils
{
    /// <summary>
    /// Creates and returns an environment scope configured as if it
    /// is not running under TeamBuild
    /// </summary>
    public static EnvironmentVariableScope CreateValidNonTeamBuildScope() =>
        new EnvironmentVariableScope()
            .SetVariable(BuildSettings.EnvironmentVariables.IsInTeamFoundationBuild, "false")
            .SetVariable(BuildSettings.EnvironmentVariables.TfsCollectionUri_Legacy, null)
            .SetVariable(BuildSettings.EnvironmentVariables.TfsCollectionUri_TFS2015, null)
            .SetVariable(BuildSettings.EnvironmentVariables.BuildUri_Legacy, null)
            .SetVariable(BuildSettings.EnvironmentVariables.BuildUri_TFS2015, null)
            .SetVariable(EnvScannerPropertiesProvider.ENV_VAR_KEY, null); // The Sonar AzDO tasks set and use this environment variable
}
