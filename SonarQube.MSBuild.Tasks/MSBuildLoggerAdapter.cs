/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
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
