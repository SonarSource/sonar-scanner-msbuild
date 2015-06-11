//-----------------------------------------------------------------------
// <copyright file="IBootstrapperSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.Bootstrapper
{
    /// <summary>
    /// Returns the settings required by the bootstrapper
    /// </summary>
    public interface IBootstrapperSettings
    {
        string SonarQubeUrl { get; }

        /// <summary>
        /// Temporary analysis directory
        /// </summary>
        string TempDirectory { get; }

        /// <summary>
        /// Directory into which the downloaded files should be placed
        /// </summary>
        string DownloadDirectory { get; }

        /// <summary>
        /// Full path to the pre-processor to be executed
        /// </summary>
        string PreProcessorFilePath { get; }

        /// <summary>
        /// Full path to the post-processor to be executed
        /// </summary>
        string PostProcessorFilePath { get; }

        /// <summary>
        /// Timeout for the pre-processor execution
        /// </summary>
        int PreProcessorTimeoutInMs { get; }

        /// <summary>
        /// Timeout for the post-processor execution
        /// </summary>
        int PostProcessorTimeoutInMs { get; }
    }
}
