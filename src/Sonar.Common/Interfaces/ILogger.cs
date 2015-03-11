//-----------------------------------------------------------------------
// <copyright file="ILogger.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
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
