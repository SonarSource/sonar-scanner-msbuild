//-----------------------------------------------------------------------
// <copyright file="IRoslynV1SarifFixer.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;

namespace SonarRunner.Shim
{
    public interface IRoslynV1SarifFixer
    {
        /// <summary>
        /// Attempts to load and fix a SARIF file emitted by Roslyn 1.0 (VS 2015 RTM).
        /// Returns a string representing a path to a valid JSON file suitable for upload to server,
        /// or null if this is not possible.
        /// </summary>
        string LoadAndFixFile(string sarifFilePath, ILogger logger);
    }
}