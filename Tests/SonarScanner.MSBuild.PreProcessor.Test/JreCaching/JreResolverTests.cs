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

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.PreProcessor.JreCaching;
using SonarScanner.MSBuild.PreProcessor.JreResolution;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreCaching;

[TestClass]
public class JreResolverTests
{
    private const string SonarUserHome = "sonarUserHome";

    private readonly IFileWrapper fileWrapper = Substitute.For<IFileWrapper>();
    private readonly JreMetadata metadata = new(null, null, null, null, null);

    private ListPropertiesProvider provider;
    private IJreCache cache;
    private TestLogger logger;
    private ISonarWebServer server;
    private IJreResolver sut;

    private ProcessedArgs Args =>
        new("valid.key",
            "valid.name",
            "1.0",
            "organization",
            false,
            provider,
            EmptyPropertyProvider.Instance,
            EmptyPropertyProvider.Instance,
            fileWrapper,
            Substitute.For<IDirectoryWrapper>(),
            Substitute.For<IOperatingSystemProvider>(),
            Substitute.For<ILogger>());

    [TestInitialize]
    public void Initialize()
    {
        provider = new();
        provider.AddProperty("sonar.scanner.os", "linux");
        cache = Substitute.For<IJreCache>();
        logger = new TestLogger();
        server = Substitute.For<ISonarWebServer>();
        server
            .DownloadJreMetadataAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(metadata));

        sut = new JreResolver(server, cache, logger);
    }

    [TestMethod]
    public async Task ResolveJrePath_JavaExePathSet()
    {
        provider.AddProperty("sonar.scanner.javaExePath", "path");
        fileWrapper.Exists("path").Returns(true);

        var res = await sut.ResolveJrePath(Args, SonarUserHome);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.javaExePath is set, skipping JRE provisioning."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_SkipProvisioningSet()
    {
        provider.AddProperty("sonar.scanner.skipJreProvisioning", "True");

        var res = await sut.ResolveJrePath(Args, SonarUserHome);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.skipJreProvisioning is set, skipping JRE provisioning."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_ArchitectureEmpty()
    {
        provider.AddProperty("sonar.scanner.arch", string.Empty);

        var res = await sut.ResolveJrePath(Args, SonarUserHome);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.arch is not set or detected, skipping JRE provisioning."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_OperatingSystemEmpty()
    {
        provider = new();
        provider.AddProperty("sonar.scanner.os", string.Empty);

        var res = await sut.ResolveJrePath(Args, SonarUserHome);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.os is not set or detected, skipping JRE provisioning."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_MetadataNotFound()
    {
        server
            .DownloadJreMetadataAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<JreMetadata>(null));

        var res = await sut.ResolveJrePath(Args, SonarUserHome);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: Metadata could not be retrieved.",
            "JreResolver: Resolving JRE path. Retrying...",
            "JreResolver: Metadata could not be retrieved."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_UnknownResult()
    {
        cache
            .IsJreCached(SonarUserHome, Arg.Any<JreDescriptor>())
            .Returns(new UnknownResult());

        var func = async () => await sut.ResolveJrePath(Args, SonarUserHome);

        await func.Should().ThrowExactlyAsync<NotSupportedException>().WithMessage("Cache result is expected to be Hit, Miss, or Failure.");
        logger.DebugMessages.Should().ContainSingle("JreResolver: Resolving JRE path.");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheFailure()
    {
        cache
            .IsJreCached(SonarUserHome, Arg.Any<JreDescriptor>())
            .Returns(new JreCacheFailure("Reason."));

        var res = await sut.ResolveJrePath(Args, SonarUserHome);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache failure. Reason.",
            "JreResolver: Resolving JRE path. Retrying...",
            "JreResolver: Cache failure. Reason."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheHit()
    {
        cache
            .IsJreCached(SonarUserHome, Arg.Any<JreDescriptor>())
            .Returns(new JreCacheHit("path"));

        var res = await sut.ResolveJrePath(Args, SonarUserHome);

        res.Should().Be("path");
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache hit 'path'."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheMiss_DownloadSuccess()
    {
        cache
            .IsJreCached(SonarUserHome, Arg.Any<JreDescriptor>())
            .Returns(new JreCacheMiss());
        cache
            .DownloadJreAsync(SonarUserHome, Arg.Any<JreDescriptor>(), Arg.Any<Func<Task<Stream>>>())
            .Returns(new JreCacheHit("path"));

        var res = await sut.ResolveJrePath(Args, SonarUserHome);

        res.Should().Be("path");
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache miss. Attempting to download JRE.",
            "JreResolver: Download success. JRE can be found at 'path'."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheMiss_DownloadFailure()
    {
        cache
            .IsJreCached(SonarUserHome, Arg.Any<JreDescriptor>())
            .Returns(new JreCacheMiss());
        cache
            .DownloadJreAsync(SonarUserHome, Arg.Any<JreDescriptor>(), Arg.Any<Func<Task<Stream>>>())
            .Returns(new JreCacheFailure("Reason."));

        var res = await sut.ResolveJrePath(Args, SonarUserHome);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache miss. Attempting to download JRE.",
            "JreResolver: Download failure. Reason.",
            "JreResolver: Resolving JRE path. Retrying...",
            "JreResolver: Cache miss. Attempting to download JRE.",
            "JreResolver: Download failure. Reason.",
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheMiss_DownloadUnknown()
    {
        cache
            .IsJreCached(SonarUserHome, Arg.Any<JreDescriptor>())
            .Returns(new JreCacheMiss());

        cache
            .DownloadJreAsync(SonarUserHome, Arg.Any<JreDescriptor>(), Arg.Any<Func<Task<Stream>>>())
            .Returns(new UnknownResult());

        var func = async () => await sut.ResolveJrePath(Args, SonarUserHome);

        await func.Should().ThrowExactlyAsync<NotSupportedException>().WithMessage("Download result is expected to be Hit or Failure.");
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache miss. Attempting to download JRE."
            ]);
    }

    private record class UnknownResult : JreCacheResult;
}
