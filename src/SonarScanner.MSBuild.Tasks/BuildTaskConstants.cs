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

namespace SonarScanner.MSBuild.Tasks;

public static class BuildTaskConstants
{
    /// <summary>
    /// Item name of the analysis result item type
    /// </summary>
    public const string ResultItemName = "AnalysisResult";

    /// <summary>
    /// Name of the analysis result "id" metadata item
    /// </summary>
    public const string ResultMetadataIdProperty = "Id";

    /// <summary>
    /// Item name of the analysis setting item type
    /// </summary>
    public const string SettingItemName = "SonarQubeSetting";

    /// <summary>
    /// Name of the analysis setting "value" metadata item
    /// </summary>
    public const string SettingValueMetadataName = "Value";
}
