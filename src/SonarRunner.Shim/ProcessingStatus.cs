//-----------------------------------------------------------------------
// <copyright file="ProcessingStatus.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarRunner.Shim
{
    /// <summary>
    /// Enumeration listing processing status codes that indicate whether a project
    /// can be analyzed or not
    /// </summary>
    public enum ProcessingStatus
    {
        Valid,
        InvalidGuid,
        DuplicateGuid,
        ExcludeFlagSet,
        NoFilesToAnalyze
    }
}
