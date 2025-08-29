/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

public static class TelemetryTestUtils
{
    public static void AssertTelemetryContent(ILogger logger, string telemetryDirectory, params string[] telemetryMessages)
    {
        Directory.CreateDirectory(telemetryDirectory);
        logger.WriteTelemetry(telemetryDirectory);
        var expectedTelemetryLocation = Path.Combine(telemetryDirectory, FileConstants.TelemetryFileName);
        File.Exists(expectedTelemetryLocation).Should().BeTrue();
        var actualTelemetry = File.ReadAllText(expectedTelemetryLocation);

        actualTelemetry.Should().BeEquivalentTo(Contents(telemetryMessages));
    }

    // Contents are created with string builder to have the correct line endings for each OS
    private static string Contents(params string[] telemetryMessages)
    {
        var st = new StringBuilder();
        foreach (var message in telemetryMessages)
        {
            st.AppendLine(message);
        }
        return st.ToString();
    }
}
