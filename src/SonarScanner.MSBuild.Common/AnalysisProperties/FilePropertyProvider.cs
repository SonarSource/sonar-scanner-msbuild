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

using SonarScanner.MSBuild.Common.CommandLine;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Handles locating an analysis properties file and returning the appropriate properties
/// </summary>
public class FilePropertyProvider : IAnalysisPropertyProvider
{
    public const string DefaultFileName = "SonarQube.Analysis.xml";
    public const string Prefix = "/s:";
    public const string DescriptorPrefix = "s:";
    private const string DescriptorId = "properties.file.argument";

    public static readonly ArgumentDescriptor Descriptor = new(DescriptorId, CommandLineFlagPrefix.GetPrefixedFlags(DescriptorPrefix),
        false, Resources.CmdLine_ArgDescription_PropertiesFilePath, false);

    public AnalysisProperties PropertiesFile { get; }
    public bool IsDefaultSettingsFile => IsDefaultPropertiesFile;
    public bool IsDefaultPropertiesFile { get; private set; }
    public PropertyProviderKind ProviderType => PropertyProviderKind.SONARQUBE_ANALYSIS_XML;

    #region Public methods

    /// <summary>
    /// Attempts to construct and return a file-based properties provider
    /// </summary>
    /// <param name="defaultPropertiesFileDirectory">Directory in which to look for the default properties file (optional)</param>
    /// <param name="commandLineArguments">List of command line arguments (optional)</param>
    /// <returns>False if errors occurred when constructing the provider, otherwise true</returns>
    /// <remarks>If a properties file could not be located then an empty provider will be returned</remarks>
    public static bool TryCreateProvider(IEnumerable<ArgumentInstance> commandLineArguments, string defaultPropertiesFileDirectory, ILogger logger, out IAnalysisPropertyProvider provider)
    {
        if (commandLineArguments is null)
        {
            throw new ArgumentNullException(nameof(commandLineArguments));
        }
        if (string.IsNullOrWhiteSpace(defaultPropertiesFileDirectory))
        {
            throw new ArgumentNullException(nameof(defaultPropertiesFileDirectory));
        }
        if (logger is null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        // If the path to a properties file was specified on the command line, use that.
        // Otherwise, look for a default properties file in the default directory.
        var settingsFileArgExists = ArgumentInstance.TryGetArgumentValue(DescriptorId, commandLineArguments,
            out var propertiesFilePath);

        if (ResolveFilePath(propertiesFilePath, defaultPropertiesFileDirectory, logger,
            out var locatedPropertiesFile))
        {
            if (locatedPropertiesFile is null)
            {
                provider = EmptyPropertyProvider.Instance;
            }
            else
            {
                provider = new FilePropertyProvider(locatedPropertiesFile, !settingsFileArgExists);
            }
            return true;
        }

        provider = null;
        return false;
    }

    public static FilePropertyProvider Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(Resources.ERROR_SettingsFileNotFound, filePath);
        }

        var properties = AnalysisProperties.Load(filePath);
        var provider = new FilePropertyProvider(properties, false);
        return provider;
    }

    #endregion Public methods

    #region IAnalysisPropertyProvider methods

    public IEnumerable<Property> GetAllProperties() =>
        PropertiesFile ?? Enumerable.Empty<Property>();

    public bool TryGetProperty(string key, out Property property) =>
        Property.TryGetProperty(key, PropertiesFile, out property);

    #endregion IAnalysisPropertyProvider methods

    #region Private methods

    private FilePropertyProvider(AnalysisProperties properties, bool isDefaultPropertiesFile)
    {
        PropertiesFile = properties ?? throw new ArgumentNullException(nameof(properties));
        IsDefaultPropertiesFile = isDefaultPropertiesFile;
    }

    /// <summary>
    /// Attempt to find a properties file - either the one specified by the user, or the default properties file.
    /// Returns true if the path to a file could be resolved, otherwise false.
    /// </summary>
    private static bool ResolveFilePath(string propertiesFilePath, string defaultPropertiesFileDirectory, ILogger logger, out AnalysisProperties properties)
    {
        properties = null;
        var isValid = true;

        var resolvedPath = propertiesFilePath ?? TryGetDefaultPropertiesFilePath(defaultPropertiesFileDirectory, logger);

        if (resolvedPath is not null)
        {
            // The File APIs below will automatically work with relative paths, but we resolve
            // the path anyway because we want to show better error messages, containing the
            // actual path where we were looking for the properties files.
            resolvedPath = Path.GetFullPath(resolvedPath.Trim());

            if (File.Exists(resolvedPath))
            {
                try
                {
                    logger.LogDebug(Resources.MSG_Properties_LoadingPropertiesFromFile, resolvedPath);
                    properties = AnalysisProperties.Load(resolvedPath);
                }
                catch (InvalidOperationException)
                {
                    logger.LogError(Resources.ERROR_Properties_InvalidPropertiesFile, resolvedPath);
                    isValid = false;
                }
            }
            else
            {
                logger.LogError(Resources.ERROR_Properties_GlobalPropertiesFileDoesNotExist, resolvedPath);
                isValid = false;
            }
        }
        return isValid;
    }

    private static string TryGetDefaultPropertiesFilePath(string defaultDirectory, ILogger logger)
    {
        var fullPath = Path.Combine(defaultDirectory, DefaultFileName);
        if (File.Exists(fullPath))
        {
            logger.LogDebug(Resources.MSG_Properties_DefaultPropertiesFileFound, fullPath);
            return fullPath;
        }
        else
        {
            logger.LogDebug(Resources.MSG_Properties_DefaultPropertiesFileNotFound, fullPath);

            return null;
        }
    }

    #endregion Private methods
}
