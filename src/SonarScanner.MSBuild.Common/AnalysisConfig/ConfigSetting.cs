/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2024 SonarSource SA
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
using System.Xml.Serialization;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Data class to describe an additional analysis configuration setting
/// /// </summary>
/// <remarks>The class is XML-serializable</remarks>
public class ConfigSetting
{
    #region Data

    /// <summary>
    /// The identifier for the setting
    /// </summary>
    [XmlAttribute]
    public string Id { get; set; }

    /// <summary>
    /// The value of the setting
    /// </summary>
    [XmlAttribute]
    public string Value { get; set; }

    #endregion Data

    #region Static helper methods

    /// <summary>
    /// Comparer to use when comparing keys of analysis settings
    /// </summary>
    public static readonly IEqualityComparer<string> SettingKeyComparer = StringComparer.Ordinal;

    /// <summary>
    /// Comparer to use when comparing keys of analysis settings
    /// </summary>
    public static readonly IEqualityComparer<string> SettingValueComparer = StringComparer.Ordinal;

    #endregion Static helper methods
}
