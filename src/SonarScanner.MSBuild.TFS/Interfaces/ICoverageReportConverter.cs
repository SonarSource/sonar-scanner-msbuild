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

namespace SonarScanner.MSBuild.TFS;

public interface ICoverageReportConverter
{
    /// <summary>
    /// Converts the supplied binary code coverage report file to XML
    /// </summary>
    /// <param name="inputFilePath">The full path to the binary file to be converted</param>
    /// <param name="outputFilePath">The name of the XML file to be created</param>
    /// <returns>True if the conversion was successful, otherwise false</returns>
    bool ConvertToXml(string inputFilePath, string outputFilePath);
}
