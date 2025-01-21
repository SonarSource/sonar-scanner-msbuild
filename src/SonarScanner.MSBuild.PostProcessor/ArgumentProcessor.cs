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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.PostProcessor;

/// <summary>
/// Processes the command line arguments.
/// Supports the standard property-related arguments automatically (i.e. /d: and /s:).
/// The appropriate "additionalDescriptors" should be supplied to provide support for other command line arguments.
/// </summary>
public static class ArgumentProcessor
{
    #region Arguments definitions

    private static IList<ArgumentDescriptor> Descriptors;

    static ArgumentProcessor()
    {
        // Initialize the set of valid descriptors.
        // To add a new argument, just add it to the list.
        Descriptors = new List<ArgumentDescriptor>
        {
            CmdLineArgPropertyProvider.Descriptor
        };

        Debug.Assert(Descriptors.All(d => d.Prefixes != null && d.Prefixes.Any()), "All descriptors must provide at least one prefix");
        Debug.Assert(Descriptors.Select(d => d.Id).Distinct().Count() == Descriptors.Count, "All descriptors must have a unique id");
    }

    #endregion Arguments definitions

    #region Public methods

    /// <summary>
    /// Attempts to process the supplied command line arguments and
    /// reports any errors using the logger.
    /// Returns false if any parsing errors were encountered.
    /// </summary>
    public static bool TryProcessArgs(IEnumerable<string> commandLineArgs, ILogger logger, out IAnalysisPropertyProvider provider)
    {
        if (commandLineArgs == null)
        {
            throw new ArgumentNullException(nameof(commandLineArgs));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        provider = null;

        // This call will fail if there are duplicate or missing arguments
        var parser = new CommandLineParser(Descriptors, false /* don't allow unrecognized arguments*/);
        var parsedOk = parser.ParseArguments(commandLineArgs, logger, out var arguments);

        if (parsedOk)
        {
            // Handler for command line analysis properties
            parsedOk &= CmdLineArgPropertyProvider.TryCreateProvider(arguments, logger, out provider);

            Debug.Assert(!parsedOk || provider != null);

            if (parsedOk && !AreParsedArgumentsValid(provider, logger))
            {
                provider = null;
            }
        }

        return provider != null;
    }

    #endregion Public methods

    #region Private methods

    /// <summary>
    /// Performs any additional validation on the parsed arguments and logs errors
    /// if necessary.
    /// </summary>
    /// <returns>True if the arguments are valid, otherwise false</returns>
    private static bool AreParsedArgumentsValid(IAnalysisPropertyProvider provider, ILogger logger)
    {
        var areValid = true;

        foreach (var property in provider.GetAllProperties())
        {
            if (!IsPermittedProperty(property))
            {
                areValid = false;
                logger.LogError(Resources.ERROR_CmdLine_DisallowedArgument, property.Id);
            }
        }

        return areValid;
    }

    /// <summary>
    /// Determines whether the supplied property is accepted by the post-processor
    /// </summary>
    public static bool IsPermittedProperty(Property property)
    {
        // Currently the post-processor only accepts command line arguments that
        // will be stripped from the the pre-processor command line
        return SonarProperties.SensitivePropertyKeys.Any(marker => Property.AreKeysEqual(marker, property.Id));
    }

    #endregion Private methods
}
