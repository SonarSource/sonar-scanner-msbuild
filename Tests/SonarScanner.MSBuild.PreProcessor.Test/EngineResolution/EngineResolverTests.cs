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

using NSubstitute.ReturnsExtensions;

namespace SonarScanner.MSBuild.PreProcessor.EngineResolution.Test;

[TestClass]
public class EngineResolverTests
{
    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsSet_LocalEnginePath()
    {
        var server = Substitute.For<ISonarWebServer>();
        var logger = Substitute.For<ILogger>();
        var resolver = new EngineResolver(server, logger);
        var args = Substitute.For<ProcessedArgs>();
        args.EngineJarPath.Returns("local/path/to/engine.jar");

        var result = await resolver.ResolveEngine(args, "sonarHome");

        result.Should().Be("local/path/to/engine.jar");
        logger.Received(1).LogDebug(Arg.Any<string>(), "local/path/to/engine.jar");
        await server.DidNotReceive().DownloadEngineMetadataAsync();
    }

    [TestMethod]
    public async Task ResolveEngine_EngineJarPathIsNull_DownloadsEngineMetadata()
    {
        var server = Substitute.For<ISonarWebServer>();
        server.DownloadEngineMetadataAsync().Returns(Task.FromResult(new EngineMetadata(
            "engine.jar",
            "907f676d488af266431bafd3bc26f58408db2d9e73efc66c882c203f275c739b",
            new Uri("https://scanner.sonarcloud.io/engines/sonarcloud-scanner-engine-11.14.1.763.jar"))));
        var logger = Substitute.For<ILogger>();
        var resolver = new EngineResolver(server, logger);
        var args = Substitute.For<ProcessedArgs>();
        args.EngineJarPath.ReturnsNull();

        var result = await resolver.ResolveEngine(args, "sonarHome");

        result.Should().BeNull();
        await server.Received(1).DownloadEngineMetadataAsync();
        logger.DidNotReceiveWithAnyArgs().LogDebug(null, null);
    }
}
