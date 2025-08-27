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

using System.Runtime.CompilerServices;
using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.PreProcessor.Interfaces;
using SonarScanner.MSBuild.PreProcessor.Roslyn;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;

namespace SonarScanner.MSBuild.PreProcessor.Test;

internal class MockObjectFactory : IPreprocessorObjectFactory
{
    private readonly List<string> calledMethods = new();

    public TestLogger Logger { get; } = new();
    public MockSonarWebServer Server { get; }
    public ITargetsInstaller TargetsInstaller { get; } = Substitute.For<ITargetsInstaller>();
    public IResolver JreResolver { get; } = Substitute.For<IResolver>();
    public IResolver EngineResolver { get; } = Substitute.For<IResolver>();
    public string PluginCachePath { get; private set; }
    public MockRoslynAnalyzerProvider AnalyzerProvider { get; private set; }

    public MockObjectFactory(TestLogger logger) : this() =>
        Logger = logger;

    public MockObjectFactory(bool withDefaultRules = true, string organization = null, Dictionary<string, string> serverProperties = null)
    {
        Server = new(organization);

        var data = Server.Data;
        data.ServerProperties.Add("server.key", "server value 1");
        if (serverProperties is not null)
        {
            foreach (var pair in serverProperties)
            {
                data.ServerProperties.Add(pair.Key, pair.Value);
            }
        }
        data.Languages.Add("cs");
        data.Languages.Add("vbnet");
        data.Languages.Add("another_plugin");

        if (withDefaultRules)
        {
            data.AddQualityProfile("qp1", "cs", organization).AddProject("key").AddRule(new SonarRule("csharpsquid", "cs.rule.id"));
            data.AddQualityProfile("qp2", "vbnet", organization).AddProject("key").AddRule(new SonarRule("vbnet", "vb.rule.id"));
        }
    }

    public Task<ISonarWebServer> CreateSonarWebServer(ProcessedArgs args, IDownloader downloader = null, IDownloader apiDownloader = null) =>
        Task.FromResult((ISonarWebServer)Server);

    public ITargetsInstaller CreateTargetInstaller() =>
        TargetsInstaller;

    public RoslynAnalyzerProvider CreateRoslynAnalyzerProvider(ISonarWebServer server, string localCacheTempPath, ILogger logger, BuildSettings teamBuildSettings, IAnalysisPropertyProvider sonarProperties, IEnumerable<SonarRule> rules, string language)
    {
        LogMethodCalled();
        PluginCachePath = localCacheTempPath;
        return AnalyzerProvider = new(teamBuildSettings, sonarProperties, rules, language) { SettingsToReturn = new AnalyzerSettings { RulesetPath = "c:\\xxx.ruleset" } };
    }

    public BuildSettings ReadSettings()
    {
        var settings = BuildSettings.GetSettingsFromEnvironment();
        settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
        settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");
        return settings;
    }

    public IResolver CreateJreResolver(ISonarWebServer server, string sonarUserHome) =>
        JreResolver;

    public IResolver CreateEngineResolver(ISonarWebServer server, string sonarUserHome) =>
        EngineResolver;

    public void AssertMethodCalled(string methodName, int callCount) =>
        calledMethods.Count(x => x == methodName).Should().Be(callCount, "Method was not called the expected number of times");

    private void LogMethodCalled([CallerMemberName] string methodName = null) =>
        calledMethods.Add(methodName);
}
