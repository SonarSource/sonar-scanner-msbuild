/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Linq;
using SonarScanner.MSBuild.Common.CommandLine;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Handles validating and fetch analysis properties from the command line
/// </summary>
public class CmdLineArgPropertyProvider : IAnalysisPropertyProvider
{
    public const string DynamicPropertyArgumentId = "dynamic.property";

    public static readonly ArgumentDescriptor Descriptor = new ArgumentDescriptor(
        id: DynamicPropertyArgumentId, prefixes: CommandLineFlagPrefix.GetPrefixedFlags("d:"), required: false, allowMultiple: true,
        description: Resources.CmdLine_ArgDescription_DynamicProperty);

    private readonly IEnumerable<Property> properties;

    #region Public methods

    /// <summary>
    /// Attempts to construct and return a provider that uses analysis properties provided on the command line
    /// </summary>
    /// <param name="commandLineArguments">List of command line arguments (optional)</param>
    /// <returns>False if errors occurred when constructing the provider, otherwise true</returns>
    /// <remarks>If no properties were provided on the command line then an empty provider will be returned</remarks>
    public static bool TryCreateProvider(IEnumerable<ArgumentInstance> commandLineArguments, ILogger logger,
        out IAnalysisPropertyProvider provider)
    {
        if (commandLineArguments == null)
        {
            throw new ArgumentNullException(nameof(commandLineArguments));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        if (ExtractAndValidateProperties(commandLineArguments, logger, out var validProperties))
        {
            if (validProperties.Any())
            {
                provider = new CmdLineArgPropertyProvider(validProperties);
            }
            else
            {
                provider = EmptyPropertyProvider.Instance;
            }

            return true;
        }

        provider = null;
        return false;
    }

    #endregion Public methods

    #region IAnalysisPropertyProvider interface

    public bool TryGetProperty(string key, out Property property)
    {
        return Property.TryGetProperty(key, properties, out property);
    }

    public IEnumerable<Property> GetAllProperties()
    {
        return properties ?? Enumerable.Empty<Property>();
    }

    #endregion IAnalysisPropertyProvider interface

    #region Private methods

    private CmdLineArgPropertyProvider(IEnumerable<Property> properties)
    {
        this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
    }

    #endregion Private methods

    #region Analysis properties handling

    /// <summary>
    /// Fetches and processes any analysis properties from the command line arguments
    /// </summary>
    /// <param name="analysisProperties">The extracted analysis properties, if any</param>
    /// <returns>Returns false if any errors are encountered, otherwise true</returns>
    /// <remarks>
    /// Analysis properties (/d:[key]=[value] arguments) need further processing. We need
    /// to extract the key-value pairs and check for duplicate keys.
    /// </remarks>
    private static bool ExtractAndValidateProperties(IEnumerable<ArgumentInstance> arguments, ILogger logger,
        out IEnumerable<Property> analysisProperties)
    {
        var containsDuplicateProperty = false;
        var containsAnalysisProperty = false;

        var validProperties = new List<Property>();

        foreach (var argument in arguments.Where(a => a.Descriptor.Id == DynamicPropertyArgumentId))
        {
            if (Property.Parse(argument.Value) is { } property)
            {
                if (Property.TryGetProperty(property.Id, validProperties, out var existing))
                {
                    logger.LogError(Resources.ERROR_CmdLine_DuplicateProperty, argument.Value, existing.Value);
                    containsDuplicateProperty = true;
                }
                else
                {
                    validProperties.Add(property);
                }
            }
            else
            {
                logger.LogError(Resources.ERROR_CmdLine_InvalidAnalysisProperty, argument.Value);
                containsAnalysisProperty = true;
            }
        }

        // Check for named parameters that can't be set by dynamic properties
        var containsProjectKey = ContainsNamedParameter(SonarProperties.ProjectKey, validProperties, logger,
            Resources.ERROR_CmdLine_MustUseProjectKey);
        var containsProjectName = ContainsNamedParameter(SonarProperties.ProjectName, validProperties, logger,
            Resources.ERROR_CmdLine_MustUseProjectName);
        var containsProjectVersion = ContainsNamedParameter(SonarProperties.ProjectVersion, validProperties, logger,
            Resources.ERROR_CmdLine_MustUseProjectVersion);
        var containsOrganization = ContainsNamedParameter(SonarProperties.Organization, validProperties, logger,
            Resources.ERROR_CmdLine_MustUseOrganization);

        // Check for others properties that can't be set
        var containsUnsettableWorkingDirectory = ContainsUnsettableParameter(SonarProperties.WorkingDirectory, validProperties,
            logger);

        analysisProperties = validProperties;

        return !containsDuplicateProperty &&
            !containsAnalysisProperty &&
            !containsProjectKey &&
            !containsProjectName &&
            !containsProjectVersion &&
            !containsOrganization &&
            !containsUnsettableWorkingDirectory;
    }

    private static bool ContainsNamedParameter(string propertyName, IEnumerable<Property> properties, ILogger logger,
        string errorMessage)
    {
        if (Property.TryGetProperty(propertyName, properties, out var existing))
        {
            logger.LogError(errorMessage);
            return true;
        }
        return false;
    }

    private static bool ContainsUnsettableParameter(string propertyName, IEnumerable<Property> properties, ILogger logger)
    {
        if (Property.TryGetProperty(propertyName, properties, out var existing))
        {
            logger.LogError(Resources.ERROR_CmdLine_CannotSetPropertyOnCommandLine, propertyName);
            return true;
        }
        return false;
    }

    #endregion Analysis properties handling
}
