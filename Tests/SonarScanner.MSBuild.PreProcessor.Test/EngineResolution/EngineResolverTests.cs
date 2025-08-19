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

using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using SonarScanner.MSBuild.PreProcessor.Caching;

namespace SonarScanner.MSBuild.PreProcessor.EngineResolution.Test;

[TestClass]
public class EngineResolverTests
{
    private readonly ISonarWebServer server;
    private readonly ILogger logger;
    private readonly IFileCache fileCache;
    private readonly ProcessedArgs args;
    private readonly EngineResolver resolver;

    public EngineResolverTests()
    {
        server = Substitute.For<ISonarWebServer>();
        server.SupportsJreProvisioning.Returns(true);
        logger = Substitute.For<ILogger>();
        fileCache = Substitute.For<IFileCache>();
        args = Substitute.For<ProcessedArgs>();
        args.EngineJarPath.ReturnsNull();

        resolver = new EngineResolver(server, fileCache, logger);
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsSet_LocalEnginePath()
    {
        args.EngineJarPath.Returns("local/path/to/engine.jar");

        var result = await resolver.ResolveEngine(args);

        result.Should().Be("local/path/to/engine.jar");
        logger.Received(1).LogDebug(Arg.Any<string>(), "local/path/to/engine.jar");
        await server.DidNotReceive().DownloadEngineMetadataAsync();
    }

    [TestMethod]
    public async Task ResolveEngine_JreProvisioningNotSupported_LogsAndReturnsNull()
    {
        server.SupportsJreProvisioning.Returns(false);
        args.EngineJarPath.ReturnsNull();

        var result = await resolver.ResolveEngine(args);

        result.Should().BeNull();
        logger.Received(1).LogDebug("EngineResolver: Skipping Sonar Engine provisioning because this version of SonarQube does not support it.");
        await server.DidNotReceive().DownloadEngineMetadataAsync();
    }

    [TestMethod]
    public async Task ResolveEngine_DownloadsEngineMetadataNull_LogsMessage()
    {
        server.DownloadEngineMetadataAsync().Returns(Task.FromResult<EngineMetadata>(null));

        var result = await resolver.ResolveEngine(args);

        result.Should().BeNull();
        logger.Received(1).LogDebug("EngineResolver: Metadata could not be retrieved.");
        await server.Received(1).DownloadEngineMetadataAsync();
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsNull_DownloadsEngineMetadata_CacheHit()
    {
        server.DownloadEngineMetadataAsync().Returns(Task.FromResult(new EngineMetadata(
            "engine.jar",
            "sha256",
            "https://scanner.sonarcloud.io/engines/sonarcloud-scanner-engine-11.14.1.763.jar")));
        fileCache.IsFileCached(Arg.Is<FileDescriptor>(x =>
            x.Filename == "engine.jar"
            && x.Sha256 == "sha256")).Returns(new CacheHit("sonarHome/.cache/engine.jar"));

        var result = await resolver.ResolveEngine(args);

        result.Should().Be("sonarHome/.cache/engine.jar");
        await server.Received(1).DownloadEngineMetadataAsync();
        fileCache.Received(1).IsFileCached(Arg.Is<FileDescriptor>(x =>
            x.Filename == "engine.jar"
            && x.Sha256 == "sha256"));
        logger.DidNotReceiveWithAnyArgs().LogDebug(null, null);
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsNull_DownloadsEngineMetadata_CacheMiss()
    {
        server.DownloadEngineMetadataAsync().Returns(Task.FromResult(new EngineMetadata(
            "engine.jar",
            "sha256",
            "https://scanner.sonarcloud.io/engines/sonarcloud-scanner-engine-11.14.1.763.jar")));
        fileCache.IsFileCached(Arg.Is<FileDescriptor>(x =>
            x.Filename == "engine.jar"
            && x.Sha256 == "sha256")).Returns(new CacheMiss());

        var act = async () => await resolver.ResolveEngine(args);

        await act.Should().ThrowAsync<NotImplementedException>();
        await server.Received(1).DownloadEngineMetadataAsync();
        fileCache.Received(1).IsFileCached(Arg.Is<FileDescriptor>(x =>
            x.Filename == "engine.jar"
            && x.Sha256 == "sha256"));
    }
}
