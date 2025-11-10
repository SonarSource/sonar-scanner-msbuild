/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

using static SonarScanner.MSBuild.Common.CommandLine.CommandLineFlagPrefix;

namespace SonarScanner.MSBuild.PreProcessor;

/// <summary>
/// Process and validates the pre-processor command line arguments and reports any errors.
/// </summary>
public static class ArgumentProcessor
{
    private const string ProjectKeyId = "projectKey.id";
    private const string ProjectNameId = "projectName.id";
    private const string ProjectVersionId = "projectVersion.id";
    private const string OrganizationId = "organization.id";
    private const string InstallLoaderTargetsId = "installLoaderTargets.id";

    private static readonly IList<ArgumentDescriptor> Descriptors =
    [
        new(id: ProjectKeyId, prefixes: GetPrefixedFlags("key:", "k:"), required: true, allowMultiple: false, description: Resources.CmdLine_ArgDescription_ProjectKey),
        new(id: ProjectNameId, prefixes: GetPrefixedFlags("name:", "n:"), required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_ProjectName),
        new(id: ProjectVersionId, prefixes: GetPrefixedFlags("version:", "v:"), required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_ProjectVersion),
        new(id: OrganizationId, prefixes: GetPrefixedFlags("organization:", "o:"), required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_Organization),
        new(id: InstallLoaderTargetsId, prefixes: GetPrefixedFlags("install:"), required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_InstallTargets),

        FilePropertyProvider.Descriptor,
        CmdLineArgPropertyProvider.Descriptor
    ];

    static ArgumentProcessor()
    {
        Debug.Assert(Descriptors.All(x => x.Prefixes is not null && x.Prefixes.Any()), "All descriptors must provide at least one prefix");
        Debug.Assert(Descriptors.Select(x => x.Id).Distinct().Count() == Descriptors.Count, "All descriptors must have a unique id");
    }

    /// <summary>
    /// Attempts to process the supplied command line arguments and
    /// reports any errors using the logger.
    /// Returns null unless all the properties are valid.
    /// </summary>
    public static ProcessedArgs TryProcessArgs(IEnumerable<string> commandLineArgs, BuildSettings buildSettings, IRuntime runtime)
    {
        _ = runtime ?? throw new ArgumentNullException(nameof(runtime));

        ProcessedArgs processed = null;

        // This call will fail if there are duplicate, missing, or unrecognized arguments
        var parser = new CommandLineParser(Descriptors, false /* don't allow unrecognized */);
        var parsedOk = parser.ParseArguments(commandLineArgs, runtime.Logger, out var arguments);

        // Handle the /install: command line only argument
        parsedOk &= TryGetInstallTargetsEnabled(arguments, runtime.Logger, out var installLoaderTargets);

        // Handler for command line analysis properties
        parsedOk &= CmdLineArgPropertyProvider.TryCreateProvider(arguments, runtime.Logger, out var cmdLineProperties);

        // Handler for scanner environment properties
        parsedOk &= EnvScannerPropertiesProvider.TryCreateProvider(runtime.Logger, out var scannerEnvProperties);

        // Handler for property file
        var asmPath = Path.GetDirectoryName(typeof(ArgumentProcessor).Assembly.Location);
        parsedOk &= FilePropertyProvider.TryCreateProvider(arguments, asmPath, runtime.Logger, out var globalFileProperties);

        if (parsedOk)
        {
            Debug.Assert(cmdLineProperties is not null, "When parse is valid, expected cmd line properties to be non-null");
            Debug.Assert(globalFileProperties is not null, "When parse is valid, expected global file properties to be non-null");

            processed = new ProcessedArgs(
                ArgumentValue(ProjectKeyId, arguments),
                ArgumentValue(ProjectNameId, arguments),
                ArgumentValue(ProjectVersionId, arguments),
                ArgumentValue(OrganizationId, arguments),
                installLoaderTargets,
                cmdLineProperties,
                globalFileProperties,
                scannerEnvProperties,
                buildSettings,
                runtime);

            if (!processed.IsValid)
            {
                processed = null;
            }
        }

        return processed;
    }

    private static string ArgumentValue(string id, IEnumerable<ArgumentInstance> arguments) =>
        arguments.Where(x => x.Descriptor.Id == id).Select(x => x.Value).SingleOrDefault();

    private static bool TryGetInstallTargetsEnabled(IEnumerable<ArgumentInstance> arguments, ILogger logger, out bool installTargetsEnabled)
    {
        var hasInstallTargetsVerb = ArgumentInstance.TryGetArgument(InstallLoaderTargetsId, arguments, out var argumentInstance);

        if (hasInstallTargetsVerb)
        {
            var canParse = bool.TryParse(argumentInstance.Value, out installTargetsEnabled);

            if (!canParse)
            {
                logger.LogError(Resources.ERROR_CmdLine_InvalidInstallTargetsValue, argumentInstance.Value);
                return false;
            }
        }
        else
        {
            installTargetsEnabled = TargetsInstaller.DefaultInstallSetting;
        }

        return true;
    }
}
