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

using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using SonarScanner.MSBuild.PreProcessor.Interfaces;

namespace SonarScanner.MSBuild.PreProcessor.EngineResolution.Test;

[TestClass]
public class EngineResolverTests
{
    private const string SonarUserHome = "sonarUserHome";
    private const string EngineJar = "engine.jar";
    private const string ChecksumValue = "sha256";
    private static readonly string CacheDir = Path.Combine(SonarUserHome, "cache");
    private static readonly string ShaPath = Path.Combine(CacheDir, ChecksumValue);
    private static readonly string CachedEnginePath = Path.Combine(ShaPath, EngineJar);

    private readonly EngineResolver resolver;
    private readonly TestRuntime runtime = new();
    private readonly ISonarWebServer server = Substitute.For<ISonarWebServer>();
    private readonly ProcessedArgs args = Substitute.For<ProcessedArgs>();
    private readonly IChecksum checksum = Substitute.For<IChecksum>();

    private readonly EngineMetadata metadata = new(
        EngineJar,
        ChecksumValue,
        new("https://scanner.sonarcloud.io/engines/sonarcloud-scanner-engine-11.14.1.763.jar"));

    public EngineResolverTests()
    {
        server.SupportsJreProvisioning.Returns(true);
        args.EngineJarPath.ReturnsNull();
        server.DownloadEngineMetadataAsync().Returns(Task.FromResult(metadata));
        resolver = new EngineResolver(server, "sonarUserHome", runtime, checksum);
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsSet_LocalEnginePath()
    {
        args.EngineJarPath.Returns("local/path/to/engine.jar");

        var result = await new EngineResolver(server, "sonarUserHome", runtime).ResolvePath(args);

        result.Should().Be("local/path/to/engine.jar");
        await server.DidNotReceive().DownloadEngineMetadataAsync();
        await server.DidNotReceiveWithAnyArgs().DownloadEngineAsync(null);
        AssertDebugMessages(
            "EngineResolver: Resolving Scanner Engine path.",
            "Using local sonar engine provided by sonar.scanner.engineJarPath=local/path/to/engine.jar");

        runtime.Logger.TelemetryMessages.Should().BeEquivalentTo(
            [
            new
            {
                Key = TelemetryKeys.NewBootstrappingEnabled,
                Value = TelemetryValues.NewBootstrappingStatus.Disabled
            },
            new
            {
                Key = TelemetryKeys.ScannerEngineDownload,
                Value = TelemetryValues.ScannerEngineDownloadStatus.UserSupplied
            }
            ]);
    }

    [TestMethod]
    public async Task ResolveEngine_JreProvisioningNotSupported_LogsAndReturnsNull()
    {
        server.SupportsJreProvisioning.Returns(false);
        args.EngineJarPath.ReturnsNull();

        var result = await resolver.ResolvePath(args);

        result.Should().BeNull();
        await server.DidNotReceive().DownloadEngineMetadataAsync();
        await server.DidNotReceiveWithAnyArgs().DownloadEngineAsync(null);
        AssertDebugMessages(
            "EngineResolver: Resolving Scanner Engine path.",
            "EngineResolver: Skipping Sonar Engine provisioning because this version of SonarQube does not support it.");

        runtime.Logger.TelemetryMessages.Should().BeEquivalentTo(
            [
            new
            {
                Key = TelemetryKeys.NewBootstrappingEnabled,
                Value = TelemetryValues.NewBootstrappingStatus.Unsupported
            }
            ]);
    }

    [TestMethod]
    public async Task ResolveEngine_DownloadsEngineMetadataNull_LogsMessage()
    {
        server.DownloadEngineMetadataAsync().Returns(Task.FromResult<EngineMetadata>(null));

        var result = await resolver.ResolvePath(args);

        result.Should().BeNull();
        await server.Received(1).DownloadEngineMetadataAsync();
        await server.DidNotReceiveWithAnyArgs().DownloadEngineAsync(null);
        AssertDebugMessages(
            "EngineResolver: Resolving Scanner Engine path.",
            "EngineResolver: Metadata could not be retrieved.");

        runtime.Logger.TelemetryMessages.Should().BeEquivalentTo(
            [
            new
            {
                Key = TelemetryKeys.NewBootstrappingEnabled,
                Value = TelemetryValues.NewBootstrappingStatus.Enabled
            }
            ]);
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsNull_DownloadsEngineMetadata_CacheHit()
    {
        runtime.File.Exists(CachedEnginePath).Returns(true);

        var result = await resolver.ResolvePath(args);

        result.Should().Be(CachedEnginePath);
        await server.Received(1).DownloadEngineMetadataAsync();
        await server.DidNotReceiveWithAnyArgs().DownloadEngineAsync(null);
        AssertDebugMessages(
            "EngineResolver: Resolving Scanner Engine path.",
            $"EngineResolver: Cache hit '{CachedEnginePath}'.");

        runtime.Logger.TelemetryMessages.Should().BeEquivalentTo(
            [
            new
            {
                Key = TelemetryKeys.NewBootstrappingEnabled,
                Value = TelemetryValues.NewBootstrappingStatus.Enabled
            },
            new
            {
                Key = TelemetryKeys.ScannerEngineDownload,
                Value = TelemetryValues.ScannerEngineDownloadStatus.CacheHit
            }
            ]);
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsNull_DownloadsEngineMetadata_CacheError()
    {
        runtime.Directory.When(x => x.CreateDirectory(Arg.Any<string>())).Throw(new IOException());

        var result = await resolver.ResolvePath(args);

        result.Should().BeNull();
        await server.Received(1).DownloadEngineMetadataAsync();
        await server.DidNotReceiveWithAnyArgs().DownloadEngineAsync(null);
        AssertDebugMessages(
            true,
            "EngineResolver: Resolving Scanner Engine path.",
            $"EngineResolver: Cache failure. The file cache directory in '{CacheDir}' could not be created.");

        runtime.Logger.TelemetryMessages.Should().BeEquivalentTo(
            [
            new
            {
                Key = TelemetryKeys.NewBootstrappingEnabled,
                Value = TelemetryValues.NewBootstrappingStatus.Enabled
            },
            new
            {
                Key = TelemetryKeys.ScannerEngineDownload,
                Value = TelemetryValues.ScannerEngineDownloadStatus.Failed
            },
            new
            {
                Key = TelemetryKeys.ScannerEngineDownload,
                Value = TelemetryValues.ScannerEngineDownloadStatus.Failed
            },
            ]);
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsNull_DownloadsEngineMetadata_CacheMiss_DownloadSuccess()
    {
        var tempFile = Path.Combine(ShaPath, "tempFile.jar");
        using var content = new MemoryStream([1, 2, 3]);
        using var computeHashStream = new MemoryStream();

        // mocks successful download from the server
        server.DownloadEngineAsync(metadata).Returns(content);
        runtime.Directory.GetRandomFileName().Returns("tempFile.jar");
        checksum.ComputeHash(computeHashStream).Returns(ChecksumValue);
        runtime.File.Create(tempFile).Returns(new MemoryStream());
        runtime.File.Open(tempFile).Returns(computeHashStream);

        var result = await resolver.ResolvePath(args);

        result.Should().Be(CachedEnginePath);
        await server.Received(1).DownloadEngineMetadataAsync();
        await server.Received(1).DownloadEngineAsync(metadata);
        AssertDebugMessages(
            "EngineResolver: Resolving Scanner Engine path.",
            "EngineResolver: Cache miss. Attempting to download Scanner Engine.",
            "Starting the file download.",
            $"The checksum of the downloaded file is '{ChecksumValue}' and the expected checksum is '{ChecksumValue}'.",
            $"EngineResolver: Download success. Scanner Engine can be found at '{CachedEnginePath}'.");

        runtime.Logger.TelemetryMessages.Should().BeEquivalentTo(
            [
            new
            {
                Key = TelemetryKeys.NewBootstrappingEnabled,
                Value = TelemetryValues.NewBootstrappingStatus.Enabled
            },
            new
            {
                Key = TelemetryKeys.ScannerEngineDownload,
                Value = TelemetryValues.ScannerEngineDownloadStatus.Downloaded
            }
            ]);
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsNull_DownloadsEngineMetadata_CacheMiss_DownloadError()
    {
        server.DownloadEngineAsync(metadata).ThrowsAsync(new Exception("Reason"));

        var result = await resolver.ResolvePath(args);

        result.Should().BeNull();
        await server.Received(1).DownloadEngineMetadataAsync();
        await server.Received(2).DownloadEngineAsync(metadata);
        AssertDebugMessages(
            true,
            "EngineResolver: Resolving Scanner Engine path.",
            "EngineResolver: Cache miss. Attempting to download Scanner Engine.",
            "Starting the file download.",
            $"Deleting file '{ShaPath}'.",
            "The download of the file from the server failed with the exception 'Reason'.",
            "EngineResolver: Download failure. The download of the file from the server failed with the exception 'Reason'.");

        runtime.Logger.TelemetryMessages.Should().BeEquivalentTo(
            [
            new
            {
                Key = TelemetryKeys.NewBootstrappingEnabled,
                Value = TelemetryValues.NewBootstrappingStatus.Enabled
            },
            new
            {
                Key = TelemetryKeys.ScannerEngineDownload,
                Value = TelemetryValues.ScannerEngineDownloadStatus.Failed
            },
            new
            {
                Key = TelemetryKeys.ScannerEngineDownload,
                Value = TelemetryValues.ScannerEngineDownloadStatus.Failed
            },
            ]);
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
        runtime.Logger.DebugMessages.Should().Equal(expected);
    }
}
