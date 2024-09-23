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

using System.Collections.Generic;
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PreProcessor.AnalysisConfigProcessing.Processors;

/// <summary>
/// This class removes sonar.sources and sonar.tests from the local settings, as we are setting them automatically on the end step.
/// </summary>
public class AnalysisScopeProcessor(ProcessedArgs localSettings, IDictionary<string, string> serverProperties)
    : AnalysisConfigProcessorBase(localSettings, serverProperties)
{
    private static readonly string[] IgnoreParameters =
    [
        SonarProperties.Sources,
        SonarProperties.Tests
    ];

    public override void Update(AnalysisConfig config) =>
        config.LocalSettings.RemoveAll(x => IgnoreParameters.Contains(x.Id));
}
