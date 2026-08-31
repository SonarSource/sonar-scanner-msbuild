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

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SonarScanner.MSBuild.Tasks.UnitTest;

[TestClass]
public class DependencyTelemetryTests
{
    [TestMethod]
    [DataRow("FluentAssertions", "dotnetenterprise.s4net.build.dependencies.fluentassertions.cnt")]
    [DataRow("Microsoft.EntityFrameworkCore", "dotnetenterprise.s4net.build.dependencies.microsoft_entityframeworkcore.cnt")]
    public void WhitelistedPackage_IsReported(string id, string expectedKey) =>
        Keys(Execute([CreateDependency(id)])).Should().ContainSingle().Which.Should().Be(expectedKey);

    [TestMethod]
    [DataRow("System.Web", "dotnetenterprise.s4net.build.dependencies.system_web.cnt")]
    [DataRow("System.Web.Services", "dotnetenterprise.s4net.build.dependencies.system_web_services.cnt")]
    [DataRow("System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "dotnetenterprise.s4net.build.dependencies.system_web.cnt")]
    public void WhitelistedReference_IsReported(string spec, string expectedKey) =>
        Keys(Execute(references: [CreateReference(spec)])).Should().ContainSingle().Which.Should().Be(expectedKey);

    [TestMethod]
    public void KnownDependency_TelemetryValueIsTrue() =>
        Execute([CreateDependency("Serilog")]).Telemetry.Should().ContainSingle().Which.GetMetadata("Value").Should().Be("true");

    [TestMethod]
    [DataRow("Some.Random.Package", null, false)]
    [DataRow("Newtonsoft.Json", "Newtonsoft.Json", false)]
    [DataRow("System.Web", null, true)]
    public void UnwantedReference_IsNotReported(string spec, string nuGetPackageId, bool isImplicit) =>
        Execute(references: [CreateReference(spec, nuGetPackageId, isImplicit)]).Telemetry.Should().BeEmpty();

    [TestMethod]
    [DataRow("Some.Random.Package", false)]
    [DataRow("Microsoft.Extensions.Logging", true)]
    public void UnwantedPackage_IsNotReported(string id, bool isImplicit) =>
        Execute([CreateDependency(id, isImplicit)]).Telemetry.Should().BeEmpty();

    [TestMethod]
    public void Matching_IsCaseInsensitive() =>
        Keys(Execute([CreateDependency("fluentassertions")], [CreateReference("system.WEB")])).Should().BeEquivalentTo(
            "dotnetenterprise.s4net.build.dependencies.fluentassertions.cnt",
            "dotnetenterprise.s4net.build.dependencies.system_web.cnt");

    [TestMethod]
    public void MixedInputs_ReportedAndDeduplicated() =>
        Keys(Execute(
            [CreateDependency("Serilog"), CreateDependency("Dapper"), CreateDependency("Unknown"), CreateDependency("Serilog")],
            [CreateReference("System.Windows.Forms"), CreateReference("mscorlib")])).Should().BeEquivalentTo(
            "dotnetenterprise.s4net.build.dependencies.dapper.cnt",
            "dotnetenterprise.s4net.build.dependencies.serilog.cnt",
            "dotnetenterprise.s4net.build.dependencies.system_windows_forms.cnt");

    [TestMethod]
    public void Empty_Inputs_ProduceNoTelemetry() =>
        Execute().Telemetry.Should().BeEmpty();

    [TestMethod]
    public void Null_Inputs_ProduceNoTelemetry()
    {
        var engine = new DummyBuildEngine();
        var task = new DependencyTelemetry { BuildEngine = engine, PackageReferences = null, AssemblyReferences = null };

        task.Execute().Should().BeTrue();
        engine.AssertNoErrors();
        engine.AssertNoWarnings();
        task.Telemetry.Should().BeEmpty();
    }

    private static DependencyTelemetry Execute(ITaskItem[] packages = null, ITaskItem[] references = null)
    {
        var engine = new DummyBuildEngine();
        var task = new DependencyTelemetry
        {
            BuildEngine = engine,
            PackageReferences = packages ?? [],
            AssemblyReferences = references ?? []
        };

        task.Execute().Should().BeTrue();
        engine.AssertNoErrors();
        engine.AssertNoWarnings();
        return task;
    }

    private static IEnumerable<string> Keys(DependencyTelemetry task) =>
        task.Telemetry.Select(x => x.ItemSpec);

    private static TaskItem CreateDependency(string id, bool isImplicit = false)
    {
        var item = new TaskItem(id);
        if (isImplicit)
        {
            item.SetMetadata("IsImplicitlyDefined", "true");
        }
        return item;
    }

    private static TaskItem CreateReference(string spec, string nuGetPackageId = null, bool isImplicit = false)
    {
        var item = new TaskItem(spec);
        if (nuGetPackageId is not null)
        {
            item.SetMetadata("NuGetPackageId", nuGetPackageId);
        }
        if (isImplicit)
        {
            item.SetMetadata("IsImplicitlyDefined", "true");
        }
        return item;
    }
}
