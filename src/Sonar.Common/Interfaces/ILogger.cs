//-----------------------------------------------------------------------
// <copyright file="ILogger.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace Sonar.Common
{
    /// <summary>
    /// Simple logging interface
    /// </summary>
    public interface ILogger
    {
        void LogMessage(string message, params object[] args);
        
        void LogWarning(string message, params object[] args);
        
        void LogError(string message, params object[] args);
    }
}
