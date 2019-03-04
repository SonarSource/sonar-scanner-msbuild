/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
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
// Example usage: msbuild x.csproj /logger:SonarScanner.MSBuild.Tasks.IntegrationTests.SimpleXmlLogger,..\SonarScanner.MSBuild.Tasks.IntegrationTests\bin\Debug\Logger.dll;log1.txt /noconsolelogger

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    public class SimpleXmlLogger : ILogger
    {
        public const string CapturedDataSeparator = "___";

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
            eventSource.MessageRaised += EventSource_MessageRaised;
        }

        private void UnregisterEvents()
        {
            eventSource.BuildFinished -= EventSource_BuildFinished;
            eventSource.BuildStarted -= EventSource_BuildStarted;

            eventSource.TargetStarted -= EventSource_TargetStarted;
            eventSource.TaskStarted += EventSource_TaskStarted;

            eventSource.WarningRaised -= EventSource_WarningRaised;
            eventSource.ErrorRaised -= EventSource_ErrorRaised;
            eventSource.MessageRaised -= EventSource_MessageRaised;
        }

        private void EventSource_BuildStarted(object sender, BuildStartedEventArgs e)
        {
            foreach (KeyValuePair<string, string> kvp in e.BuildEnvironment)
            {
                log.BuildProperties.Add(new BuildKeyValue { Name = kvp.Key, Value = kvp.Value });
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

        private void EventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
            // We're not interested in all messages, just those in a specific format
            // that indicate that we should extract and store data

            // The message formats are:
            // 1. for property data
            // CAPTURE___PROPERTY___[Name]___[Value]
            // and 
            // 2. for item data
            // CAPTURE___ITEM___[Name]___[Value]
            //      ... then optionally further ___[Name]___[Value] pairs for any metadata items
            var msg = e.Message;

            if (!msg.StartsWith($"CAPTURE{CapturedDataSeparator}"))
            {
                log.LogMessage(e.Message);
                return;
            }

            var propertyData = msg.Split(new string[] { CapturedDataSeparator }, System.StringSplitOptions.None);

            if (propertyData.Length < 3)
            {
                log.Errors.Add($"Test logger error: unexpected value for captured property data message: {e.Message}. Expecting at least a three part message: CAPTURE{CapturedDataSeparator}[PROPERTY or ITEM]{CapturedDataSeparator}...");
                return;
            }

            switch (propertyData[1])
            {
                case "PROPERTY":
                    ProcessPropertyMessage(e.Message, propertyData);
                    break;
                case "ITEM":
                    ProcessItemMessage(e.Message, propertyData);
                    break;
                default:
                    log.Errors.Add($"Test logger error: unexpected value for captured data type: {propertyData[1]}. Expecting PROPERTY or ITEM");
                    break;
            }
        }

        private void ProcessPropertyMessage(string message, string[] data)
        {
            if (data.Length != 4)
            {
                log.Errors.Add($"Test logger error: unexpected value for captured property data message: {message}. Expecting a four part message: CAPTURE{CapturedDataSeparator}PROPERTY{CapturedDataSeparator}[Name]{CapturedDataSeparator}[Value]");
                return;
            }

            var capturedData = new BuildKeyValue
            {
                Name = data[2],
                Value = data[3]
            };
            log.CapturedProperties.Add(capturedData);
        }

        private void ProcessItemMessage(string message, string[] data)
        {
            if (data.Length % 2 != 0)
            {
                log.Errors.Add($"Test logger error: unexpected value for captured item data message: {message}. Expecting a four part message: CAPTURE{CapturedDataSeparator}ITEM{CapturedDataSeparator}[Name]{CapturedDataSeparator}[Value]");
                return;
            }

            var itemData = new BuildItem
            {
                Name = data[2],
                Value = data[3],
                Metadata = new List<BuildKeyValue>()
            };
            log.CapturedItemValues.Add(itemData);

            // Process any metadata items
            for (var i = 4; i < data.Length; i = i + 2)
            {
                var metadataItem = new BuildKeyValue
                {
                    Name = data[i],
                    Value = data[i + 1]
                };
                itemData.Metadata.Add(metadataItem);
            }
        }

        public void Shutdown()
        {
            log.Save(fileName);
            UnregisterEvents();
            eventSource = null;
        }
    }
}
