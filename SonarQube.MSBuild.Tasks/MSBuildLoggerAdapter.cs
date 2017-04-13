/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

        void ILogger.SuspendOutput()
        {
            // no-op
        }

        void ILogger.ResumeOutput()
        {
            // no-op
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
