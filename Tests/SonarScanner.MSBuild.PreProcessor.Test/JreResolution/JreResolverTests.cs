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
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Unpacking;

namespace SonarScanner.MSBuild.PreProcessor.JreResolution.Test;

[TestClass]
public class JreResolverTests
{
    private const string SonarUserHome = "sonarUserHome";

    private static readonly string CacheDir = Path.Combine(SonarUserHome, "cache");
    private static readonly string ShaPath = Path.Combine(CacheDir, "sha256");
    private static readonly string DownloadPath = Path.Combine(ShaPath, "filename.tar.gz");
    private static readonly string ExtractedPath = Path.Combine(ShaPath, "filename.tar.gz_extracted");
    private static readonly string JavaExePath = Path.Combine("path", "to", "java.exe");
    private static readonly string ExtractedJavaPath = Path.Combine(ExtractedPath, JavaExePath);

    private readonly JreMetadata metadata = new("1", "filename.tar.gz", JavaExePath, new Uri("https://localhost.com/path/to-jre"), "sha256");
    private readonly IChecksum checksum = Substitute.For<IChecksum>();

    private ListPropertiesProvider provider;
    private ISonarWebServer server;
    private JreResolver sut;
    private TestRuntime runtime;

    [TestInitialize]
    public void Initialize()
    {
        provider = [];
        provider.AddProperty("sonar.scanner.os", "linux");
        server = Substitute.For<ISonarWebServer>();
        server.DownloadJreMetadataAsync(null, null).ReturnsForAnyArgs(metadata);
        server.SupportsJreProvisioning.Returns(true);
        runtime = new();

        sut = new JreResolver(server, checksum, SonarUserHome, runtime, Substitute.For<UnpackerFactory>(runtime));
    }

    [TestMethod]
    public async Task ResolveJrePath_JavaExePathSet()
    {
        var args = Args();
        args.JavaExePath.Returns("path");

        var res = await sut.ResolvePath(args);

        res.Should().BeNull();
        AssertDebugMessages(
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.javaExePath is set, skipping JRE provisioning.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.Disabled)
            .And.HaveMessage(TelemetryKeys.JreDownload, TelemetryValues.JreDownload.UserSupplied);
    }

    [TestMethod]
    public async Task ResolveJrePath_SkipProvisioningSet()
    {
        var args = Args();
        args.SkipJreProvisioning.Returns(true);

        var res = await sut.ResolvePath(args);

        res.Should().BeNull();
        AssertDebugMessages(
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.skipJreProvisioning is set, skipping JRE provisioning.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.Disabled)
            .And.NotHaveKey(TelemetryKeys.JreDownload);
    }

    [TestMethod]
    public async Task ResolveJrePath_ArchitectureEmpty()
    {
        var args = Args();
        args.Architecture.Returns(string.Empty);

        var res = await sut.ResolvePath(args);

        res.Should().BeNull();
        AssertDebugMessages(
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.arch is not set or detected, skipping JRE provisioning.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.UnsupportedNoArch)
            .And.NotHaveKey(TelemetryKeys.JreDownload);
    }

