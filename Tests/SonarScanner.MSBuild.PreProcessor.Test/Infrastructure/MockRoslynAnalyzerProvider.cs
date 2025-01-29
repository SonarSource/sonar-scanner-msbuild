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

using System.Collections.Generic;
using FluentAssertions;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test;

internal class MockRoslynAnalyzerProvider(BuildSettings buildSettings, IAnalysisPropertyProvider sonarProperties, IEnumerable<SonarRule> rules, string language) : RoslynAnalyzerProvider(new MockAnalyzerInstaller(), new TestLogger(), buildSettings, sonarProperties, rules, language)
{
    public AnalyzerSettings SettingsToReturn { get; set; }

    public IAnalysisPropertyProvider SuppliedSonarProperties { get; private set; }

    public override AnalyzerSettings SetupAnalyzer()
    {
        teamBuildSettings.Should().NotBeNull();
        sonarProperties.Should().NotBeNull();
        language.Should().NotBeNullOrWhiteSpace();
        SuppliedSonarProperties = sonarProperties;
        return SettingsToReturn;
    }
}
