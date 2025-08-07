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

using SonarScanner.MSBuild.PreProcessor.Caching;

namespace SonarScanner.MSBuild.PreProcessor.EngineResolution;

public sealed record EngineMetadata
{
    public string Filename { get; }
    public string Sha256 { get; }
    public Uri DownloadUrl { get; } // Optional, only exists for SonarCloud

    public EngineMetadata(string filename, string sha256, Uri downloadUrl)
    {
        Filename = filename;
        Sha256 = sha256;
        DownloadUrl = downloadUrl;
    }

    public FileDescriptor ToDescriptor() =>
        new(Filename, Sha256);
}