    [TestMethod]
    public async Task ResolveJrePath_OperatingSystemEmpty()
    {
        var args = Args();
        args.OperatingSystem.Returns(string.Empty);

        var res = await sut.ResolvePath(args);

        res.Should().BeNull();
        AssertDebugMessages(
            "JreResolver: Resolving JRE path.",
            "JreResolver: sonar.scanner.os is not set or detected, skipping JRE provisioning.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.UnsupportedNoOS)
            .And.NotHaveKey(TelemetryKeys.JreDownload);
    }

    [TestMethod]
    public async Task ResolveJrePath_MetadataNotFound()
    {
        server
            .DownloadJreMetadataAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<JreMetadata>(null));

        var res = await sut.ResolvePath(Args());

        res.Should().BeNull();
        AssertDebugMessages(
            true,
            "JreResolver: Resolving JRE path.",
            "JreResolver: Metadata could not be retrieved.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.Enabled)
            .And.HaveMessage(TelemetryKeys.JreDownload, TelemetryValues.JreDownload.Failed);
    }

    [TestMethod]
    public async Task ResolveJrePath_CacheHitForJre()
    {
        runtime.File.Exists(null).ReturnsForAnyArgs(true);

        var res = await sut.ResolvePath(Args());

        res.Should().Be(ExtractedJavaPath);
        AssertDebugMessages(
            "JreResolver: Resolving JRE path.",
            $"JreResolver: Cache hit '{ExtractedJavaPath}'.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.Enabled)
            .And.HaveMessage(TelemetryKeys.JreDownload, TelemetryValues.JreDownload.CacheHit);
    }

    [TestMethod]
    public async Task ResolveJrePath_CacheMiss_DownloadSuccess()
    {
        var tempArchive = Path.Combine(ShaPath, "tempFile.zip");
        var downloadContentArray = new byte[] { 1, 2, 3 };
        using var content = new MemoryStream(downloadContentArray);
        using var computeHashStream = new MemoryStream();

        // mocks successful download from the server, and unpacking of the jre.
        server.DownloadJreAsync(metadata).Returns(content);
        runtime.Directory.GetRandomFileName().Returns("tempFile.zip");
        runtime.File.Exists(Path.Combine(tempArchive, JavaExePath)).Returns(true); // the temp file created during the download, not the file within the cache
        runtime.File.Create(tempArchive).Returns(new MemoryStream());
        runtime.File.Open(tempArchive).Returns(computeHashStream);
        checksum.ComputeHash(computeHashStream).Returns("sha256");

        var res = await sut.ResolvePath(Args());

        res.Should().Be(ExtractedJavaPath);
        AssertJreBottleNeckMessage();
        AssertDebugMessages(
            "JreResolver: Resolving JRE path.",
            $"Cache miss. Could not find '{ExtractedJavaPath}'.",
            $"Cache miss. Could not find '{DownloadPath}'.",
            "The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            $"Starting to extract files from archive '{DownloadPath}' to folder '{tempArchive}'.",
            $"Moving extracted files from '{tempArchive}' to '{ExtractedPath}'.",
            $"The archive was successfully extracted to '{ExtractedPath}'.",
            $"JreResolver: Download success. JRE can be found at '{ExtractedJavaPath}'.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.Enabled)
            .And.HaveMessage(TelemetryKeys.JreDownload, TelemetryValues.JreDownload.Downloaded);
    }

    [TestMethod]
    public async Task ResolveJrePath_ArchiveExists_DoesNotDownloadFromServer()
    {
        using var computeHashStream = new MemoryStream();

        var tempArchive = Path.Combine(ShaPath, "tempFile.zip");
        runtime.File.Exists(DownloadPath).Returns(true);
        runtime.File.Open(DownloadPath).Returns(computeHashStream);
        checksum.ComputeHash(computeHashStream).Returns("sha256");
        runtime.Directory.GetRandomFileName().Returns("tempFile.zip");
        runtime.File.Exists(Path.Combine(tempArchive, JavaExePath)).Returns(true); // the temp file created during the download, not the file within the cache
        runtime.File.Create(tempArchive).Returns(new MemoryStream());
        runtime.File.Open(tempArchive).Returns(computeHashStream);

        var res = await sut.ResolvePath(Args());

        await server.DidNotReceive().DownloadJreAsync(Arg.Any<JreMetadata>());
        res.Should().Be(ExtractedJavaPath);
        AssertJreBottleNeckMessage();
        AssertDebugMessages(
            "JreResolver: Resolving JRE path.",
            $"Cache miss. Could not find '{ExtractedJavaPath}'.",
            $"The file was already downloaded from the server and stored at '{DownloadPath}'.",
            "The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            $"Starting to extract files from archive '{DownloadPath}' to folder '{tempArchive}'.",
            $"Moving extracted files from '{tempArchive}' to '{ExtractedPath}'.",
            $"The archive was successfully extracted to '{ExtractedPath}'.",
            $"JreResolver: Download success. JRE can be found at '{ExtractedJavaPath}'.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.Enabled)
            .And.HaveMessage(TelemetryKeys.JreDownload, TelemetryValues.JreDownload.Downloaded);
    }

    [TestMethod]
    public async Task ResolveJrePath_CacheMiss_DownloadFailure()
    {
        runtime.File.Create(Arg.Any<string>()).Throws(new IOException("Reason"));
        var res = await sut.ResolvePath(Args());

        AssertJreBottleNeckMessage(true);
        res.Should().BeNull();
        AssertDebugMessages(
            true,
            "JreResolver: Resolving JRE path.",
            $"Cache miss. Could not find '{ExtractedJavaPath}'.",
            $"Cache miss. Could not find '{DownloadPath}'.",
            $"Deleting file '{ShaPath}'.",  // should be temp file path but the scaffolding is not setup
            "The download of the file from the server failed with the exception 'Reason'.",
            "JreResolver: Download failure. The download of the file from the server failed with the exception 'Reason'.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.Enabled)
            .And.HaveMessage(TelemetryKeys.JreDownload, TelemetryValues.JreDownload.Failed);
    }

    [TestMethod]
    public async Task ResolveJrePath_DownloadSuccessAfterRetry()
    {
        var tempArchive = Path.Combine(ShaPath, "tempFile.zip");
        var downloadContentArray = new byte[] { 1, 2, 3 };
        using var content = new MemoryStream(downloadContentArray);
        using var computeHashStream = new MemoryStream();

        // mocks failed and then successful download from the server
        server.DownloadJreAsync(metadata).Returns(_ => throw new Exception("Reason"), _ => content);
        runtime.Directory.GetRandomFileName().Returns("tempFile.zip");
        runtime.File.Exists(Path.Combine(tempArchive, JavaExePath)).Returns(true); // the temp file created during the download, not the file within the cache
        runtime.File.Create(tempArchive).Returns(_ => new MemoryStream());
        runtime.File.Open(tempArchive).Returns(computeHashStream);
        checksum.ComputeHash(computeHashStream).Returns("sha256");

        var res = await sut.ResolvePath(Args());

        res.Should().Be(ExtractedJavaPath);
        AssertJreBottleNeckMessage(retry: true);
        await server.ReceivedWithAnyArgs(2).DownloadJreMetadataAsync(null, null);
        await server.Received(2).DownloadJreAsync(metadata);
        AssertDebugMessages(
            "JreResolver: Resolving JRE path.",
            $"Cache miss. Could not find '{ExtractedJavaPath}'.",
            $"Cache miss. Could not find '{DownloadPath}'.",
            $"Deleting file '{tempArchive}'.",
            "The download of the file from the server failed with the exception 'Reason'.",
            "JreResolver: Download failure. The download of the file from the server failed with the exception 'Reason'.",
            "JreResolver: Resolving JRE path. Retrying...",
            $"Cache miss. Could not find '{ExtractedJavaPath}'.",
            $"Cache miss. Could not find '{DownloadPath}'.",
            "The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            $"Starting to extract files from archive '{DownloadPath}' to folder '{tempArchive}'.",
            $"Moving extracted files from '{tempArchive}' to '{ExtractedPath}'.",
            $"The archive was successfully extracted to '{ExtractedPath}'.",
            $"JreResolver: Download success. JRE can be found at '{ExtractedJavaPath}'.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.Enabled)
            .And.HaveMessage(TelemetryKeys.JreDownload, TelemetryValues.JreDownload.Downloaded);    // Failed value is overridden by retry.
    }

    [TestMethod]
    public async Task ResolveJrePath_MetadataDownloadSuccessAfterRetry()
    {
        var tempArchive = Path.Combine(ShaPath, "tempFile.zip");
        var downloadContentArray = new byte[] { 1, 2, 3 };
        using var content = new MemoryStream(downloadContentArray);
        using var computeHashStream = new MemoryStream();

        // mocks failed and then successful metadata download from the server
        server.DownloadJreMetadataAsync(null, null).ReturnsForAnyArgs(null, metadata);
        server.DownloadJreAsync(metadata).Returns(content);
        runtime.Directory.GetRandomFileName().Returns("tempFile.zip");
        runtime.File.Exists(Path.Combine(tempArchive, JavaExePath)).Returns(true); // the temp file created during the download, not the file within the cache
        runtime.File.Create(tempArchive).Returns(x => new MemoryStream());
        runtime.File.Open(tempArchive).Returns(computeHashStream);
        checksum.ComputeHash(computeHashStream).Returns("sha256");

        var res = await sut.ResolvePath(Args());

        res.Should().Be(ExtractedJavaPath);
        AssertJreBottleNeckMessage();
        await server.ReceivedWithAnyArgs(2).DownloadJreMetadataAsync(null, null);
        await server.Received(1).DownloadJreAsync(metadata);
        AssertDebugMessages(
            "JreResolver: Resolving JRE path.",
            "JreResolver: Metadata could not be retrieved.",
            "JreResolver: Resolving JRE path. Retrying...",
            $"Cache miss. Could not find '{ExtractedJavaPath}'.",
            $"Cache miss. Could not find '{DownloadPath}'.",
            "The checksum of the downloaded file is 'sha256' and the expected checksum is 'sha256'.",
            $"Starting to extract files from archive '{DownloadPath}' to folder '{tempArchive}'.",
            $"Moving extracted files from '{tempArchive}' to '{ExtractedPath}'.",
            $"The archive was successfully extracted to '{ExtractedPath}'.",
            $"JreResolver: Download success. JRE can be found at '{ExtractedJavaPath}'.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.Enabled)
            .And.HaveMessage(TelemetryKeys.JreDownload, TelemetryValues.JreDownload.Downloaded);    // Failed value is overridden by retry.
    }

    [TestMethod]
    public async Task ResolveJrePath_SkipProvisioningOnUnsupportedServers()
    {
        server.SupportsJreProvisioning.Returns(false);
        await sut.ResolvePath(Args());

        AssertDebugMessages(
            "JreResolver: Resolving JRE path.",
            "JreResolver: Skipping Java runtime environment provisioning because this version of SonarQube does not support it.");
        runtime.Telemetry.Should().HaveMessage(TelemetryKeys.JreBootstrapping, TelemetryValues.JreBootstrapping.UnsupportedByServer)
            .And.NotHaveKey(TelemetryKeys.JreDownload);
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

            await sut.ResolvePath(args);
            runtime.Logger.DebugMessages.Should().BeEquivalentTo(
                ["JreResolver: Resolving JRE path.",
                firstInvalid.Message],
                because: $"The combination {perm.Select((x, i) => new { Index = i, x.Valid }).Aggregate(new StringBuilder(), (sb, x) => sb.Append($"\n{x}"))} is set.");
            runtime.Logger.DebugMessages.Clear();
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

    private static ProcessedArgs Args()
    {
        var args = Substitute.For<ProcessedArgs>();
        args.OperatingSystem.Returns("os");
        args.Architecture.Returns("arch");
        return args;
    }

    private void AssertJreBottleNeckMessage(bool retry = false)
    {
        var bottleNeckMessage = """
            The JRE provisioning is a time consuming operation.
            JRE provisioned: filename.tar.gz.
            If you already have a compatible Java version installed, please add either the parameter "/d:sonar.scanner.skipJreProvisioning=true" or "/d:sonar.scanner.javaExePath=<PATH>".
            """;
        runtime.Logger.InfoMessages.Should().BeEquivalentTo(Enumerable.Repeat(bottleNeckMessage, retry ? 2 : 1));
    }

    private void AssertDebugMessages(params string[] messages) =>
        AssertDebugMessages(false, messages);

    private void AssertDebugMessages(bool retry, params string[] messages)
    {
        var expected = new List<string>(messages);
        if (retry)
        {
            var retryMessages = messages.ToArray();
            retryMessages[0] += " Retrying...";
            expected.AddRange(retryMessages);
        }
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(expected);
    }
}
