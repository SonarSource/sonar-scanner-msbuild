/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System.Collections.Generic;
using Microsoft.Build.Framework;

// See MSDN for an example: https://msdn.microsoft.com/en-us/library/ms171471#Anchor_5
// Example usage: msbuild x.csproj /logger:SonarScanner.MSBuild.Tasks.IntegrationTests.SimpleXmkLogger,..\SonarScanner.MSBuild.Tasks.IntegrationTests\bin\Debug\Logger.dll;log1.txt /noconsolelogger

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    public class SimpleXmlLogger : ILogger
    {
        private IEventSource eventSource;

        private BuildLog log;

        private string fileName;

        public LoggerVerbosity Verbosity { get; set; }

        public string Parameters { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            // The name of the log file should be passed as the first item in the
            // "parameters" specification in the /logger switch.  It is required
            // to pass a log file to this logger. Other loggers may have zero or more than 
            // one parameters.
            if (null == Parameters)
            {
                throw new LoggerException("Log file was not set.");
            }
            var parameters = Parameters.Split(';');

            var logFile = parameters[0];
            if (string.IsNullOrEmpty(logFile))
            {
                throw new LoggerException("Log file was not set.");
            }

            if (parameters.Length > 1)
            {
                throw new LoggerException("Too many parameters passed.");
            }

            fileName = logFile;

            this.eventSource = eventSource;
            RegisterEvents();
            log = new BuildLog();
        }

        private void RegisterEvents()
        {
            eventSource.BuildFinished += EventSource_BuildFinished;
            eventSource.BuildStarted += EventSource_BuildStarted;

            eventSource.TargetStarted += EventSource_TargetStarted;
            eventSource.TaskStarted += EventSource_TaskStarted;

            eventSource.WarningRaised += EventSource_WarningRaised;
            eventSource.ErrorRaised += EventSource_ErrorRaised;
        }

        private void UnregisterEvents()
        {
            eventSource.BuildFinished -= EventSource_BuildFinished;
            eventSource.BuildStarted -= EventSource_BuildStarted;

            eventSource.TargetStarted -= EventSource_TargetStarted;
            eventSource.TaskStarted += EventSource_TaskStarted;

            eventSource.WarningRaised -= EventSource_WarningRaised;
            eventSource.ErrorRaised -= EventSource_ErrorRaised;
        }

        private void EventSource_BuildStarted(object sender, BuildStartedEventArgs e)
        {
            foreach (KeyValuePair<string, string> kvp in e.BuildEnvironment)
            {
                log.BuildProperties.Add(new BuildProperty { Name = kvp.Key, Value = kvp.Value });
            }
        }

        private void EventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
        {
            log.BuildSucceeded = e.Succeeded;
        }

        private void EventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            log.Tasks.Add(e.TaskName);
        }

        private void EventSource_TargetStarted(object sender, TargetStartedEventArgs e)
        {
            log.Targets.Add(e.TargetName);
        }
        private void EventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            log.Errors.Add(e.Message);
        }

        private void EventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            log.Warnings.Add(e.Message);
        }

        public void Shutdown()
        {
            log.Save(fileName);
            UnregisterEvents();
            eventSource = null;
        }
    }
}
