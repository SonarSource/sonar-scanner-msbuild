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

namespace SonarScanner.MSBuild.Common;

public static class TelemetryKeys
{
    // Jre Bootstrapping
    public const string JreBootstrapping            = "dotnetenterprise.s4net.jre.bootstrapping";
    public const string JreDownload                 = "dotnetenterprise.s4net.jre.download";
    // Scanner Engine Bootstrapping
    public const string ScannerEngineBootstrapping  = "dotnetenterprise.s4net.scannerEngine.bootstrapping";
    public const string ScannerEngineDownload       = "dotnetenterprise.s4net.scannerEngine.download";
    // Scanner CLI Bootstrapping
    public const string ScannerCliDownload          = "dotnetenterprise.s4net.scannerCli.download";
    // Server Info
    public const string ServerInfoRegion            = "dotnetenterprise.s4net.serverInfo.region";
    public const string ServerInfoProduct           = "dotnetenterprise.s4net.serverInfo.product";
    public const string ServerInfoServerUrl         = "dotnetenterprise.s4net.serverInfo.serverUrl";
    public const string ServerInfoVersion           = "dotnetenterprise.s4net.serverInfo.version";
    // EndStep
    public const string EndstepLegacyTFS            = "dotnetenterprise.s4net.endstep.legacyTFS";
    public const string CoverageConversion          = "dotnetenterprise.s4net.end.coverage_conversion";
    // Begin
    public const string BeginRuntime                = "dotnetenterprise.s4net.begin.runtime";
}
