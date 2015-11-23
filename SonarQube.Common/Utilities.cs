//-----------------------------------------------------------------------
// <copyright file="Utilities.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SonarQube.Common
{
    public static class Utilities
    {
        
        #region Public methods

        /// <summary>
        /// Retries the specified operation until the specified timeout period expires
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="op">The operation to perform. Should return true if the operation succeeded, otherwise false.</param>
        /// <returns>True if the operation succeed, otherwise false</returns>
        public static bool Retry(int timeoutInMilliseconds, int pauseBetweenTriesInMilliseconds, ILogger logger, Func<bool> op)
        {
            if(timeoutInMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException("timeoutInMilliseconds");
            }
            if (pauseBetweenTriesInMilliseconds < 1)
            {
                throw new ArgumentOutOfRangeException("pauseBetweenTriesInMilliseconds");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (op == null)
            {
                throw new ArgumentNullException("op");
            }
            
            logger.LogDebug(Resources.MSG_BeginningRetry, timeoutInMilliseconds, pauseBetweenTriesInMilliseconds);

            Stopwatch timer = Stopwatch.StartNew();
            bool succeeded = op();

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
                throw new ArgumentNullException("directory");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
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
                throw new ArgumentNullException("directory");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
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
                throw new ArgumentNullException("logger");
            }
            Debug.Assert(directories.Length > 0);

            foreach (string directory in directories)
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

        /// <summary>
        /// Common logic for handling web exceptions when connecting to the SonarQube server. Common exceptions 
        /// are handled by logging user friendly errors.
        /// </summary>
        /// <returns>True if the exception was handled</returns>
        public static bool HandleHostUrlWebException(WebException ex, string hostUrl, ILogger logger)
        {
            var response = ex.Response as HttpWebResponse;
            if (response != null && response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogError(Resources.ERROR_FileNotFound, response.ResponseUri);
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

        public static void LogAssemblyVersion(ILogger logger, System.Reflection.Assembly assembly, string description)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (assembly == null)
            {
                throw new ArgumentNullException("assembly");
            }
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentNullException("description");
            }

            logger.LogInfo("{0} {1}", description, assembly.GetName().Version);
        }

        /// <summary>
        /// Disposes the supplied object if it can be disposed. Null objects are ignored.
        /// </summary>
        public static void SafeDispose(object instance)
        {
            if (instance == null)
            {
                IDisposable disposable = instance as IDisposable;
                if (instance != null)
                {
                    disposable.Dispose();
                }
            }
        }

        /// <summary>
        /// Returns true if the given SARIF came from the VS 2015 RTM Roslyn, which does not provide correct output.
        /// </summary>
        public static bool IsSarifFromUnsupportedCompiler(string input)
        {
            // low risk of false positives / false negatives
            if (input.Contains(@"""toolName"": ""Microsoft (R) Visual C# Compiler""")
                && input.Contains(@"""productVersion"": ""1.0.0"""))
            {
                return true;
            }

            // all other cases
            return false;
        }
        
        /// <summary>
        /// Used to correct invalid SARIF generated by VS 2015 RTM Roslyn. 
        /// Replaces all slashes in any line beginning with "uri": with double slashes.
        /// Applying this to already-valid SARIF will result in over-escaping.
        /// </summary>
        public static bool FixImproperlyEscapedSarif(string input, out string output)
        {
            string[] inputLines = input.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            bool changeApplied = false;

            if (IsSarifFromUnsupportedCompiler(input))
            {
                for (int i = 0; i < inputLines.Length; i++)
                {
                    string line = inputLines[i];
                    if (Regex.IsMatch(line, @"""uri"":.*\\")) // if line contains "uri": and also contains a \ char
                    {
                        line = Regex.Replace(line, @"\\", @"\\");
                        inputLines[i] = line;
                        changeApplied = true;
                    }
                }
            }

            if (changeApplied)
            {
                output = string.Join(Environment.NewLine, inputLines);
            }
            else
            {
                output = input;
            }

            return changeApplied;
        }
        
        #endregion

    }
}
