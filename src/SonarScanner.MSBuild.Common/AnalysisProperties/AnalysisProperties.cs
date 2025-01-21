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
using System.Linq;
using System.Xml.Serialization;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Data class to describe global analysis properties
/// /// </summary>
/// <remarks>The class is XML-serializable.
/// We want the serialized representation to be a simple list of elements so the class inherits directly from the generic List</remarks>
[XmlRoot(Namespace = XmlNamespace, ElementName = XmlElementName)]
public class AnalysisProperties : List<Property>
{
    public const string XmlNamespace = ProjectInfo.XmlNamespace;
    public const string XmlElementName = "SonarQubeAnalysisProperties";

    #region Serialization

    [XmlIgnore]
    public string FilePath
    {
        get; private set;
    }

    /// <summary>
    /// Saves the project to the specified file as XML
    /// </summary>
    public void Save(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        Serializer.SaveModel(this, fileName);

        FilePath = fileName;
    }

    /// <summary>
    /// Loads and returns project info from the specified XML file
    /// </summary>
    public static AnalysisProperties Load(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        var properties = Serializer.LoadModel<AnalysisProperties>(fileName);
        properties.FilePath = fileName;

        if (properties.Any(p => p.Id == null))
        {
            throw new System.Xml.XmlException(Resources.ERROR_InvalidPropertyName);
        }

        return properties;
    }

    #endregion Serialization
}
