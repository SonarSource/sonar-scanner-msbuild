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
using System.Runtime.Serialization;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Thrown when a there is an error that is well handled and should cause the process to exit in a clean way.
/// The message will be logged and the process will return exit code 1.
/// </summary>
public class AnalysisException : Exception
{
    public AnalysisException()
    {
    }

    public AnalysisException(string message)
        : base(message)
    {
    }

    public AnalysisException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    protected AnalysisException(SerializationInfo info, StreamingContext context)
    {
    }
}
