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
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.PreProcessor.WebServer;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class PreprocessorObjectFactoryTests
{
    private TestLogger logger;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TestInitialize() =>
        logger = new TestLogger();

    [TestMethod]
    public void CreateSonarWebServer_ThrowsOnInvalidInput()
    {
        ((Func<PreprocessorObjectFactory>)(() => new PreprocessorObjectFactory(null))).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("logger");

        var sut = new PreprocessorObjectFactory(logger);
        sut.Invoking(x => x.CreateSonarWebServer(null).Result).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("args");
    }

    [TestMethod]
    public async Task CreateSonarWebService_RequestServerVersionThrows_ShouldReturnNullAndLogError()
    {
        var sut = new PreprocessorObjectFactory(logger);
        var downloader = Substitute.For<IDownloader>();
        downloader.Download(Arg.Any<string>(), Arg.Any<bool>()).Throws<InvalidOperationException>();
        downloader.DownloadResource(Arg.Any<string>()).Returns(new HttpResponseMessage());

        var result = await sut.CreateSonarWebServer(CreateValidArguments(), downloader);

        result.Should().BeNull();
        logger.AssertNoWarningsLogged();
        logger.AssertSingleErrorExists("An error occured while querying the server version! Please check if the server is running and if the address is correct.");
    }

    [TestMethod]
    public async Task CreateSonarWebService_InvalidHostUrl_ReturnNullAndLogErrors()
    {
        var sut = new PreprocessorObjectFactory(logger);

        var result = await sut.CreateSonarWebServer(CreateValidArguments("http:/myhost:222"), Substitute.For<IDownloader>());

        result.Should().BeNull();
        logger.AssertSingleErrorExists("The value provided for the host URL parameter (http:/myhost:222) is not valid. Please make sure that you have entered a valid URL and try again.");
        logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    public async Task CreateSonarWebService_MissingUriScheme_ReturnNullAndLogErrors()
    {
        var sut = new PreprocessorObjectFactory(logger);

        var result = await sut.CreateSonarWebServer(CreateValidArguments("myhost:222"), Substitute.For<IDownloader>());

        result.Should().BeNull();
        logger.AssertSingleErrorExists("The URL (myhost:222) provided does not contain the scheme. Please include 'http://' or 'https://' at the beginning.");
        logger.AssertNoWarningsLogged();
    }

    [TestMethod]
    [DataRow("https://sonarcloud.io", "8.0", typeof(SonarCloudWebServer))]
    [DataRow("https://sonarcloud.io/", "8.0", typeof(SonarCloudWebServer))]
    [DataRow("https://sonarcloud.io//", "8.0", typeof(SonarCloudWebServer))]
    [DataRow("https://sonarcloud_other.io//", "8.1", typeof(SonarQubeWebServer))]
    [DataRow("http://localhost:222", "8.9", typeof(SonarQubeWebServer))]
    public async Task CreateSonarWebServer_CorrectServiceType(string hostUrl, string version, Type serviceType)
    {
        var sut = new PreprocessorObjectFactory(logger);
        var downloader = Substitute.For<IDownloader>();
        downloader.Download(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(version));
        downloader.DownloadResource(Arg.Any<string>()).Returns(new HttpResponseMessage());

        var service = await sut.CreateSonarWebServer(CreateValidArguments(hostUrl), downloader, downloader);

        service.Should().BeOfType(serviceType);
    }

    [TestMethod]
    [DataRow("https://sonarcloud.io", "8.9", true)]
    [DataRow("https://sonarqube.gr", "8.0", false)]
    [DataRow("http://localhost:4242", "8.0", false)]
    public async Task CreateSonarWebServer_IncosistentServer(string hostUrl, string version, bool isCloud)
    {
        var sut = new PreprocessorObjectFactory(logger);
        var downloader = Substitute.For<IDownloader>();
        downloader.Download(Arg.Any<string>(), Arg.Any<bool>()).Returns(Task.FromResult(version));
        downloader.DownloadResource(Arg.Any<string>()).Returns(new HttpResponseMessage());
        var detected = isCloud ? "SonarCloud" : "SonarQube";
        var real = isCloud ? "SonarQube" : "SonarCloud";

        var service = await sut.CreateSonarWebServer(CreateValidArguments(hostUrl), downloader);

        service.Should().BeNull();
        logger.AssertErrorLogged($"Detected {detected} but server was found to be {real}. Please make sure the correct combination of 'sonar.host.url' and 'sonar.scanner.sonarcloudUrl' is set.");
    }

    [TestMethod]
    [DataRow("8.9", "10.3", 8)]
    [DataRow("10.3", "8.9", 10)]
    [DataRow(null, "8.3", 8)]
    [DataRow("10.3", null, 10)]
    public async Task CreateSonarWebServer_ValidCallSequence_ValidObjectReturned(string endpointResult, string fallbackResult, int expectedVersion)
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.Download("analysis/version", Arg.Any<bool>(), LoggerVerbosity.Debug).Returns(Task.FromResult(endpointResult));
        downloader.Download("api/server/version", Arg.Any<bool>(), LoggerVerbosity.Info).Returns(Task.FromResult(fallbackResult));
        downloader.DownloadResource(Arg.Any<string>()).Returns(new HttpResponseMessage());
        var validArgs = CreateValidArguments();
        var sut = new PreprocessorObjectFactory(logger);

        var server = await sut.CreateSonarWebServer(validArgs, downloader, downloader);

        server.Should().NotBeNull();
        server.ServerVersion.Major.Should().Be(expectedVersion);
    }

    [TestMethod]
    public void CreateTargetInstaller_Success()
    {
        var sut = new PreprocessorObjectFactory(logger);
        sut.CreateTargetInstaller().Should().NotBeNull();
    }

    [TestMethod]
    public void CreateJreResolver_Success()
    {
        var sut = new PreprocessorObjectFactory(logger);
        sut.CreateJreResolver(Substitute.For<ISonarWebServer>(), "sonarUserHome").Should().NotBeNull();
    }

    [TestMethod]
    public async Task CreateSonarWebService_WithoutOrganizationOnSonarCloud_ReturnsNullAndLogsAnErrorAndWarning()
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.Download("api/server/version", Arg.Any<bool>()).Returns(Task.FromResult("8.0")); // SonarCloud
        downloader.DownloadResource(Arg.Any<string>()).Returns(new HttpResponseMessage());
        var sut = new PreprocessorObjectFactory(logger);

        var server = await sut.CreateSonarWebServer(CreateValidArguments(hostUrl: "https://sonarcloud.io", organization: null), downloader);

        server.Should().BeNull();
        logger.AssertSingleErrorExists(@"Organization parameter (/o:""<organization>"") is required and needs to be provided!");
        logger.AssertSingleWarningExists("""
            In version 7 of the scanner, the default value for the sonar.host.url changed from "http://localhost:9000" to "https://sonarcloud.io".
            If the intention was to connect to the local SonarQube instance, please add the parameter: /d:sonar.host.url="http://localhost:9000"
            """);
    }

    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [TestMethod]
    public async Task CreateSonarWebService_WithFailedAuthentication_ReturnsNullAndLogsWarning(HttpStatusCode status)
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.DownloadResource(Arg.Any<string>()).Returns(new HttpResponseMessage(status));
        downloader.Download("api/server/version", Arg.Any<bool>()).Returns(Task.FromResult("8.0")); // SonarCloud
        var sut = new PreprocessorObjectFactory(logger);

        var server = await sut.CreateSonarWebServer(CreateValidArguments(hostUrl: "https://sonarcloud.io", organization: "org"), downloader);

        server.Should().BeNull();
        logger.Warnings.Should().BeEquivalentTo(
            "Authentication with the server has failed.",
            """
            In version 7 of the scanner, the default value for the sonar.host.url changed from "http://localhost:9000" to "https://sonarcloud.io".
            If the intention was to connect to the local SonarQube instance, please add the parameter: /d:sonar.host.url="http://localhost:9000"
            """);
    }

    [TestMethod]
    public void CreateRoslynAnalyzerProvider_Success()
    {
        var sut = new PreprocessorObjectFactory(logger);
        var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));
        sut.CreateRoslynAnalyzerProvider(Substitute.For<ISonarWebServer>(), "cache", logger, settings, new ListPropertiesProvider(), [], "cs").Should().NotBeNull();
    }

    [TestMethod]
    public void CreateRoslynAnalyzerProvider_NullServer_ThrowsArgumentNullException()
    {
        var sut = new PreprocessorObjectFactory(logger);
        var settings = BuildSettings.CreateNonTeamBuildSettingsForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));
        ((Func<RoslynAnalyzerProvider>)(() => sut.CreateRoslynAnalyzerProvider(null, "cache", logger, settings, new ListPropertiesProvider(), [], "cs"))).Should().ThrowExactly<ArgumentNullException>();
    }

    private ProcessedArgs CreateValidArguments(string hostUrl = "http://myhost:222", string organization = "organization")
    {
        var cmdLineArgs = new ListPropertiesProvider([new Property(SonarProperties.HostUrl, hostUrl)]);
        return new ProcessedArgs(
            "key",
            "name",
            "version",
            organization,
            false,
            cmdLineArgs,
            new ListPropertiesProvider(),
            EmptyPropertyProvider.Instance,
            Substitute.For<IFileWrapper>(),
            Substitute.For<IDirectoryWrapper>(),
            Substitute.For<IOperatingSystemProvider>(),
            logger);
    }
}
