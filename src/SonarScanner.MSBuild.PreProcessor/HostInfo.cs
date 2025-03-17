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

namespace SonarScanner.MSBuild.PreProcessor;

public abstract record HostInfo(string ServerUrl, string ApiBaseUrl)
{
    public abstract bool IsSonarCloud { get; }
    public string ServerUrl { get; } = ServerUrl;
    public string ApiBaseUrl { get; } = ApiBaseUrl;

    // see spec in https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001395/Scanner+Bootstrappers+implementation+guidelines
    public static HostInfo FromProperties(ILogger logger, string sonarHostUrl, string sonarcloudUrl, string apiBaseUrl, string region)
    {
        if (region is not null && (sonarHostUrl is not null || sonarcloudUrl is not null || apiBaseUrl is not null))
        {
            logger.LogWarning(Resources.WARN_RegionIsOverriden, SonarProperties.Region, region, SonarProperties.HostUrl, SonarProperties.SonarcloudUrl, SonarProperties.ApiBaseUrl);
        }
        var info = new { sonarHostUrl, sonarcloudUrl } switch
        {
            { sonarHostUrl: { }, sonarcloudUrl: { } } when sonarHostUrl != sonarcloudUrl => Error(Resources.ERR_HostUrlDiffersFromSonarcloudUrl),
            { sonarHostUrl: { }, sonarcloudUrl: { } } when string.IsNullOrWhiteSpace(sonarcloudUrl) => Error(Resources.ERR_HostUrlAndSonarcloudUrlAreEmpty),
            { sonarHostUrl: { }, sonarcloudUrl: { } } => Warn(
                FromProperties(logger, sonarHostUrl: null, sonarcloudUrl, apiBaseUrl, region),
                Resources.WARN_HostUrlAndSonarcloudUrlSet),
            { sonarHostUrl: { }, sonarcloudUrl: null } when sonarHostUrl.TrimEnd('/') != SonarPropertiesDefault.SonarcloudUrl =>
                new ServerHostInfo(sonarHostUrl, apiBaseUrl ?? $"{sonarHostUrl.TrimEnd('/')}/api/v2"),
            _ => CloudHostInfo.FromProperties(logger, sonarHostUrl, sonarcloudUrl, apiBaseUrl, region),
        };

        if (info is not null)
        {
            // Override by the user
            logger.LogDebug(Resources.MSG_ServerInfo_ServerUrlDetected, info.ServerUrl);
            logger.LogDebug(Resources.MSG_ServerInfo_ApiUrlDetected, info.ApiBaseUrl);
            logger.LogDebug(Resources.MSG_ServerInfo_IsSonarCloudDetected, info.IsSonarCloud);
        }

        return info;

        HostInfo Error(string message)
        {
            logger.LogError(message);
            return null;
        }

        HostInfo Warn(HostInfo server, string message)
        {
            logger.LogWarning(message);
            return server;
        }
    }
}

public record ServerHostInfo(string ServerUrl, string ApiBaseUrl) : HostInfo(ServerUrl, ApiBaseUrl)
{
    public override bool IsSonarCloud => false;
}

public record CloudHostInfo(string ServerUrl, string ApiBaseUrl, string Region) : HostInfo(ServerUrl, ApiBaseUrl)
{
    public override bool IsSonarCloud => true;
    public string Region { get; } = Region;

    public static new CloudHostInfo FromProperties(ILogger logger, string sonarHostUrl, string sonarcloudUrl, string apiBaseUrl, string region)
    {
        var defaultCloudUrl = SonarPropertiesDefault.SonarcloudUrl;
        var defaultApiUrl = SonarPropertiesDefault.SonarcloudApiBaseUrl;
        if (region is not null)
        {
            switch (region.Trim().ToLower())
            {
                case "":
                    break;
                case "us":
                    defaultCloudUrl = SonarPropertiesDefault.SonarcloudUrlUs;
                    defaultApiUrl = SonarPropertiesDefault.SonarcloudApiBaseUrlUs;
                    break;
                default:
                    logger.LogError(Resources.ERROR_UnsupportedRegion, region);
                    return null;
            }
        }
        var serverUrl = sonarcloudUrl ?? sonarHostUrl ?? defaultCloudUrl;
        var apiUrl = apiBaseUrl ?? defaultApiUrl;
        return new(serverUrl, apiUrl, region);
    }
}
