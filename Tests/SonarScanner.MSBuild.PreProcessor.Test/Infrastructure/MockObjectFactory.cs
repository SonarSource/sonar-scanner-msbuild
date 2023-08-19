/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2023 SonarSource SA
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

using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SonarScanner.MSBuild.Common;
using SonarScanner.MSBuild.Common.TFS;
using SonarScanner.MSBuild.PreProcessor.Roslyn.Model;
using TestUtilities;

namespace SonarScanner.MSBuild.PreProcessor.Test
{
    internal class MockObjectFactory : IPreprocessorObjectFactory
    {
        public TestLogger Logger { get; } = new();
        public MockSonarWebServer Server { get; }
        public Mock<ITargetsInstaller> TargetsInstaller { get; } = new();
        public MockRoslynAnalyzerProvider AnalyzerProvider { get; } = new() { SettingsToReturn = new AnalyzerSettings { RulesetPath = "c:\\xxx.ruleset" } };

        public MockObjectFactory(TestLogger logger) : this() =>
            Logger = logger;

        public MockObjectFactory(bool withDefaultRules = true, string organization = null)
        {
            Server = new(organization);

            var data = Server.Data;
            data.ServerProperties.Add("server.key", "server value 1");
            data.Languages.Add("cs");
            data.Languages.Add("vbnet");
            data.Languages.Add("another_plugin");

            if (withDefaultRules)
            {
                data.AddQualityProfile("qp1", "cs", organization).AddProject("key").AddRule(new SonarRule("csharpsquid", "cs.rule.id"));
                data.AddQualityProfile("qp2", "vbnet", organization).AddProject("key").AddRule(new SonarRule("vbnet", "vb.rule.id"));
            }
        }

        public Task<ISonarWebServer> CreateSonarWebServer(ProcessedArgs args, IDownloader downloader = null) =>
            Task.FromResult((ISonarWebServer)Server);

        public ITargetsInstaller CreateTargetInstaller() =>
            TargetsInstaller.Object;

        public IAnalyzerProvider CreateRoslynAnalyzerProvider(ISonarWebServer server, string localCacheTempPath) =>
            AnalyzerProvider;

        public BuildSettings ReadSettings()
        {
            var settings = BuildSettings.GetSettingsFromEnvironment(Logger);
            settings.Should().NotBeNull("Test setup error: TFS environment variables have not been set correctly");
            settings.BuildEnvironment.Should().Be(BuildEnvironment.NotTeamBuild, "Test setup error: build environment was not set correctly");
            return settings;
        }
    }
}
