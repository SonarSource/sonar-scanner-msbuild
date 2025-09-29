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

namespace SonarScanner.MSBuild.PreProcessor.JreResolution;

public sealed class JreMetadata(string Id, string Filename, string JavaPath, Uri DownloadUrl, string Sha256)
{
    public string Id { get; } = Id;                     // Optional, only exists for SonarQube
    public string Filename { get; } = Filename;
    public string Sha256 { get; } = Sha256;
    public string JavaPath { get; } = JavaPath;
    public Uri DownloadUrl { get; } = DownloadUrl;   // Optional, only exists for SonarCloud

    public ArchiveDescriptor ToDescriptor() =>
        new(Filename, Sha256, JavaPath);
}
