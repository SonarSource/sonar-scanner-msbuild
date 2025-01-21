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
using System.Diagnostics;
using System.IO;
using SonarScanner.MSBuild.Common;

namespace SonarScanner.MSBuild.Tasks;

/// <summary>
/// Helper methods used by multiple tasks
/// </summary>
public static class TaskUtilities
{
    // Workaround for the file locking issue: retry after a short period.
    public const int MaxConfigRetryPeriodInMilliseconds = 2500; // Maximum time to spend trying to access the config file

    public const int DelayBetweenRetriesInMilliseconds = 499; // Period to wait between retries

    #region Public methods

    /// <summary>
    /// Attempts to load the analysis config from the specified directory.
    /// Will retry if the file is locked.
    /// </summary>
    /// <returns>The loaded configuration, or null if the file does not exist or could not be
    /// loaded e.g. due to being locked</returns>
    public static AnalysisConfig TryGetConfig(string configDir, ILogger logger)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        AnalysisConfig config = null;
        if (string.IsNullOrEmpty(configDir)) // not specified
        {
            return null;
        }

        var fullAnalysisPath = Path.Combine(configDir, FileConstants.ConfigFileName);
        logger.LogDebug(Resources.Shared_ReadingConfigFile, fullAnalysisPath);
        if (!File.Exists(fullAnalysisPath))
        {
            logger.LogDebug(Resources.Shared_ConfigFileNotFound);
            return null;
        }

        var succeeded = Utilities.Retry(MaxConfigRetryPeriodInMilliseconds, DelayBetweenRetriesInMilliseconds, logger,
            () => DoLoadConfig(fullAnalysisPath, logger, out config));
        if (succeeded)
        {
            logger.LogDebug(Resources.Shared_ReadingConfigSucceeded, fullAnalysisPath);
        }
        else
        {
            logger.LogError(Resources.Shared_ReadingConfigFailed, fullAnalysisPath);
        }
        return config;
    }

    #endregion Public methods

    #region Private methods

    /// <summary>
    /// Attempts to load the config file, suppressing any IO errors that occur.
    /// This method is expected to be called inside a "retry"
    /// </summary>
    private static bool DoLoadConfig(string filePath, ILogger logger, out AnalysisConfig config)
    {
        Debug.Assert(File.Exists(filePath), "Expecting the config file to exist: " + filePath);
        config = null;

        try
        {
            config = AnalysisConfig.Load(filePath);
        }
        catch (IOException e)
        {
            // Log this as a message for info. We'll log an error if all of the re-tries failed
            logger.LogDebug(Resources.Shared_ErrorReadingConfigFile, e.Message);
            return false;
        }
        return true;
    }

    #endregion Private methods
}
