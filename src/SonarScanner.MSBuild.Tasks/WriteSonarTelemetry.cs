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
        if (AllTelemetry().Select(static x =>
            $$"""
            {{{HttpUtility.JavaScriptStringEncode(x.Key)}}:{{HttpUtility.JavaScriptStringEncode(x.Value)}}}
            """).ToList() is { Count: > 0 } allTelemetry)
        {
            Action<string, IEnumerable<string>, Encoding> allLinesWriter = CreateNew ? fileWrapper.CreateNewAllLines : fileWrapper.AppendAllLines;
            try
            {
                allLinesWriter(Filename.ItemSpec, allTelemetry, Encoding.UTF8);
            }
            catch (IOException ex) // For CreateNew, this exception is thrown, when the file already exists, with:
                                   // * ex.HResult == 0x80070050 on Windows. This corresponds to ERROR_FILE_EXISTS (0x50) from WinError.h wrapped in an HResult.
                                   // * ex.HResult == 0x00000011 on Umbuntu. This corresponds EEXIST.
                                   // EEXIST is tpyically 0x00000011 but the concrete number is not defined by POSIX.
                                   // We do not want to depend on the concrete number returned by HResult as it seems not be very stable.
                                   // We assume that the IOException is ERROR_FILE_EXISTS for the CreateNew case regardless the HResult.
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
