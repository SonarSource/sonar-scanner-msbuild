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

using System.Runtime.CompilerServices;

namespace SonarScanner.MSBuild.PreProcessor.Test;

internal class PreprocessorObjectFactoryStub : PreprocessorObjectFactory
{
    private readonly List<string> calledMethods = [];

    public TestRuntime Runtime { get; } = new();
    public SonarQubeBase Client { get; set; } = MockSonarQube.Create();
    public IResolver JreResolver { get; } = Substitute.For<IResolver>();
    public IResolver EngineResolver { get; } = Substitute.For<IResolver>();
    public IResolver ScannerCliResolver { get; } = Substitute.For<IResolver>();
    public string PluginCachePath { get; private set; }
    public MockRoslynAnalyzerProvider AnalyzerProvider { get; private set; }

    public PreprocessorObjectFactoryStub(TestRuntime runtime) : this(runtime, true) { }

    public PreprocessorObjectFactoryStub(bool withDefaultRules = true)
        : this(new TestRuntime(), withDefaultRules) { }

    private PreprocessorObjectFactoryStub(TestRuntime runtime, bool withDefaultRules) : base(runtime)
    {
        Client.ServerVersion.Returns("2026.1");
        Client.DownloadProperties(null, null).ReturnsForAnyArgs(new Dictionary<string, string> { { "server.key", "server value 1" } });
        Client.DownloadAllLanguages().Returns(["cs", "vbnet", "another_plugin"]);
        if (withDefaultRules)
        {
            Client.DownloadRules("qp1").Returns([new SonarRule("csharpsquid", "cs.rule.id")]);
            Client.DownloadRules("qp2").Returns([new SonarRule("vbnet", "vb.rule.id")]);
        }
    }

    public override Task<SonarQubeBase> CreateClient(ProcessedArgs args, IDownloader webDownloader = null, IDownloader apiDownloader = null) =>
        Task.FromResult(Client);

    public override RoslynAnalyzerProvider CreateRoslynAnalyzerProvider(SonarQubeBase client,
                                                                        string localCacheTempPath,
                                                                        BuildSettings teamBuildSettings,
                                                                        IAnalysisPropertyProvider sonarProperties,
                                                                        IEnumerable<SonarRule> rules,
                                                                        string language)
    {
        LogMethodCalled();
        PluginCachePath = localCacheTempPath;
        return AnalyzerProvider = new(teamBuildSettings, sonarProperties, rules, language) { SettingsToReturn = new AnalyzerSettings { RulesetPath = "c:\\xxx.ruleset" } };
    }

    public override IResolver CreateJreResolver(SonarQubeBase client, string sonarUserHome) =>
        JreResolver;

    public override IResolver CreateEngineResolver(SonarQubeBase client, string sonarUserHome) =>
        EngineResolver;

    public override IResolver CreateScannerCliResolver(SonarQubeBase client, string sonarUserHome) =>
        ScannerCliResolver;

    public void AssertMethodCalled(string methodName, int callCount) =>
        calledMethods.Count(x => x == methodName).Should().Be(callCount, "Method was not called the expected number of times");

    private void LogMethodCalled([CallerMemberName] string methodName = null) =>
        calledMethods.Add(methodName);
}
