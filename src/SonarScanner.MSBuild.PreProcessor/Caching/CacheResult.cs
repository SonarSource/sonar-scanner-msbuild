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

namespace SonarScanner.MSBuild.PreProcessor.Caching;

public abstract record CacheResult;

/// <summary>
/// File has been found in the cache. In case of the JRE, this is the path to the java executable.
/// </summary>
public sealed record CacheHit(string FilePath) : CacheResult
{
    /// <summary>
    /// Path to the cached file, which is either the JRE executable or a file downloaded from the server.
    /// </summary>
    public string FilePath { get; } = FilePath;
}

/// <summary>
/// File not found in the cache.
/// </summary>
public sealed record CacheMiss : CacheResult;

/// <summary>
/// The cache location is invalid, the file found in the cache is invalid.
/// </summary>
public sealed record CacheError(string Message) : CacheResult
{
    public string Message { get; } = Message;
}
