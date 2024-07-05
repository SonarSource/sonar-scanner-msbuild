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
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test.JreCaching;

[TestClass]
public class JreResolverTests
{
    private readonly string sonarUserHome = "sonarUserHome";
    private readonly TestLogger logger = new TestLogger();
    // For ProcessedArgs
    private readonly IFileWrapper fileWrapper = Substitute.For<IFileWrapper>();
    private readonly ListPropertiesProvider provider = new ListPropertiesProvider();
    // For JreResolver
    private readonly ISonarWebServer server = Substitute.For<ISonarWebServer>();
    private readonly IJreCache cache = Substitute.For<IJreCache>();
    private readonly JreMetadata metadata = new JreMetadata(null, null, null, null, null);

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
            Substitute.For<IOperatingSystemProvider>(),
            logger);

    [TestInitialize]
    public void Initialize()
    {
        sut = new JreResolver(server, cache, logger);
        server
            .DownloadJreMetadataAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(metadata));
    }

    [TestMethod]
    public async Task ResolveJrePath_JavaExePathSet()
    {
        provider.AddProperty("sonar.scanner.javaExePath", "path");
        fileWrapper.Exists("path").Returns(true);

        var res = await sut.ResolveJrePath(Args, sonarUserHome);

        res.Should().BeNull();
        logger.AssertDebugLogged("JreResolver: Resolving JRE path.");
        logger.AssertDebugLogged("JreResolver: JavaExePath is set, skipping JRE provisioning.");
    }

    [TestMethod]
    public async Task ResolveJrePath_SkipProvisioningSet()
    {
        provider.AddProperty("sonar.scanner.skipJreProvisioning", "True");

        var res = await sut.ResolveJrePath(Args, sonarUserHome);

        res.Should().BeNull();
        logger.AssertDebugLogged("JreResolver: Resolving JRE path.");
        logger.AssertDebugLogged("JreResolver: SkipJreProvisioning is set, skipping JRE provisioning.");
    }

    [TestMethod]
    public async Task ResolveJrePath_ArchitectureEmpty()
    {
        provider.AddProperty("sonar.scanner.arch", string.Empty);

        var res = await sut.ResolveJrePath(Args, sonarUserHome);

        res.Should().BeNull();
        logger.AssertDebugLogged("JreResolver: Resolving JRE path.");
        logger.AssertDebugLogged("JreResolver: Architecture is not set or detected, skipping JRE provisioning.");
    }

    [TestMethod]
    public async Task ResolveJrePath_OperatingSystemEmpty()
    {
        provider.AddProperty("sonar.scanner.os", string.Empty);

        var res = await sut.ResolveJrePath(Args, sonarUserHome);

        res.Should().BeNull();
        logger.AssertDebugLogged("JreResolver: Resolving JRE path.");
        logger.AssertDebugLogged("JreResolver: Operating System is not set or detected, skipping JRE provisioning.");
    }

    [TestMethod]
    public async Task ResolveJrePath_MetadataNotFound()
    {
        server
            .DownloadJreMetadataAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<JreMetadata>(null));

        var res = await sut.ResolveJrePath(Args, sonarUserHome);

        res.Should().BeNull();
        logger.AssertDebugLogged("JreResolver: Resolving JRE path.");
        logger.AssertDebugLogged("JreResolver: Metadata could not be retrieved.");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheFailure()
    {
        cache
            .IsJreCached(sonarUserHome, Arg.Any<JreDescriptor>())
            .Returns(new JreCacheFailure("failure"));

        var res = await sut.ResolveJrePath(Args, sonarUserHome);

        res.Should().BeNull();
        logger.AssertDebugLogged("JreResolver: Resolving JRE path.");
        logger.AssertDebugLogged("JreResolver: Exiting early due to cache failure.");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheHit()
    {
        cache
            .IsJreCached(sonarUserHome, Arg.Any<JreDescriptor>())
            .Returns(new JreCacheHit("path"));

        var res = await sut.ResolveJrePath(Args, sonarUserHome);

        res.Should().Be("path");
        logger.AssertDebugLogged("JreResolver: Resolving JRE path.");
        logger.AssertDebugLogged("JreResolver: Cache hit 'path'.");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheMiss_DownloadSuccess()
    {
        cache
            .IsJreCached(sonarUserHome, Arg.Any<JreDescriptor>())
            .Returns(new JreCacheMiss());

        cache
            .DownloadJreAsync(sonarUserHome, Arg.Any<JreDescriptor>(), Arg.Any<Func<Task<Stream>>>())
            .Returns(new JreCacheHit("path"));

        var res = await sut.ResolveJrePath(Args, sonarUserHome);

        res.Should().Be("path");
        logger.AssertDebugLogged("JreResolver: Resolving JRE path.");
        logger.AssertDebugLogged("JreResolver: Cache miss.");
        logger.AssertDebugLogged("JreResolver: Attempting to download JRE.");
        logger.AssertDebugLogged("JreResolver: Download successful. JRE can be found at 'path'.");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheMiss_DownloadFailure()
    {
        cache
            .IsJreCached(sonarUserHome, Arg.Any<JreDescriptor>())
            .Returns(new JreCacheMiss());

        cache
            .DownloadJreAsync(sonarUserHome, Arg.Any<JreDescriptor>(), Arg.Any<Func<Task<Stream>>>())
            .Returns(new JreCacheMiss());

        var res = await sut.ResolveJrePath(Args, sonarUserHome);

        res.Should().BeNull();
        logger.AssertDebugLogged("JreResolver: Resolving JRE path.");
        logger.AssertDebugLogged("JreResolver: Cache miss.");
        logger.AssertDebugLogged("JreResolver: Attempting to download JRE.");
        logger.AssertDebugLogged("JreResolver: Attempting to download JRE. Retrying...");
    }
}
