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
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Unpacking;

namespace SonarScanner.MSBuild.PreProcessor.JreResolution.Test;

[TestClass]
public class JreResolverTests
{
    private const string SonarUserHome = "sonarUserHome";

    private readonly IFileWrapper fileWrapper = Substitute.For<IFileWrapper>();
    private readonly JreMetadata metadata = new("1", "filename.tar.gz", "javaPath", "path/to-jre", "sha256");
    private readonly IDirectoryWrapper directoryWrapper = Substitute.For<IDirectoryWrapper>();
    private readonly IFilePermissionsWrapper filePermissionsWrapper = Substitute.For<IFilePermissionsWrapper>();
    private readonly IChecksum checksum = Substitute.For<IChecksum>();

    private ListPropertiesProvider provider;
    private TestLogger logger;
    private ISonarWebServer server;
    private JreResolver sut;
    private IUnpackerFactory unpackerFactory;
    private CachedDownloader cachedDownloader;

    [TestInitialize]
    public void Initialize()
    {
        provider = [];
        provider.AddProperty("sonar.scanner.os", "linux");
        logger = new TestLogger();
        server = Substitute.For<ISonarWebServer>();
        server
            .DownloadJreMetadataAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(metadata));
        server.SupportsJreProvisioning.Returns(true);
        unpackerFactory = Substitute.For<IUnpackerFactory>();
        var cachedDownloader = Substitute.For<CachedDownloader>(
        cachedDownloader = Substitute.For<CachedDownloader>(
            logger,
            Substitute.For<IDirectoryWrapper>(),
            fileWrapper,
            Substitute.For<IChecksum>(),
            unpackerFactory,
            Substitute.For<IFilePermissionsWrapper>(),
            directoryWrapper,
            fileWrapper,
            checksum,
            SonarUserHome);
        downloader = Substitute.For<JreDownloader>(
            logger,
            cachedDownloader,
            Substitute.For<IDirectoryWrapper>(),
            Substitute.For<IFileWrapper>(),
            Substitute.For<UnpackerFactory>(),
            Substitute.For<IFilePermissionsWrapper>());
        sut = new JreResolver(server, downloader, logger);

        sut = new JreResolver(server, logger, filePermissionsWrapper, cachedDownloader, unpackerFactory, directoryWrapper, fileWrapper);
    }

    [TestMethod]
    public async Task ResolveJrePath_JavaExePathSet()
    {
        var args = Args();
        args.JavaExePath.Returns("path");

        var res = await sut.ResolveJrePath(args);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.javaExePath is set, skipping JRE provisioning.");
    }

    [TestMethod]
    public async Task ResolveJrePath_SkipProvisioningSet()
    {
        var args = Args();
        args.SkipJreProvisioning.Returns(true);

        var res = await sut.ResolveJrePath(args);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.skipJreProvisioning is set, skipping JRE provisioning.");
    }

    [TestMethod]
    public async Task ResolveJrePath_ArchitectureEmpty()
    {
        var args = Args();
        args.Architecture.Returns(string.Empty);

        var res = await sut.ResolveJrePath(args);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.arch is not set or detected, skipping JRE provisioning.");
    }

    [TestMethod]
    public async Task ResolveJrePath_OperatingSystemEmpty()
    {
        var args = Args();
        args.OperatingSystem.Returns(string.Empty);

        var res = await sut.ResolveJrePath(args);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.os is not set or detected, skipping JRE provisioning.");
    }

    [TestMethod]
    public async Task ResolveJrePath_MetadataNotFound()
    {
        server
            .DownloadJreMetadataAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<JreMetadata>(null));

