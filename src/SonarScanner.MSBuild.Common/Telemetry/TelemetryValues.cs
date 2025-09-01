﻿/*
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

namespace SonarScanner.MSBuild.Common;

public static class TelemetryValues
{
    public static class ScannerEngineDownload
    {
        public static readonly string Downloaded = nameof(Downloaded);
        public static readonly string CacheHit = nameof(CacheHit);
        public static readonly string UserSupplied = nameof(UserSupplied);
        public static readonly string Failed = nameof(Failed);
    }

    public static class NewBootstrapping
    {
        public static readonly string Unsupported = nameof(Unsupported);
        public static readonly string Enabled = nameof(Enabled);
        public static readonly string Disabled = nameof(Disabled);
    }

    public static class EndstepLegacyTFS
    {
        public static readonly string Called = nameof(Called);
        public static readonly string NotCalled = nameof(NotCalled);
    }

    public static class Product
    {
        public static readonly string Server = "SQ_Server";
        public static readonly string Cloud = "SQ_Cloud";
    }
}
