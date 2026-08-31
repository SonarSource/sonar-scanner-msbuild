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

namespace SonarScanner.MSBuild.Tasks;

public sealed class DependencyTelemetry : Task
{
    private const string TelemetryKeyPrefix = "dotnetenterprise.s4net.build.dependencies.";

    // Well-known dependencies, matched against both PackageReference ids (SDK-style) and authored <Reference>
    // assembly names (non-SDK-style); for most libraries the two are identical.
    internal static readonly HashSet<string> KnownDependencies = new(StringComparer.OrdinalIgnoreCase)
        {
            // Test / mocking / assertions
            "AutoFixture",
            "BenchmarkDotNet",
            "Bogus",
            "coverlet.collector",
            "coverlet.msbuild",
            "FluentAssertions",
            "Microsoft.NET.Test.Sdk",
            "Moq",
            "MSTest.TestFramework",
            "NSubstitute",
            "NUnit",
            "Shouldly",
            "xunit",
            // Logging / observability
            "log4net",
            "Microsoft.ApplicationInsights.AspNetCore",
            "Microsoft.Extensions.Logging",
            "NLog",
            "OpenTelemetry.Exporter.OpenTelemetryProtocol",
            "OpenTelemetry.Extensions.Hosting",
            "Serilog",
            "Serilog.AspNetCore",
            // Serialization
            "MessagePack",
            "Newtonsoft.Json",
            "protobuf-net",
            "System.Text.Json",
            "YamlDotNet",
            // Web / API / validation / mapping / resilience
            "AutoMapper",
            "FluentValidation",
            "Grpc.AspNetCore",
            "HotChocolate.AspNetCore",
            "MediatR",
            "Microsoft.AspNetCore.Authentication.JwtBearer",
            "Microsoft.AspNetCore.SignalR.Client",
            "Microsoft.Extensions.Http",
            "Polly",
            "Refit",
            "RestSharp",
            "Swashbuckle.AspNetCore",
            // Data / ORM / drivers
            "Dapper",
            "EntityFramework",
            "Microsoft.Data.SqlClient",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.EntityFrameworkCore.Design",
            "Microsoft.EntityFrameworkCore.SqlServer",
            "MongoDB.Driver",
            "Npgsql",
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            "Pomelo.EntityFrameworkCore.MySql",
            "StackExchange.Redis",
            // DI / cloud
            "Autofac",
            "AWSSDK.DynamoDBv2",
            "AWSSDK.S3",
            "AWSSDK.SQS",
            "Azure.Identity",
            "Azure.Messaging.ServiceBus",
            "Azure.Storage.Blobs",
            "Microsoft.Azure.Cosmos",
            "Microsoft.Extensions.DependencyInjection",
            // Auth / identity / misc
            "BCrypt.Net-Next",
            "Microsoft.IdentityModel.Tokens",
            "NodaTime",
            "System.IdentityModel.Tokens.Jwt",
            // .NET Framework assemblies referenced directly by non-SDK-style projects (no NuGet package)
            "PresentationCore",
            "PresentationFramework",
            "System.Configuration",
            "System.DirectoryServices",
            "System.Messaging",
            "System.ServiceModel",
            "System.Web",
            "System.Web.Http",
            "System.Web.Mvc",
            "System.Web.Services",
            "System.Windows.Forms",
            "WindowsBase"
        };

    public ITaskItem[] PackageReferences { get; set; } = [];

    public ITaskItem[] AssemblyReferences { get; set; } = [];

    [Output]
    public ITaskItem[] Telemetry { get; private set; } = [];

    public override bool Execute()
    {
        Telemetry = PackageNames()
            .Concat(AssemblyNames())
            .Where(KnownDependencies.Contains)
            .Select(TelemetryKey)
            .Distinct(StringComparer.Ordinal)
            .Select(CreateTelemetryItem)
            .ToArray();
        return true;
    }

    private IEnumerable<string> PackageNames() =>
        (PackageReferences ?? [])
            .Where(x => !string.IsNullOrEmpty(x.ItemSpec) && !string.Equals(x.GetMetadata("IsImplicitlyDefined"), "true", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ItemSpec);

    private IEnumerable<string> AssemblyNames() =>
        (AssemblyReferences ?? [])
            .Where(IsDirectReference)
            .Select(x => SimpleAssemblyName(x.ItemSpec));

    // Keep only assemblies authored as plain <Reference> items. Skip those with NuGetPackageId as direct Nuget references are caught via PackageReferences, and transitive are unwanted.
    private static bool IsDirectReference(ITaskItem reference) =>
        !string.IsNullOrEmpty(reference.ItemSpec)
        && string.IsNullOrEmpty(reference.GetMetadata("NuGetPackageId"))
        && !string.Equals(reference.GetMetadata("IsImplicitlyDefined"), "true", StringComparison.OrdinalIgnoreCase);

    private static string TelemetryKey(string name) =>
        $"{TelemetryKeyPrefix}{TelemetryUtils.SanitizeKey(name)}.cnt";

    private static ITaskItem CreateTelemetryItem(string key)
    {
        var item = new TaskItem(key);
        item.SetMetadata("Value", "true");
        return item;
    }

    private static string SimpleAssemblyName(string itemSpec) =>
        itemSpec.Split(',')[0].Trim();
}
