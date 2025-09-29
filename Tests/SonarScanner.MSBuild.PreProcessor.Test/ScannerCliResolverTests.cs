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
using SonarScanner.MSBuild.PreProcessor.Test.Infrastructure;
using SonarScanner.MSBuild.PreProcessor.Unpacking;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class ScannerCliResolverTests
{
    private const string SonarUserHome = "sonarUserHome";
    private const string ScannerCliFilename = "sonar-scanner-cli-5.0.2.4997.zip";
    private const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string TempFolderName = "folder.temp";

    private static readonly string ScannerCliTargetPath = Path.Combine("sonar-scanner-5.0.2.4997", "bin", "sonar-scanner");
    private static readonly string ShaPath = Path.Combine(SonarUserHome, "cache", Sha256);
    private static readonly string DownloadPath = Path.Combine(ShaPath, ScannerCliFilename);
    private static readonly string ExtractedPath = Path.Combine(ShaPath, ScannerCliFilename + "_extracted");
    private static readonly string ExtractedScannerPath = Path.Combine(ExtractedPath, ScannerCliTargetPath);
    private static readonly string TempExtractionPath = Path.Combine(ShaPath, TempFolderName);

    private readonly TestRuntime runtime = new();
    private readonly UnpackerFactory unpackerFactory;
    private readonly HttpMessageHandlerMock handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
    private readonly ProcessedArgs args = Substitute.For<ProcessedArgs>();
    private readonly ScannerCliResolver sut;

    public ScannerCliResolverTests()
    {
        var checksum = Substitute.For<IChecksum>();
        checksum.ComputeHash(null).ReturnsForAnyArgs(Sha256);
        runtime.Directory.Exists(ShaPath).Returns(true);
        runtime.Directory.GetRandomFileName().Returns(TempFolderName);
        runtime.File.Create(null).ReturnsForAnyArgs(x => new MemoryStream());
        runtime.File.Open(null).ReturnsForAnyArgs(x => new MemoryStream());
        runtime.File.Exists(Path.Combine(TempExtractionPath, ScannerCliTargetPath)).Returns(true);
        unpackerFactory = Substitute.For<UnpackerFactory>(runtime);
        sut = new ScannerCliResolver(checksum, SonarUserHome, runtime, unpackerFactory, handler);
    }

    [TestMethod]
    public async Task ResolvePath_CacheHit_ReturnsPath()
    {
        runtime.File.Exists(ExtractedScannerPath).Returns(true);

        var result = await sut.ResolvePath(args);
        result.Should().Be(ExtractedScannerPath);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            "ScannerCliResolver: Resolving SonarScanner CLI path.",
            $"ScannerCliResolver: Cache hit '{ExtractedScannerPath}'.");
    }

    [TestMethod]
    public async Task ResolvePath_CacheMiss_DownloadSuccess()
    {
        var result = await sut.ResolvePath(args);
        result.Should().Be(ExtractedScannerPath);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            "ScannerCliResolver: Resolving SonarScanner CLI path.",
            $"Cache miss. Attempting to download '{DownloadPath}'.",
            $"The checksum of the downloaded file is '{Sha256}' and the expected checksum is '{Sha256}'.",
            $"Starting to extract files from archive '{DownloadPath}' to folder '{TempExtractionPath}'.",
            $"Moving extracted files from '{TempExtractionPath}' to '{ExtractedPath}'.",
            $"The archive was successfully extracted to '{ExtractedPath}'.",
            $"ScannerCliResolver: Download success. SonarScanner CLI can be found at '{ExtractedScannerPath}'.");
    }

    [TestMethod]
    public async Task ResolvePath_ArchiveExists_DoesNotDownloadFromServer()
    {
        runtime.File.Exists(DownloadPath).Returns(true);

        var result = await sut.ResolvePath(args);
        result.Should().Be(ExtractedScannerPath);
        handler.Requests.Should().BeEmpty();
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            "ScannerCliResolver: Resolving SonarScanner CLI path.",
            $"The file was already downloaded from the server and stored at '{DownloadPath}'.",
            $"The checksum of the downloaded file is '{Sha256}' and the expected checksum is '{Sha256}'.",
            $"Starting to extract files from archive '{DownloadPath}' to folder '{TempExtractionPath}'.",
            $"Moving extracted files from '{TempExtractionPath}' to '{ExtractedPath}'.",
            $"The archive was successfully extracted to '{ExtractedPath}'.",
            $"ScannerCliResolver: Download success. SonarScanner CLI can be found at '{ExtractedScannerPath}'.");
    }

    [TestMethod]
    public async Task ResolvePath_DownloadFailure_ReturnsNull()
    {
        runtime.File.Create(null).ThrowsForAnyArgs(new IOException("Reason"));

        var result = await sut.ResolvePath(args);
        result.Should().BeNull();
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            "ScannerCliResolver: Resolving SonarScanner CLI path.",
            $"Cache miss. Attempting to download '{DownloadPath}'.",
            $"Deleting file '{TempExtractionPath}'.",
            "The download of the file from the server failed with the exception 'Reason'.",
            "ScannerCliResolver: Download failure. The download of the file from the server failed with the exception 'Reason'.",
            "ScannerCliResolver: Resolving SonarScanner CLI path. Retrying...",
            $"Cache miss. Attempting to download '{DownloadPath}'.",
            $"Deleting file '{TempExtractionPath}'.",
            "The download of the file from the server failed with the exception 'Reason'.",
            "ScannerCliResolver: Download failure. The download of the file from the server failed with the exception 'Reason'.");
    }

    [TestMethod]
    public async Task ResolvePath_SuccessAfterRetry()
    {
        runtime.File.Create(null).ReturnsForAnyArgs(x => throw new IOException("First attempt failed"), x => new MemoryStream());

        var result = await sut.ResolvePath(args);
        result.Should().Be(ExtractedScannerPath);
        runtime.Logger.DebugMessages.Should().BeEquivalentTo(
            "ScannerCliResolver: Resolving SonarScanner CLI path.",
            $"Cache miss. Attempting to download '{DownloadPath}'.",
            $"Deleting file '{TempExtractionPath}'.",
            "The download of the file from the server failed with the exception 'First attempt failed'.",
            "ScannerCliResolver: Download failure. The download of the file from the server failed with the exception 'First attempt failed'.",
            "ScannerCliResolver: Resolving SonarScanner CLI path. Retrying...",
            $"Cache miss. Attempting to download '{DownloadPath}'.",
            $"The checksum of the downloaded file is '{Sha256}' and the expected checksum is '{Sha256}'.",
            $"Starting to extract files from archive '{DownloadPath}' to folder '{TempExtractionPath}'.",
            $"Moving extracted files from '{TempExtractionPath}' to '{ExtractedPath}'.",
            $"The archive was successfully extracted to '{ExtractedPath}'.",
            $"ScannerCliResolver: Download success. SonarScanner CLI can be found at '{ExtractedScannerPath}'.");
    }
}
