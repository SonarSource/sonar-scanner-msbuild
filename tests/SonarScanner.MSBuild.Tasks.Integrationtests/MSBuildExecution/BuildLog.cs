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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// XML-serializable data class used to record which targets and tasks were executed during the build
    /// </summary>
    public class BuildLog
    {
        private readonly IDictionary<string, string> properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly IDictionary<string, List<BuildItem>> items = new Dictionary<string, List<BuildItem>>(StringComparer.OrdinalIgnoreCase);

        //FIXME: make private fields where possible
        public List<string> Targets { get; } = new List<string>();
        public List<string> Tasks { get; } = new List<string>();
        /// <summary>
        /// List of messages emmited by the &lt;Message ... /&gt; task
        /// </summary>
        public List<string> Messages { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public bool BuildSucceeded { get; private set; }

        public BuildLog(string filePath)
        {
            var successSet = false;
            var root = BinaryLog.ReadBuild(filePath);
            root.VisitAllChildren<Build>(processBuild);
            root.VisitAllChildren<Target>(processTarget);
            root.VisitAllChildren<Task>(processTask);
            root.VisitAllChildren<Warning>(x => Warnings.Add(x.Text));
            root.VisitAllChildren<Error>(x => Errors.Add(x.Text));
            root.VisitAllChildren<Property>(x => properties[x.Name] = x.Value);
            root.VisitAllChildren<AddItem>(processAddItem);
            root.VisitAllChildren<RemoveItem>(processRemoveItem);

            void processBuild(Build build)
            {
                Debug.Assert(!successSet, "Build should be processed only once");
                BuildSucceeded = build.Succeeded;
                successSet = true;
            }

            void processTarget(Target target)
            {
                if (target.Id >= 0) // If our target fails with error, we still want to register it. Skipped have log Id = -1
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

            void processAddItem(AddItem addItem)
            {
                if (!items.ContainsKey(addItem.Name))
                {
                    items.Add(addItem.Name, new List<BuildItem>());
                }
                items[addItem.Name].AddRange(addItem.Children.OfType<Item>().Select(x => new BuildItem(x)));
            }

            void processRemoveItem(RemoveItem removeItem)
            {
                if (items.TryGetValue(removeItem.Name, out var list))
                {
                    foreach (var item in removeItem.Children.OfType<Item>())
                    {
                        var index = FindIndex(item);
                        while (index >= 0)
                        {
                            list.RemoveAt(index);
                            index = FindIndex(item);
                        }
                    }

                    int FindIndex(Item item) =>
                        list.FindIndex(x => x.Text.Equals(item.Text, StringComparison.OrdinalIgnoreCase));
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

        public IEnumerable<BuildItem> GetItem(string itemName)
        {
            if (items.TryGetValue(itemName, out var values))
            {
                return values;
            }
            else
            {
                throw new AssertFailedException("Test logger error: Failed to find expected item: " + itemName);
            }
        }
    }

    public class BuildItem
    {
        public string Text { get; }
        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public BuildItem(Item item)
        {
            Text = item.Text;
            foreach(var metadata in item.Children.OfType<Metadata>())
            {
                Metadata[metadata.Name] = metadata.Value;
            }
        }
    }
}