        var res = await sut.ResolveJrePath(Args());

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: Metadata could not be retrieved.",
            "JreResolver: Resolving JRE path. Retrying...",
            "JreResolver: Metadata could not be retrieved.");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheFailure()
    {
        cachedDownloader.EnsureCacheRoot().ReturnsNull();

        var res = await sut.ResolveJrePath(Args());

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache failure. The file cache directory in 'sonarUserHome\\cache' could not be created.",
            "JreResolver: Resolving JRE path. Retrying...",
            "JreResolver: Cache failure. The file cache directory in 'sonarUserHome\\cache' could not be created.");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheHit()
    {
        directoryWrapper.Exists(null).ReturnsForAnyArgs(true);
        fileWrapper.Exists(null).ReturnsForAnyArgs(true);

        var res = await sut.ResolveJrePath(Args());

        res.Should().Be("""sonarUserHome\cache\sha256\filename.tar.gz_extracted\javaPath""");
        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            """JreResolver: Cache hit 'sonarUserHome\cache\sha256\filename.tar.gz_extracted\javaPath'.""");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheMiss_DownloadSuccess()
    {
        cachedDownloader.DownloadFileAsync(null, null).ReturnsForAnyArgs(new ResolutionSuccess("path"));
        fileWrapper.Exists("""sonarUserHome\cache\sha256\javaPath""").Returns(true);

        var res = await sut.ResolveJrePath(Args());

        res.Should().Be("""sonarUserHome\cache\sha256\filename.tar.gz_extracted\javaPath""");
        logger.InfoMessages.Should().BeEquivalentTo("""
            The JRE provisioning is a time consuming operation.
            JRE provisioned: filename.tar.gz.
            If you already have a compatible java version installed, please add either the parameter "/d:sonar.scanner.skipJreProvisioning=true" or "/d:sonar.scanner.javaExePath=<PATH>".
            """);
        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache miss. Attempting to download JRE.",
            """Starting extracting the Java runtime environment from archive 'path' to folder 'sonarUserHome\cache\sha256'.""",
            """Moving extracted Java runtime environment from 'sonarUserHome\cache\sha256' to 'sonarUserHome\cache\sha256\filename.tar.gz_extracted'.""",
            """The Java runtime environment was successfully added to 'sonarUserHome\cache\sha256\filename.tar.gz_extracted'.""",
            """JreResolver: Download success. JRE can be found at 'sonarUserHome\cache\sha256\filename.tar.gz_extracted\javaPath'.""");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheMiss_DownloadFailure()
    {
        cachedDownloader.DownloadFileAsync(null, null).ReturnsForAnyArgs(new ResolutionError("Reason."));

        var res = await sut.ResolveJrePath(Args());

        logger.InfoMessages.Should().BeEquivalentTo("""
            The JRE provisioning is a time consuming operation.
            JRE provisioned: filename.tar.gz.
            If you already have a compatible java version installed, please add either the parameter "/d:sonar.scanner.skipJreProvisioning=true" or "/d:sonar.scanner.javaExePath=<PATH>".
            """,
            """
            The JRE provisioning is a time consuming operation.
            JRE provisioned: filename.tar.gz.
            If you already have a compatible java version installed, please add either the parameter "/d:sonar.scanner.skipJreProvisioning=true" or "/d:sonar.scanner.javaExePath=<PATH>".
            """);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache miss. Attempting to download JRE.",
            "JreResolver: Download failure. Reason.",
            "JreResolver: Resolving JRE path. Retrying...",
            "JreResolver: Cache miss. Attempting to download JRE.",
            "JreResolver: Download failure. Reason.");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheMiss_DownloadUnknown()
    {
        var func = async () => await sut.ResolveJrePath(Args());

        await func.Should().ThrowExactlyAsync<NotSupportedException>().WithMessage("Download result is expected to be Hit or Failure.");

        logger.InfoMessages.Should().BeEquivalentTo("""
            The JRE provisioning is a time consuming operation.
            JRE provisioned: filename.tar.gz.
            If you already have a compatible java version installed, please add either the parameter "/d:sonar.scanner.skipJreProvisioning=true" or "/d:sonar.scanner.javaExePath=<PATH>".
            """);

        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache miss. Attempting to download JRE.");
    }

    [TestMethod]
    public async Task ResolveJrePath_CreateUnpackerFails_ReturnsFailure()
    {
        unpackerFactory.Create(null, null, null, null, null).ReturnsNullForAnyArgs();

        var res = await sut.ResolveJrePath(Args());

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache failure. The archive format of the JRE archive `filename.tar.gz` is not supported.",
            "JreResolver: Resolving JRE path. Retrying...",
            "JreResolver: Cache failure. The archive format of the JRE archive `filename.tar.gz` is not supported.");
    }

    [TestMethod]
    public async Task ResolveJrePath_SkipProvisioningOnUnsupportedServers()
    {
        server.SupportsJreProvisioning.Returns(false);
        await sut.ResolveJrePath(Args());

        logger.DebugMessages.Should().BeEquivalentTo(
            "JreResolver: Resolving JRE path.",
            "JreResolver: Skipping Java runtime environment provisioning because this version of SonarQube does not support it.");
    }

    [TestMethod]
    public async Task Args_IsValid_Priority()
    {
        var args = Args();
        // The order of evaluation: The first invalid property is the one that will be logged.
        var argProps = new (Action Valid, Action Invalid, string Message)[]
        {
            (Valid: () => args.JavaExePath.Returns(string.Empty), Invalid: () => args.JavaExePath.Returns("path"), Message: Resources.MSG_JreResolver_JavaExePathSet),
            (Valid: () => args.SkipJreProvisioning.Returns(false), Invalid: () => args.SkipJreProvisioning.Returns(true), Message: Resources.MSG_JreResolver_SkipJreProvisioningSet),
            (Valid: () => server.SupportsJreProvisioning.Returns(true), Invalid: () => server.SupportsJreProvisioning.Returns(false), Message: Resources.MSG_JreResolver_NotSupportedByServer),
            (Valid: () => args.OperatingSystem.Returns("os"), Invalid: () => args.OperatingSystem.Returns(string.Empty), Message: Resources.MSG_JreResolver_OperatingSystemMissing),
            (Valid: () => args.Architecture.Returns("arch"), Invalid: () => args.Architecture.Returns(string.Empty), Message: Resources.MSG_JreResolver_ArchitectureMissing),
        };
        var perms = Permutations(argProps).ToArray();
        foreach (var perm in perms)
        {
            var (firstInvalid, _) = perm.FirstOrDefault(x => !x.Valid);
            if (firstInvalid == default)
            {
                continue; // All valid is not a case we want to test
            }

            foreach (var (argProp, valid) in perm)
            {
                if (valid)
                {
                    argProp.Valid();
                }
                else
                {
                    argProp.Invalid();
                }
            }

            await sut.ResolveJrePath(args);
            logger.DebugMessages.Should().BeEquivalentTo(
                ["JreResolver: Resolving JRE path.",
                firstInvalid.Message],
                because: $"The combination {perm.Select((x, i) => new { Index = i, x.Valid }).Aggregate(new StringBuilder(), (sb, x) => sb.Append($"\n{x}"))} is set.");
            logger.DebugMessages.Clear();
        }

        static IEnumerable<IEnumerable<(T Item, bool Valid)>> Permutations<T>(IEnumerable<T> list)
        {
            var (head, rest) = (list.First(), list.Skip(1));
            if (rest.Any())
            {
                foreach (var restPerm in Permutations(rest))
                {
                    yield return [(head, true), .. restPerm];
                    yield return [(head, false), .. restPerm];
                }
            }
            else
            {
                yield return [(head, true)];
                yield return [(head, false)];
            }
        }
    }

    private ProcessedArgs Args()
    {
        var args = Substitute.For<ProcessedArgs>(
            "valid.key",
            "valid.name",
            "1.0",
            "organization",
            false,
            provider,
            EmptyPropertyProvider.Instance,
            EmptyPropertyProvider.Instance,
            fileWrapper,
            Substitute.For<IDirectoryWrapper>(),
            Substitute.For<OperatingSystemProvider>(Substitute.For<IFileWrapper>(), Substitute.For<ILogger>()),
            Substitute.For<ILogger>());
        args.OperatingSystem.Returns("os");
        args.Architecture.Returns("arch");
        return args;
    }

    private record class UnknownCache : CacheResult;

    private record class UnknownResult : DownloadResult;
}
