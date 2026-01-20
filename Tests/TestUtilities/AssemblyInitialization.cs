/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

namespace TestUtilities;

public static class AssemblyInitialization
{
    public static void Initialize() =>
        // Make sure the UTs aren't affected by the hosting environment.
        // AzDo environment and our AzDo extension sets additional properties in an environment variable that would affect the setup.
        ResetEnvironmentVariables(typeof(EnvironmentVariables));

    private static void ResetEnvironmentVariables(Type variableNames)
    {
        foreach (var field in variableNames.GetFields())
        {
            Environment.SetEnvironmentVariable((string)field.GetValue(null), null);
        }
        foreach (var nestedType in variableNames.GetNestedTypes().Where(x => x != typeof(EnvironmentVariables.System))) // We don't want to reset the system ones
        {
            ResetEnvironmentVariables(nestedType);
        }
    }
}
