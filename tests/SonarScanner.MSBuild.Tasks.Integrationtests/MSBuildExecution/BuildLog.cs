/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
 * mailto: info AT sonarsource DOT com
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// XML-serializable data class used to record which targets and tasks were executed during the build
    /// </summary>
    public class BuildLog
    {
        private readonly IDictionary<string, string> properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        //FIXME: make private fields where possible
        public List<BuildItem> CapturedItemValues { get; } = new List<BuildItem>();         // FIXME:Rename
        public List<string> Targets { get; } = new List<string>();
        public List<string> Tasks { get; } = new List<string>();
        /// <summary>
        /// List of messages emmited by the &lt;Message ... /&gt; task
        /// </summary>
        public List<string> Messages { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public bool BuildSucceeded { get; private set; }

        //FIXME: Reconsider if it's still needed
        // We want the normal messages to appear in the log file as a string rather than as a series of discrete messages to make them more readable.
        public string MessageLog { get; set; } // for serialization

        public BuildLog(string filePath)
        {
            var successSet = false;
            var root = BinaryLog.ReadBuild(filePath);
            root.VisitAllChildren<Build>(processBuild);
            root.VisitAllChildren<Target>(processTarget);
            root.VisitAllChildren<Task>(processTask);
            root.VisitAllChildren<Property>(x => properties[x.Name] = x.Value);

            void processBuild(Build build)
            {
                Debug.Assert(!successSet, "Build should be processed only once");
                BuildSucceeded = build.Succeeded;
                successSet = true;
            }

            void processTarget(Target target)
            {
                if (target.Succeeded)
                {
                    Targets.Add(target.Name);
                }
            }

            void processTask(Task task)
            {
                Tasks.Add(task.Name);
                if (task.Name == "Message")
                {
                    Messages.Add(task.Children.OfType<Message>().Single().Text);
                }
            }
        }

        public bool TryGetPropertyValue(string propertyName, out string value) =>
            properties.TryGetValue(propertyName, out value);

        public bool GetPropertyAsBoolean(string propertyName) =>
            // We treat a value as false if it is not set
            TryGetPropertyValue(propertyName, out string value)
            && !string.IsNullOrEmpty(value)
            && bool.Parse(value);
    }

    public class BuildKeyValue
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class BuildItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public List<BuildKeyValue> Metadata { get; set; }
    }
}
