/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
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

namespace SonarScanner.MSBuild.Tasks.IntegrationTest;

/// <summary>
/// XML-serializable data class used to record which targets and tasks were executed during the build.
/// </summary>
public class BuildLog
{
    private readonly IDictionary<string, string> properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, List<BuildItem>> items = new Dictionary<string, List<BuildItem>>(StringComparer.OrdinalIgnoreCase);
    private readonly ISet<string> tasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public List<string> Targets { get; } = new List<string>();
    public List<string> Messages { get; } = new List<string>();
    public List<string> Warnings { get; } = new List<string>();
    public List<string> Errors { get; } = new List<string>();
    public bool BuildSucceeded { get; private set; }

    public BuildLog(string filePath)
    {
        var successSet = false;
        var root = BinaryLog.ReadBuild(filePath);
        root.VisitAllChildren<Build>(ProcessBuild);
        root.VisitAllChildren<Target>(ProcessTarget);
        root.VisitAllChildren<Task>(x => tasks.Add(x.Name));
        root.VisitAllChildren<Message>(x => Messages.Add(x.Text));
        root.VisitAllChildren<Warning>(x => Warnings.Add(x.Text));
        root.VisitAllChildren<Error>(x => Errors.Add(x.Text));
        root.VisitAllChildren<Property>(x => properties[x.Name] = x.Value);
        root.VisitAllChildren<NamedNode>(ProcessNamedNode);

        void ProcessBuild(Build build)
        {
            Debug.Assert(!successSet, "Build should be processed only once");
            BuildSucceeded = build.Succeeded;
            successSet = true;
        }

        void ProcessTarget(Target target)
        {
            if (target.Id >= 0) // If our target fails with error, we still want to register it. Skipped have log Id = -1
            {
                Targets.Add(target.Name);
            }
        }

        void ProcessNamedNode(NamedNode node)
        {
            if (node is AddItem addItem)
            {
                if (!items.ContainsKey(addItem.Name))
                {
                    items.Add(addItem.Name, new List<BuildItem>());
                }
                items[addItem.Name].AddRange(addItem.Children.OfType<Item>().Select(x => new BuildItem(x)));
            }
            else if (node is RemoveItem removeItem && items.TryGetValue(removeItem.Name, out var list))
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

    public bool ContainsTask(string taskName) =>
        tasks.Contains(taskName);

    public bool TryGetPropertyValue(string propertyName, out string value) =>
        properties.TryGetValue(propertyName, out value);

    public bool GetPropertyAsBoolean(string propertyName) =>
        // We treat a value as false if it is not set
        TryGetPropertyValue(propertyName, out var value)
        && !string.IsNullOrEmpty(value)
        && bool.Parse(value);

    public IEnumerable<BuildItem> GetItem(string itemName) =>
        items.TryGetValue(itemName, out var values) ? values : Enumerable.Empty<BuildItem>();
}

public class BuildItem
{
    public string Text { get; }
    public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public BuildItem(Item item)
    {
        Text = item.Text;
        foreach (var metadata in item.Children.OfType<Metadata>())
        {
            Metadata[metadata.Name] = metadata.Value;
        }
    }

    public override string ToString() => Text;
}
