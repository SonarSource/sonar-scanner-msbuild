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

using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SonarScanner.MSBuild.Tasks;

public sealed class WriteTelemetryFactory : ITaskFactory
{
    private string filename;

    public WriteTelemetryFactory()
    {
        Debugger.Launch();
    }

    public string FactoryName => nameof(WriteTelemetryFactory);

    public Type TaskType => typeof(WriteTelemetry);

    public void CleanupTask(ITask task) { }

    public ITask CreateTask(IBuildEngine taskFactoryLoggingHost) =>
        new WriteTelemetry(filename);

    public TaskPropertyInfo[] GetTaskParameters() =>
        [
            new(nameof(WriteTelemetry.Filename), typeof(ITaskItem), output: false, required: false),
            new(nameof(WriteTelemetry.Key), typeof(string), output: false, required: true),
            new(nameof(WriteTelemetry.Value), typeof(string), output: false, required: true),
        ];

    public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost)
    {
        filename = taskBody?.Trim();
        return true;
    }
}

public sealed class WriteTelemetry : Task
{
    public WriteTelemetry(string defaultFilename)
    {
        Filename = new TaskItem(defaultFilename);
    }

    public ITaskItem Filename { get; set; }

    public string Key { get; set; }
    public string Value { get; set; }

    public override bool Execute()
    {
        Debugger.Launch();
        File.AppendAllLines(Filename.ItemSpec, new[] { $"{Key}={Value}" }, Encoding.UTF8);
        return true;
    }
}
