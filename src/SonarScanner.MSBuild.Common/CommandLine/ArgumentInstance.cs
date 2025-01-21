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

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Data class for an instance of an argument
/// </summary>
[DebuggerDisplay("{Descriptor.Id}={Value}")]
public class ArgumentInstance
{
    public ArgumentInstance(ArgumentDescriptor descriptor, string value)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Value = value;
    }

    #region Data

    public ArgumentDescriptor Descriptor { get; }

    public string Value { get; }

    #endregion Data

    #region Static methods

    public static bool TryGetArgument(string id, IEnumerable<ArgumentInstance> arguments, out ArgumentInstance instance)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(nameof(id));
        }
        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        instance = arguments.FirstOrDefault(a => ArgumentDescriptor.IdComparer.Equals(a.Descriptor.Id, id));
        return instance != null;
    }

    public static bool TryGetArgumentValue(string id, IEnumerable<ArgumentInstance> arguments, out string value)
    {
        if (TryGetArgument(id, arguments, out var instance))
        {
            value = instance.Value;
        }
        else
        {
            value = null;
        }

        return instance != null;
    }

    #endregion Static methods
}
