//-----------------------------------------------------------------------
// <copyright file="IDownloader.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Interface introduced for testability
    /// </summary>
    public interface IDownloader : IDisposable
    {
        /// <summary>
        /// Attempts to download the specified page
        /// </summary>
        /// <returns>False if the url does not exist, true if the contents were downloaded successfully.
        /// Exceptions are thrown for other web failures.</returns>
        bool TryDownloadIfExists(string url, out string contents);

        /// <summary>
        /// Attempts to download the specified file
        /// </summary>
        /// <param name="targetFilePath">The file to which the downloaded data should be saved</param>
        /// <returns>False if the url does not exist, true if the data was downloaded successfully.
        /// Exceptions are thrown for other web failures.</returns>
        bool TryDownloadFileIfExists(string url, string targetFilePath);

        string Download(string url);
    }
}
