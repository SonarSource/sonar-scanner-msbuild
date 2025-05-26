﻿/*
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

using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SonarScanner.MSBuild.Tasks;

public sealed class WriteSonarTelemetry : Task
{
    private readonly IFileWrapper fileWrapper;

    [Required]
    public ITaskItem Filename { get; set; }

    public bool CreateNew { get; set; }

    public string Key { get; set; }

    public string Value { get; set; }

    public ITaskItem[] Telemetry { get; set; } = [];

    public WriteSonarTelemetry() : this(FileWrapper.Instance) { }

    public WriteSonarTelemetry(IFileWrapper instance) => fileWrapper = instance;

    public override bool Execute()
    {
        if (AllTelemetry().Select(static x => new JsonObject { new KeyValuePair<string, JsonNode>(x.Key, JsonValue.Create(x.Value)) }.ToJsonString()).ToList() is { Count: > 0 } allTelemetry)
        {
            Action<string, IEnumerable<string>, Encoding> allLinesWriter = CreateNew ? fileWrapper.CreateNewAllLines : fileWrapper.AppendAllLines;
            try
            {
                allLinesWriter(Filename.ItemSpec, allTelemetry, Encoding.UTF8);
            }
            catch (IOException ex)
            {
                if (!CreateNew) // CreateNewAllLines throws if the file already exists. This is the desired behavior and we do not want to log that exception.
                {
                    Log.LogWarningFromException(ex);
                }
            }
        }
        return true;
    }

    private IEnumerable<KeyValuePair<string, string>> AllTelemetry() =>
        Enumerable.Repeat(new KeyValuePair<string, string>(Key, Value), 1)
        .Concat(Telemetry.Select(x => new KeyValuePair<string, string>(x.ItemSpec, x.GetMetadata("Value"))))
        .Where(x => !string.IsNullOrEmpty(x.Key));
}
