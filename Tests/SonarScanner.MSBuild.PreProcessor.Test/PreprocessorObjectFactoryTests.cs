/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

#pragma warning disable S3994 // we are specifically testing string urls

using SonarScanner.MSBuild.PreProcessor.SonarQubeClient;

namespace SonarScanner.MSBuild.PreProcessor.Test;

[TestClass]
public class PreprocessorObjectFactoryTests
{
    private readonly TestRuntime runtime = new();

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void CreateClient_ThrowsOnInvalidInput()
    {
        ((Func<PreprocessorObjectFactory>)(() => new PreprocessorObjectFactory(null))).Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("runtime");

        var sut = new PreprocessorObjectFactory(runtime);
        sut.Invoking(x => x.CreateClient(null).Result).Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("args");
    }

    [TestMethod]
    public async Task CreateClient_InvalidHostUrl_ReturnNullAndLogErrors()
    {
        var sut = new PreprocessorObjectFactory(runtime);

        var result = await sut.CreateClient(CreateValidArguments("http:/myhost:222"), Substitute.For<IDownloader>());

        result.Should().BeNull();
        runtime.Logger.Should().HaveErrorOnce("The value provided for the host URL parameter (http:/myhost:222) is not valid. Please make sure that you have entered a valid URL and try again.")
            .And.HaveNoWarnings();
    }

    [TestMethod]
    public async Task CreateClient_MissingUriScheme_ReturnNullAndLogErrors()
    {
        var sut = new PreprocessorObjectFactory(runtime);

        var result = await sut.CreateClient(CreateValidArguments("myhost:222"), Substitute.For<IDownloader>());

        result.Should().BeNull();
        runtime.Logger.Should().HaveErrorOnce("The URL (myhost:222) provided does not contain the scheme. Please include 'http://' or 'https://' at the beginning.")
            .And.HaveNoWarnings();
    }

    [TestMethod]
    [DataRow("https://sonarcloud.io", "8.0", typeof(SonarQubeCloud))]
    [DataRow("https://sonarcloud.io/", "8.0", typeof(SonarQubeCloud))]
    [DataRow("https://sonarcloud.io//", "8.0", typeof(SonarQubeCloud))]
    [DataRow("https://sonarcloud_other.io//", "26.1", typeof(SonarQubeServer))]
    [DataRow("http://localhost:222", "26.1", typeof(SonarQubeServer))]
    public async Task CreateClient_CorrectServiceType(string hostUrl, string version, Type serviceType)
    {
        var sut = new PreprocessorObjectFactory(runtime);
        var downloader = Substitute.For<IDownloader>();
        downloader.Download(Arg.Any<Uri>(), Arg.Any<bool>()).Returns(Task.FromResult(version));
        downloader.DownloadResource(Arg.Any<Uri>()).Returns(new HttpResponseMessage());
        downloader.DownloadResource(new("api/editions/is_valid_license", UriKind.Relative))
            .Returns(Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(@"{ ""isValidLicense"": true }") }));

        var service = await sut.CreateClient(CreateValidArguments(hostUrl), downloader, downloader);
        service.Should().BeOfType(serviceType);
    }

    [TestMethod]
    public void CreateJreResolver_Success()
    {
        var sut = new PreprocessorObjectFactory(runtime);
        sut.CreateJreResolver(MockSonarQube.Create(), "sonarUserHome").Should().NotBeNull();
    }

    [TestMethod]
    public void CreateEngineResolver_Success() =>
        new PreprocessorObjectFactory(runtime).CreateEngineResolver(MockSonarQube.Create(), "sonarUserHome").Should().NotBeNull();

    [TestMethod]
    public void CreateScannerCliResolver_Success() =>
        new PreprocessorObjectFactory(runtime).CreateScannerCliResolver(MockSonarQube.Create(), "sonarUserHome").Should().NotBeNull();

    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.Unauthorized)]
    [TestMethod]
    public async Task CreateClient_WithFailedAuthentication_ReturnsNullAndLogsWarning(HttpStatusCode status)
    {
        var downloader = Substitute.For<IDownloader>();
        downloader.DownloadResource(Arg.Any<Uri>()).Returns(new HttpResponseMessage(status));
        var sut = new PreprocessorObjectFactory(runtime);

        var client = await sut.CreateClient(CreateValidArguments(hostUrl: "https://sonarcloud.io", organization: "org"), downloader);
        client.Should().BeNull();
        runtime.Logger.Warnings.Should().BeEquivalentTo(
            "Authentication with the server has failed.",
            """
            In version 7 of the scanner, the default value for the sonar.host.url changed from "http://localhost:9000" to "https://sonarcloud.io".
            If the intention was to connect to the local SonarQube instance, please add the parameter: /d:sonar.host.url="http://localhost:9000"
            """
                .ToUnixLineEndings());
    }

    [TestMethod]
    public void CreateRoslynAnalyzerProvider_Success()
    {
        var sut = new PreprocessorObjectFactory(runtime);
        var settings = BuildSettings.CreateForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));
        sut.CreateRoslynAnalyzerProvider(MockSonarQube.Create(), "cache", settings, new ListPropertiesProvider(), [], "cs").Should().NotBeNull();
    }

    [TestMethod]
    public void CreateRoslynAnalyzerProvider_NullClient_ThrowsArgumentNullException()
    {
        var sut = new PreprocessorObjectFactory(runtime);
        var settings = BuildSettings.CreateForTesting(TestUtils.CreateTestSpecificFolderWithSubPaths(TestContext));
        FluentActions.Invoking(() => sut.CreateRoslynAnalyzerProvider(null, "cache", settings, new ListPropertiesProvider(), [], "cs")).Should()
            .ThrowExactly<ArgumentNullException>()
            .WithParameterName("client");
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
            runtime);
    }
}
