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

namespace SonarScanner.MSBuild.PreProcessor.JreResolution;

/// <summary>
/// A descriptor of the JRE found or not found in the cache.
/// </summary>
public abstract record JreCacheResult;

/// <summary>
/// Jre found in the cache.
/// </summary>
public sealed record JreCacheHit(string JavaExe) : JreCacheResult
{
    public string JavaExe { get; } = JavaExe;
}

/// <summary>
/// Jre not found in the cache. A download of the JRE is required.
/// </summary>
public sealed record JreCacheMiss : JreCacheResult;

/// <summary>
/// The cache location is invalid or the Jre found in the cache is invalid. A download of the JRE is not required.
/// </summary>
public sealed record JreCacheFailure(string Message) : JreCacheResult
{
    public string Message { get; } = Message;
}
