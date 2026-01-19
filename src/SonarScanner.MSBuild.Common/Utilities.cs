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

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace SonarScanner.MSBuild.Common;

public static class Utilities
{
    #region Public methods

    public static string ScannerVersion => typeof(Utilities).Assembly.GetName().Version.ToDisplayString();

    /// <summary>
    /// Retries the specified operation until the specified timeout period expires
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="op">The operation to perform. Should return true if the operation succeeded, otherwise false.</param>
    /// <returns>True if the operation succeed, otherwise false</returns>
    public static bool Retry(int timeoutInMilliseconds, int pauseBetweenTriesInMilliseconds, ILogger logger, Func<bool> op)
    {
        if (timeoutInMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutInMilliseconds));
        }
        if (pauseBetweenTriesInMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pauseBetweenTriesInMilliseconds));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }
        if (op == null)
        {
            throw new ArgumentNullException(nameof(op));
        }

        logger.LogDebug(Resources.MSG_BeginningRetry, timeoutInMilliseconds, pauseBetweenTriesInMilliseconds);

        var timer = Stopwatch.StartNew();
        var succeeded = op();

        while (!succeeded && timer.ElapsedMilliseconds < timeoutInMilliseconds)
        {
            logger.LogDebug(Resources.MSG_RetryingOperation);
            System.Threading.Thread.Sleep(pauseBetweenTriesInMilliseconds);
            succeeded = op();
        }

        timer.Stop();

        if (succeeded)
        {
            logger.LogDebug(Resources.MSG_RetryOperationSucceeded, timer.ElapsedMilliseconds);
        }
        else
        {
            logger.LogDebug(Resources.MSG_RetryOperationFailed, timer.ElapsedMilliseconds);
        }
        return succeeded;
    }

    /// <summary>
    /// Ensures that the specified directory exists
    /// </summary>
    public static void EnsureDirectoryExists(string directory, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentNullException(nameof(directory));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        if (Directory.Exists(directory))
        {
            logger.LogDebug(Resources.MSG_DirectoryAlreadyExists, directory);
        }
        else
        {
            logger.LogDebug(Resources.MSG_CreatingDirectory, directory);
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Ensures that the specified directory exists and is empty
    /// </summary>
    public static void EnsureEmptyDirectory(string directory, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentNullException(nameof(directory));
        }
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        if (Directory.Exists(directory))
        {
            logger.LogDebug(Resources.MSG_DeletingDirectory, directory);
            Directory.Delete(directory, true);
        }
        logger.LogDebug(Resources.MSG_CreatingDirectory, directory);
        Directory.CreateDirectory(directory);
    }

    /// <summary>
    /// Attempts to ensure the specified empty directories exist.
    /// Handles the common types of failure and logs a more helpful error message.
    /// </summary>
    public static bool TryEnsureEmptyDirectories(ILogger logger, params string[] directories)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }
        Debug.Assert(directories.Length > 0);

        foreach (var directory in directories)
        {
            try
            {
                EnsureEmptyDirectory(directory, logger);
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException || ex is IOException)
                {
                    logger.LogError(Resources.ERROR_CannotCreateEmptyDirectory, directory, ex.Message);
                    return false;
                }
                throw;
            }
        }
        return true;
    }

    public static bool IsSecuredServerProperty(string s)
    {
        return s.EndsWith(".secured", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Common logic for handling web exceptions when connecting to the SonarQube server. Common exceptions
    /// are handled by logging user friendly errors.
    /// </summary>
    /// <returns>True if the exception was handled</returns>
    //TODO: change this to reflect new Http Exceptions.
    public static bool HandleHostUrlWebException(WebException ex, string hostUrl, ILogger logger)
    {
        var response = ex.Response as HttpWebResponse;
        if (response != null && response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogError(Resources.ERROR_FileNotFound, response.ResponseUri);
            return true;
        }

        if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogError(Resources.ERROR_UnauthorizedConnection, response.ResponseUri);
            return true;
        }

        if (ex.Status == WebExceptionStatus.NameResolutionFailure)
        {
            logger.LogError(Resources.ERROR_UrlNameResolutionFailed, hostUrl);
            return true;
        }

        if (ex.Status == WebExceptionStatus.ConnectFailure)
        {
            logger.LogError(Resources.ERROR_ConnectionFailed, hostUrl);
            return true;
        }

        if (ex.Status == WebExceptionStatus.TrustFailure)
        {
            logger.LogError(Resources.ERROR_TrustFailure, hostUrl);
            return true;
        }

        return false;
    }

    public static void LogAssemblyVersion(ILogger logger, string description)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentNullException(nameof(description));
        }

        logger.LogInfo("{0} {1}", description, ScannerVersion);
    }

    public static string ToDisplayString(this Version version)
    {
        var sb = new StringBuilder();
        sb.Append(version.Major);
        sb.Append(".");
        sb.Append(version.Minor);

        if (version.Build != 0 || version.Revision != 0)
        {
            sb.Append(".");
            sb.Append(version.Build);
        }

        if (version.Revision != 0)
        {
            sb.Append(".");
            sb.Append(version.Revision);
        }

        return sb.ToString();
    }

    #endregion Public methods
}
