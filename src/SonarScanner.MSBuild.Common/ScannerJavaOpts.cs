/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Resolves the JVM options that are passed to the Scanner Engine process from the environment.
/// </summary>
public static class ScannerJavaOpts
{
    /// <summary>
    /// Reads the JVM options from SONAR_SCANNER_JAVA_OPTS, the name documented in the scanner bootstrapper guidelines and used by the other scanners.
    /// Falls back to SONAR_SCANNER_OPTS, the historical name of this scanner. When both are set, SONAR_SCANNER_JAVA_OPTS wins and SONAR_SCANNER_OPTS is ignored.
    /// </summary>
    /// <param name="name">The name of the environment variable the value was read from, or null if none is set.</param>
    /// <param name="value">The value of the environment variable, or null if none is set.</param>
    /// <returns>True if one of the variables is set to a value that is not empty or whitespace.</returns>
    public static bool TryGetFromEnvironment(out string name, out string value)
    {
        foreach (var candidate in new[] { EnvironmentVariables.SonarScannerJavaOptsVariableName, EnvironmentVariables.SonarScannerOptsVariableName })
        {
            var candidateValue = Environment.GetEnvironmentVariable(candidate);
            if (!string.IsNullOrWhiteSpace(candidateValue))
            {
                name = candidate;
                value = candidateValue;
                return true;
            }
        }
        name = null;
        value = null;
        return false;
    }
}
