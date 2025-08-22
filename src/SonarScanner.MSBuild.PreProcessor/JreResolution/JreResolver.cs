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
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Unpacking;

namespace SonarScanner.MSBuild.PreProcessor.JreResolution;

// https://xtranet-sonarsource.atlassian.net/wiki/spaces/LANG/pages/3155001372/Scanner+Bootstrapping
public class JreResolver : IJreResolver
{
    private readonly ISonarWebServer server;
    private readonly ILogger logger;
    private readonly UnpackerFactory unpackerFactory;
    private readonly IDirectoryWrapper directoryWrapper;
    private readonly IFileWrapper fileWrapper;
    private readonly IChecksum checksum;
    private readonly string sonarUserHome;

    public JreResolver(ISonarWebServer server,
                       ILogger logger,
                       IChecksum checksum,
                       string sonarUserHome,
                       UnpackerFactory unpackerFactory = null,
                       IDirectoryWrapper directoryWrapper = null,
                       IFileWrapper fileWrapper = null)
    {
        this.server = server;
        this.logger = logger;
        this.checksum = checksum;
        this.sonarUserHome = sonarUserHome;
        this.unpackerFactory = unpackerFactory ?? new UnpackerFactory(logger, new OperatingSystemProvider(FileWrapper.Instance, logger));
        this.directoryWrapper = directoryWrapper ?? DirectoryWrapper.Instance;
        this.fileWrapper = fileWrapper ?? FileWrapper.Instance;
    }

    public async Task<string> ResolveJrePath(ProcessedArgs args)
    {
        logger.LogDebug(Resources.MSG_Resolver_Resolving, nameof(JreResolver), "JRE", string.Empty);
        if (!IsValid(args))
        {
            return null;
        }

        if (await DownloadJre(args) is { } jrePath)
        {
            return jrePath;
        }
        else
        {
            logger.LogDebug(Resources.MSG_Resolver_Resolving, nameof(JreResolver), "JRE", " Retrying...");
            return await DownloadJre(args);
        }
    }

    private async Task<string> DownloadJre(ProcessedArgs args)
    {
        var metadata = await server.DownloadJreMetadataAsync(args.OperatingSystem, args.Architecture);
        if (metadata is null)
        {
            logger.LogDebug(Resources.MSG_Resolver_MetadataFailure, nameof(JreResolver));
            return null;
        }

        var descriptor = metadata.ToDescriptor();
        if (unpackerFactory.Create(descriptor.Filename) is { } unpacker)
        {
            var jreDownloader = new JreDownloader(logger, directoryWrapper, fileWrapper, unpacker, checksum, sonarUserHome, descriptor);
            switch (jreDownloader.IsJreCached())
            {
                case CacheHit hit:
                    logger.LogDebug(Resources.MSG_Resolver_CacheHit, nameof(JreResolver), hit.FilePath);
                    return hit.FilePath;
                case CacheMiss:
                    logger.LogDebug(Resources.MSG_Resolver_CacheMiss, nameof(JreResolver), "JRE");
                    return await DownloadJre(jreDownloader, metadata);
                case CacheError failure:
                    logger.LogDebug(Resources.MSG_Resolver_CacheFailure, nameof(JreResolver), failure.Message);
                    return null;
                default:
                    throw new NotSupportedException("File Resolution is expected to be CacheHit, CacheMiss, or CacheError.");
            }
        }
        else
        {
            logger.LogDebug(Resources.MSG_Resolver_CacheFailure, nameof(JreResolver), string.Format(Resources.ERR_JreArchiveFormatNotSupported, descriptor.Filename));
            return null;
        }
    }

    private async Task<string> DownloadJre(JreDownloader jreDownloader, JreMetadata metadata)
    {
        var result = await jreDownloader.DownloadJreAsync(() => server.DownloadJreAsync(metadata));
        if (result is DownloadSuccess success)
        {
            logger.LogDebug(Resources.MSG_Resolver_DownloadSuccess, nameof(JreResolver), "JRE", success.FilePath);
            return success.FilePath;
        }
        else if (result is DownloadError error)
        {
            logger.LogDebug(Resources.MSG_Resolver_DownloadFailure, nameof(JreResolver), error.Message);
            return null;
        }
        throw new NotSupportedException("Download result is expected to be DownloadSuccess or DownloadError.");
    }

    private bool IsValid(ProcessedArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.JavaExePath))
        {
            logger.LogDebug(Resources.MSG_JreResolver_JavaExePathSet);
            return false;
        }
        if (args.SkipJreProvisioning)
        {
            logger.LogDebug(Resources.MSG_JreResolver_SkipJreProvisioningSet);
            return false;
        }
        if (!server.SupportsJreProvisioning)
        {
            logger.LogDebug(Resources.MSG_JreResolver_NotSupportedByServer);
            return false;
        }
        if (string.IsNullOrWhiteSpace(args.OperatingSystem))
        {
            logger.LogDebug(Resources.MSG_JreResolver_OperatingSystemMissing);
            return false;
        }
        if (string.IsNullOrWhiteSpace(args.Architecture))
        {
            logger.LogDebug(Resources.MSG_JreResolver_ArchitectureMissing);
            return false;
        }
        return true;
    }
}
