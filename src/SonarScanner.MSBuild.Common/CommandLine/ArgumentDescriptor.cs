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
using System.Diagnostics;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Data class that describes a single valid command line argument - id, prefixes, multiplicity etc
/// </summary>
[DebuggerDisplay("{Id}")]
public class ArgumentDescriptor
{
    // https://msdn.microsoft.com/en-us/library/ms973919.aspx
    // "[d]ata that is designed to be culture-agnostic and linguistically irrelevant should...
    //  use either StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase..."
    public static readonly StringComparer IdComparer = StringComparer.Ordinal;

    public static readonly StringComparison IdComparison = StringComparison.Ordinal;

    public ArgumentDescriptor(string id, string[] prefixes, bool required, string description, bool allowMultiple)
        : this(id, prefixes, required, description, allowMultiple, false /* not a verb */)
    {
    }

    public ArgumentDescriptor(string id, string[] prefixes, bool required, string description, bool allowMultiple, bool isVerb)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(nameof(id));
        }
        if (prefixes == null || prefixes.Length == 0)
        {
            throw new ArgumentNullException(nameof(prefixes));
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentNullException(nameof(description));
        }

        Id = id;
        Prefixes = prefixes;
        Required = required;
        Description = description;
        AllowMultiple = allowMultiple;
        IsVerb = isVerb;
    }

    #region Properties

    /// <summary>
    /// The unique (internal) identifier for the argument
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Any prefixes supported for the argument. This should include all of the characters that
    /// are not to be treated as part of the value e.g. /key=
    /// </summary>
    public string[] Prefixes { get; }

    /// <summary>
    /// Whether the argument is mandatory or not
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// A short description of the argument that will be displayed to the user
    /// e.g. /key= [SonarQube project key]
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// True if the argument can be specified multiple times,
    /// false if it can be specified at most once
    /// </summary>
    public bool AllowMultiple { get; }

    /// <summary>
    /// False if the argument has a value that follows the prefix,
    /// true if the argument is just single word (e.g. "begin")
    /// </summary>
    public bool IsVerb { get; }

    #endregion Properties
}
