/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

namespace TestUtilities;

/// <summary>
/// An exception thrown by <see cref="JavaPropertyReader"/> when parsing
/// a properties stream.
/// </summary>
/// Copied from https://github.com/Kajabity/Kajabity-Tools
public class ParseException : System.Exception
{
    /// <summary>
    /// Construct an exception with an error message.
    /// </summary>
    /// <param name="message">A descriptive message for the exception</param>
    public ParseException(string message) : base(message)
    {
    }
}
