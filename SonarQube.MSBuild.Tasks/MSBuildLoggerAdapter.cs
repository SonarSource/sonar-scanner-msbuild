//-----------------------------------------------------------------------
// <copyright file="MSBuildLoggerAdapter.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Utilities;
using SonarQube.Common;
using System;

namespace SonarQube.MSBuild.Tasks
{
    /// <summary>
    /// Adapter that converts between the SonarQube and MSBuild logging interfaces
    /// </summary>
    internal class MSBuildLoggerAdapter : ILogger
    {
        private readonly TaskLoggingHelper msBuildLogger;

        public MSBuildLoggerAdapter(TaskLoggingHelper msBuildLogger)
        {
            if (msBuildLogger == null)
            {
                throw new ArgumentNullException("msBuildLogger");
            }
            this.msBuildLogger = msBuildLogger;
        }

        #region SonarQube.Common.ILogger methods

        bool ILogger.IncludeTimestamp
        {
            get; set;
        }

        LoggerVerbosity ILogger.Verbosity
        {
            get; set;
        }

        void ILogger.LogDebug(string message, params object[] args)
        {
            this.LogMessage(Common.LoggerVerbosity.Debug, message, args);
        }

        void ILogger.LogError(string message, params object[] args)
        {
            this.msBuildLogger.LogError(message, args);
        }

        void ILogger.LogInfo(string message, params object[] args)
        {
            this.LogMessage(Common.LoggerVerbosity.Info, message, args);
        }

        void ILogger.LogWarning(string message, params object[] args)
        {
            this.msBuildLogger.LogWarning(message, args);
        }

        #endregion

        #region Private methods

        private void LogMessage(Common.LoggerVerbosity verbosity, string message, params object[] args)
        {
            // We need to adapt between the ILogger verbosity and the MsBuild logger verbosity
            if (verbosity == Common.LoggerVerbosity.Info)
            {
                this.msBuildLogger.LogMessage(Microsoft.Build.Framework.MessageImportance.Normal, message, args);
            }
            else
            {
                this.msBuildLogger.LogMessage(Microsoft.Build.Framework.MessageImportance.Low, message, args);
            }
        }

        #endregion Private methods
    }
}
