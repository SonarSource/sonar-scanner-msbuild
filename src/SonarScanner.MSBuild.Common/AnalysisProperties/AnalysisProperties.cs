/*
 * SonarScanner for .NET
 * Copyright (C) SonarSource Sàrl
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

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Data class to describe global analysis properties.
/// </summary>
/// <remarks>The class is XML-serializable.
/// We want the serialized representation to be a simple list of elements so the class inherits directly from the generic List.</remarks>
[XmlRoot(Namespace = XmlNamespace, ElementName = XmlElementName)]
public class AnalysisProperties : List<Property>
{
    public const string XmlNamespace = ProjectInfo.XmlNamespace;
    public const string XmlElementName = "SonarQubeAnalysisProperties";

    [XmlIgnore]
    public string FilePath { get; private set; }

    public AnalysisProperties() { } // Serialization

    public AnalysisProperties(IEnumerable<Property> properties) =>
        AddRange(properties);

    public void Save(string fileName)
    {
        Contract.ThrowIfNullOrWhitespace(fileName, nameof(fileName));
        Serializer.SaveModel(this, fileName);
        FilePath = fileName;
    }

    public static AnalysisProperties Load(string fileName)
    {
        Contract.ThrowIfNullOrWhitespace(fileName, nameof(fileName));
        var properties = Serializer.LoadModel<AnalysisProperties>(fileName);
        properties.FilePath = fileName;
        if (properties.Any(x => x.Id is null))
        {
            throw new System.Xml.XmlException(Resources.ERROR_InvalidPropertyName);
        }
        return properties;
    }
}
