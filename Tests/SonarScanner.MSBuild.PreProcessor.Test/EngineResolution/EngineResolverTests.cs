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

using NSubstitute.ReturnsExtensions;
using SonarScanner.MSBuild.PreProcessor.Caching;

namespace SonarScanner.MSBuild.PreProcessor.EngineResolution.Test;

[TestClass]
public class EngineResolverTests
{
    private readonly ISonarWebServer server;
    private readonly ILogger logger;
    private readonly IFileCache fileCache;
    private readonly EngineResolver resolver;

    public EngineResolverTests()
    {
        server = Substitute.For<ISonarWebServer>();
        logger = Substitute.For<ILogger>();
        fileCache = Substitute.For<IFileCache>();
        resolver = new EngineResolver(server, fileCache, logger);
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsSet_LocalEnginePath()
    {
        var args = Substitute.For<ProcessedArgs>();
        args.EngineJarPath.Returns("local/path/to/engine.jar");

        var result = await resolver.ResolveEngine(args, "sonarHome");

        result.Should().Be("local/path/to/engine.jar");
        logger.Received(1).LogDebug(Arg.Any<string>(), "local/path/to/engine.jar");
        await server.DidNotReceive().DownloadEngineMetadataAsync();
    }

    [TestMethod]
    public async Task ResolveEngine_LogsAndReturnsNull_WhenJreProvisioningNotSupported()
    {
        server.SupportsJreProvisioning.Returns(false);
        var args = Substitute.For<ProcessedArgs>();
        args.EngineJarPath.ReturnsNull();

        var result = await resolver.ResolveEngine(args, "sonarHome");

        result.Should().BeNull();
        logger.Received(1).LogDebug("EngineResolver: Skipping Sonar Engine provisioning because this version of SonarQube does not support it.");
        await server.DidNotReceive().DownloadEngineMetadataAsync();
    }

    [TestMethod]
    public async Task ResolveEngine_DownloadsEngineMetadata_WhenMetadaDownloadFails()
    {
        server.SupportsJreProvisioning.Returns(true);
        server.DownloadEngineMetadataAsync().Returns(Task.FromResult<EngineMetadata>(null));
        var args = Substitute.For<ProcessedArgs>();
        args.EngineJarPath.ReturnsNull();

        var result = await resolver.ResolveEngine(args, "sonarHome");

        result.Should().BeNull();
        logger.Received(1).LogDebug("EngineResolver: Metadata could not be retrieved.");
        await server.Received(1).DownloadEngineMetadataAsync();
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsNull_DownloadsEngineMetadata()
    {
        server.SupportsJreProvisioning.Returns(true);
        server.DownloadEngineMetadataAsync().Returns(Task.FromResult(new EngineMetadata(
            "engine.jar",
            "sha256",
            new Uri("https://scanner.sonarcloud.io/engines/sonarcloud-scanner-engine-11.14.1.763.jar"))));
        fileCache.IsFileCached("sonarHome", Arg.Is<FileDescriptor>(x =>
            x.Filename == "engine.jar"
            && x.Sha256 == "sha256")).Returns(new CacheHit("sonarHome/.cache/engine.jar"));
        var args = Substitute.For<ProcessedArgs>();
        args.EngineJarPath.ReturnsNull();

        var result = await resolver.ResolveEngine(args, "sonarHome");

        result.Should().Be("sonarHome/.cache/engine.jar");
        await server.Received(1).DownloadEngineMetadataAsync();
        fileCache.Received(1).IsFileCached("sonarHome", Arg.Is<FileDescriptor>(x =>
            x.Filename == "engine.jar"
            && x.Sha256 == "sha256"));
        logger.DidNotReceiveWithAnyArgs().LogDebug(null, null);
    }
}
