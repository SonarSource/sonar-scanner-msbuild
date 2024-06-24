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

using static System.Net.WebRequestMethods;

namespace SonarScanner.MSBuild.PreProcessor;

public abstract record SonarServer(string ServerUrl)
{
    public string ServerUrl { get; } = ServerUrl;

    /// <summary>
    /// Base Url for the V2 endpoint
    /// </summary>
    public abstract string DefaultApiBaseUrl { get; }
}

public sealed record SonarQubeServer(string ServerUrl) : SonarServer(ServerUrl)
{
    /// <inheritdoc/>
    public override string DefaultApiBaseUrl => $"{ServerUrl?.TrimEnd('/')}/api/v2";
}

public sealed record SonarCloudServer(string ServerUrl) : SonarServer(ServerUrl)
{
    /// <inheritdoc/>
    public override string DefaultApiBaseUrl => "https://api.sonarcloud.io";
}
