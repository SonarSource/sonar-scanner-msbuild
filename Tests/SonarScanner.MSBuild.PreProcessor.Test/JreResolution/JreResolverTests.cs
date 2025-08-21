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

namespace SonarScanner.MSBuild.PreProcessor.JreResolution.Test;

[TestClass]
public class JreResolverTests
{
    private const string SonarUserHome = "sonarUserHome";

    private readonly IFileWrapper fileWrapper = Substitute.For<IFileWrapper>();
    private readonly JreMetadata metadata = new(null, null, null, null, null);

    private ListPropertiesProvider provider;
    private JreDownloader downloader;
    private TestLogger logger;
    private ISonarWebServer server;
    private JreResolver sut;

    [TestInitialize]
    public void Initialize()
    {
        provider = new();
        provider.AddProperty("sonar.scanner.os", "linux");
        logger = new TestLogger();
        server = Substitute.For<ISonarWebServer>();
        server
            .DownloadJreMetadataAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(metadata));
        server.SupportsJreProvisioning.Returns(true);
        var cachedDownloader = Substitute.For<CachedDownloader>(
            logger,
            Substitute.For<IDirectoryWrapper>(),
            fileWrapper,
            Substitute.For<IChecksum>(),
            SonarUserHome);
        downloader = Substitute.For<JreDownloader>(
            logger,
            cachedDownloader,
            Substitute.For<IDirectoryWrapper>(),
            Substitute.For<IFileWrapper>(),
            Substitute.For<IUnpackerFactory>(),
            Substitute.For<IFilePermissionsWrapper>());
        sut = new JreResolver(server, downloader, logger);
    }

    [TestMethod]
    public async Task ResolveJrePath_JavaExePathSet()
    {
        var args = GetArgs();
        args.JavaExePath.Returns("path");

        var res = await sut.ResolveJrePath(args);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.javaExePath is set, skipping JRE provisioning."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_SkipProvisioningSet()
    {
        var args = GetArgs();
        args.SkipJreProvisioning.Returns(true);

        var res = await sut.ResolveJrePath(args);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.skipJreProvisioning is set, skipping JRE provisioning."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_ArchitectureEmpty()
    {
        var args = GetArgs();
        args.Architecture.Returns(string.Empty);

        var res = await sut.ResolveJrePath(args);

        res.Should().BeNull();
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.arch is not set or detected, skipping JRE provisioning."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_OperatingSystemEmpty()
    {
        var args = GetArgs();
        args.OperatingSystem.Returns(string.Empty);

        var res = await sut.ResolveJrePath(args);

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

        var res = await sut.ResolveJrePath(GetArgs());

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
        downloader
            .IsJreCached(Arg.Any<JreDescriptor>())
            .Returns(new UnknownCache());

        var func = async () => await sut.ResolveJrePath(GetArgs());

        await func.Should().ThrowExactlyAsync<NotSupportedException>().WithMessage("Cache result is expected to be Hit, Miss, or Failure.");
        logger.DebugMessages.Should().ContainSingle("JreResolver: Resolving JRE path.");
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheFailure()
    {
        downloader
            .IsJreCached(Arg.Any<JreDescriptor>())
            .Returns(new CacheError("Reason."));

        var res = await sut.ResolveJrePath(GetArgs());

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
        downloader
            .IsJreCached(Arg.Any<JreDescriptor>())
            .Returns(new CacheHit("path"));

        var res = await sut.ResolveJrePath(GetArgs());

        res.Should().Be("path");
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache hit 'path'."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_IsJreCached_CacheMiss_DownloadSuccess()
    {
        downloader
            .IsJreCached(Arg.Any<JreDescriptor>())
            .Returns(new CacheMiss());
        downloader
            .DownloadJreAsync(Arg.Any<JreDescriptor>(), Arg.Any<Func<Task<Stream>>>())
            .Returns(new DownloadSuccess("path"));

        var res = await sut.ResolveJrePath(GetArgs());

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
        downloader
            .IsJreCached(Arg.Any<JreDescriptor>())
            .Returns(new CacheMiss());
        downloader
            .DownloadJreAsync(Arg.Any<JreDescriptor>(), Arg.Any<Func<Task<Stream>>>())
            .Returns(new DownloadError("Reason."));

        var res = await sut.ResolveJrePath(GetArgs());

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
        downloader
            .IsJreCached(Arg.Any<JreDescriptor>())
            .Returns(new CacheMiss());

        downloader
            .DownloadJreAsync(Arg.Any<JreDescriptor>(), Arg.Any<Func<Task<Stream>>>())
            .Returns(new UnknownResult());

        var func = async () => await sut.ResolveJrePath(GetArgs());

        await func.Should().ThrowExactlyAsync<NotSupportedException>().WithMessage("Download result is expected to be Hit or Failure.");
        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: Cache miss. Attempting to download JRE."
            ]);
    }

    [TestMethod]
    public async Task ResolveJrePath_SkipProvisioningOnUnsupportedServers()
    {
        server.SupportsJreProvisioning.Returns(false);
        await sut.ResolveJrePath(GetArgs());

        logger.DebugMessages.Should().BeEquivalentTo([
            "JreResolver: Resolving JRE path.",
            "JreResolver: Skipping Java runtime environment provisioning because this version of SonarQube does not support it."]);
    }

    [TestMethod]
    public async Task Args_IsValid_Priority()
    {
        var args = GetArgs();
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
            logger.DebugMessages.Should().BeEquivalentTo([
                "JreResolver: Resolving JRE path.",
                firstInvalid.Message], because: $"The combination {perm.Select((x, i) => new { Index = i, x.Valid }).Aggregate(new StringBuilder(), (sb, x) => sb.Append($"\n{x}"))} is set.");
            logger.DebugMessages.Clear();
        }

        static IEnumerable<IEnumerable<(T Item, bool Valid)>> Permutations<T>(IEnumerable<T> list)
        {
            var (head, rest) = (list.First(), list.Skip(1));
            if (!rest.Any())
            {
                yield return [(head, true)];
                yield return [(head, false)];
            }
            else
            {
                foreach (var restPerm in Permutations(rest))
                {
                    yield return [(head, true), .. restPerm];
                    yield return [(head, false), .. restPerm];
                }
            }
        }
    }

    private ProcessedArgs GetArgs()
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
            Substitute.For<IOperatingSystemProvider>(),
            Substitute.For<ILogger>());
        args.OperatingSystem.Returns("os");
        args.Architecture.Returns("arch");
        return args;
    }

    private record class UnknownCache : CacheResult;

    private record class UnknownResult : DownloadResult;
}
