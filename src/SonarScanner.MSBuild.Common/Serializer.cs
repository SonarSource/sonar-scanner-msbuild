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

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace SonarScanner.MSBuild.Common;

/// <summary>
/// Helper class to serialize objects to and from XML
/// </summary>
public static class Serializer
{
    #region Public methods

    /// <summary>
    /// Save the object as XML
    /// </summary>
    public static void SaveModel<T>(T model, string fileName) where T : class
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        // Serialize to memory first to reduce the opportunity for intermittent
        // locking issues when writing the file
        MemoryStream stream = null;
        try
        {
            var containingDir = new FileInfo(fileName).Directory;
            if (!containingDir.Exists)
            {
                containingDir.Create();
            }

            stream = new MemoryStream();
            using (var writer = new StreamWriter(stream))
            {
                Write(model, writer);
                File.WriteAllBytes(fileName, stream.ToArray());
            }
        }
        finally
        {
            if (stream != null)
            {
                stream.Dispose();
            }
        }
    }

    /// <summary>
    /// Return the object as an XML string
    /// </summary>
    public static string ToString<T>(T model) where T : class
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }
        var sb = new StringBuilder();
        using (var writer = new StringWriter(sb, CultureInfo.InvariantCulture))
        {
            Write(model, writer);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Loads and returns an instance of <typeparamref name="T"/> from the specified XML file
    /// </summary>
    public static T LoadModel<T>(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }
        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException(fileName);
        }

        var ser = new XmlSerializer(typeof(T));

        object o;
        using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            o = ser.Deserialize(fs);
        }

        var model = (T)o;
        return model;
    }

    #endregion Public methods

    #region Private methods

    private static void Write<T>(T model, TextWriter writer) where T : class
    {
        Debug.Assert(model != null);
        Debug.Assert(writer != null);

        var serializer = new XmlSerializer(typeof(T));

        var settings = new XmlWriterSettings
        {
            CloseOutput = true,
            ConformanceLevel = ConformanceLevel.Document,
            Indent = true,
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
            OmitXmlDeclaration = false
        };

        using (var xmlWriter = XmlWriter.Create(writer, settings))
        {
            serializer.Serialize(xmlWriter, model);
            xmlWriter.Flush();
        }
    }

    #endregion Private methods
}
