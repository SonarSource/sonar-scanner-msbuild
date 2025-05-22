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

using SonarScanner.MSBuild.Common.CommandLine;

namespace SonarScanner.MSBuild;

/// <summary>
/// Processes the command line arguments.
/// Supports the standard property-related arguments automatically (i.e. /d: and /s:).
/// The appropriate "additionalDescriptors" should be supplied to provide support for other command line arguments.
/// </summary>
public static class ArgumentProcessor
{
    // FIX: this code is very similar to that in the pre-processor. Consider refactoring to avoid duplication
    // once the other argument and properties-writing tickets have been completed.

    #region Arguments definitions

    private const string BeginId = "begin.id";
    private const string EndId = "end.id";
    public const string HelpId = "help.id";

    public const string BeginVerb = "begin";
    public const string EndVerb = "end";

    // Initialize the set of valid descriptors.
    // To add a new argument, just add it to the list.
    private static readonly List<ArgumentDescriptor> Descriptors =
        [
            new(id: BeginId, prefixes: [BeginVerb], required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_Begin, isVerb: true),
            new(id: EndId, prefixes: [EndVerb], required: false, allowMultiple: false, description: Resources.CmdLine_ArgDescription_End, isVerb: true),
            FilePropertyProvider.Descriptor,
            CmdLineArgPropertyProvider.Descriptor
        ];

    static ArgumentProcessor()
    {
        Debug.Assert(Descriptors.TrueForAll(d => d.Prefixes != null && d.Prefixes.Any()), "All descriptors must provide at least one prefix");
        Debug.Assert(Descriptors.Select(d => d.Id).Distinct().Count() == Descriptors.Count, "All descriptors must have a unique id");
    }

    #endregion Arguments definitions

    public static bool IsHelp(string[] commandLineArgs) =>
        commandLineArgs.Length == 0
        || Array.Exists(CommandLineFlagPrefix.GetPrefixedFlags("?", "h", "help"), commandLineArgs.Contains);

    /// <summary>
    /// Attempts to process the supplied command line arguments and reports any errors using the logger.
    /// Returns false if any parsing errors were encountered.
    /// </summary>
    public static bool TryProcessArgs(string[] commandLineArgs, ILogger logger, out IBootstrapperSettings settings)
    {
        _ = commandLineArgs ?? throw new ArgumentNullException(nameof(commandLineArgs));
        _ = logger ?? throw new ArgumentNullException(nameof(logger));

        // This call will fail if there are duplicate or missing arguments
        var parsedOk = new CommandLineParser(Descriptors, true).ParseArguments(commandLineArgs, logger, out var arguments);

        // Handler for command line analysis properties
        parsedOk &= CmdLineArgPropertyProvider.TryCreateProvider(arguments, logger, out var cmdLineProperties);

        // Handler for property file
        var asmPath = Path.GetDirectoryName(typeof(ArgumentProcessor).Assembly.Location);
        parsedOk &= FilePropertyProvider.TryCreateProvider(arguments, asmPath, logger, out var globalFileProperties);

        parsedOk &= TryGetPhase(arguments, logger, out var phase);

        if (parsedOk)
        {
            Debug.Assert(cmdLineProperties is not null, "When parse is valid, expected cmd line properties to be non-null");
            Debug.Assert(globalFileProperties is not null, "When parse is valid, expected global file properties to be non-null");

            var properties = new AggregatePropertiesProvider(cmdLineProperties, globalFileProperties);
            var baseChildArgs = commandLineArgs.Except([BeginVerb, EndVerb]).ToList(); // We don't want to forward these to the pre- or post- processor.

            settings = phase == AnalysisPhase.PreProcessing
                ? CreatePreProcessorSettings(baseChildArgs, properties, globalFileProperties, logger)
                : CreatePostProcessorSettings(baseChildArgs, properties, logger);
            return true;
        }
        else
        {
            settings = null;
            return false;
        }
    }

    private static bool TryGetPhase(IEnumerable<ArgumentInstance> arguments, ILogger logger, out AnalysisPhase phase)
    {
        // The command line parser will already have checked for duplicates
        var hasBeginVerb = ArgumentInstance.TryGetArgument(BeginId, arguments, out var _);
        var hasEndVerb = ArgumentInstance.TryGetArgument(EndId, arguments, out var _);

        if (hasBeginVerb && hasEndVerb)
        {
            logger.LogError(Resources.ERROR_CmdLine_BothBeginAndEndSupplied);
            phase = AnalysisPhase.Unspecified;
            return false;
        }
        else if (!hasBeginVerb && !hasEndVerb)
        {
            logger.LogError(Resources.ERROR_CmdLine_NeitherBeginNorEndSupplied);
            phase = AnalysisPhase.Unspecified;
            return false;
        }
        else
        {
            phase = hasBeginVerb ? AnalysisPhase.PreProcessing : AnalysisPhase.PostProcessing;
            return true;
        }
    }

    private static IBootstrapperSettings CreatePreProcessorSettings(ICollection<string> childArgs, IAnalysisPropertyProvider properties, IAnalysisPropertyProvider globalFileProperties, ILogger logger)
    {
        // If we're using the default properties file then we need to pass it
        // explicitly to the pre-processor (it's in a different folder and won't
        // be able to find it otherwise).
        if (globalFileProperties is FilePropertyProvider { IsDefaultSettingsFile: true } fileProvider)
        {
            Debug.Assert(fileProvider.PropertiesFile is not null, "Expected the properties file to be non-null");
            Debug.Assert(!string.IsNullOrEmpty(fileProvider.PropertiesFile.FilePath), "Expected the properties file path to be set");
            childArgs.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}{1}", FilePropertyProvider.Prefix, fileProvider.PropertiesFile.FilePath));
        }

        return CreateSettings(AnalysisPhase.PreProcessing, childArgs, properties, logger);
    }

    private static IBootstrapperSettings CreatePostProcessorSettings(IEnumerable<string> childArgs, IAnalysisPropertyProvider properties, ILogger logger) =>
        CreateSettings(AnalysisPhase.PostProcessing, childArgs, properties, logger);

    private static IBootstrapperSettings CreateSettings(AnalysisPhase phase, IEnumerable<string> childArgs, IAnalysisPropertyProvider properties, ILogger logger) =>
        new BootstrapperSettings(phase, childArgs, VerbosityCalculator.ComputeVerbosity(properties, logger), logger);
}
