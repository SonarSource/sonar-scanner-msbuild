/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
 * mailto:info AT sonarsource DOT com
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// XML-serializable data class used to record which targets and tasks
    /// were executed during the build
    /// </summary>
    public class BuildLog
    {
        public List<BuildKeyValue> BuildProperties { get; set; } = new List<BuildKeyValue>();

        public List<BuildKeyValue> CapturedProperties { get; set; } = new List<BuildKeyValue>();

        public List<BuildItem> CapturedItemValues { get; set; } = new List<BuildItem>();

        public List<string> Targets { get; set; } = new List<string>();

        public List<string> Tasks { get; set; } = new List<string>();

        public List<string> Warnings { get; set; } = new List<string>();

        public List<string> Errors { get; set; } = new List<string>();

        public bool BuildSucceeded { get; set; }

        #region Message logging

        private readonly StringBuilder messageLogBuilder = new StringBuilder();

        // We want the normal messages to appear in the log file as a string rather than as a series
        // of discrete messages to make them more readable.
        public string MessageLog { get; set; } // for serialization

        public void LogMessage(string message)
        {
            messageLogBuilder.AppendLine(message);
        }

        #endregion

        public string GetPropertyValue(string propertyName)
        {
            TryGetPropertyValue(propertyName, out var propertyValue);
            return propertyValue;
        }

        public bool TryGetPropertyValue(string propertyName, out string value) =>
            TryGetBuildPropertyValue(BuildProperties, propertyName, out value);

        public bool TryGetCapturedPropertyValue(string propertyName, out string value) =>
            TryGetBuildPropertyValue(CapturedProperties, propertyName, out value);

        public bool GetPropertyAsBoolean(string propertyName)
        {
            // We treat a value as false if it is not set
            if (TryGetCapturedPropertyValue(propertyName, out string value))
            {
                return (string.IsNullOrEmpty(value)) ? false : bool.Parse(value);
            }
            return false;
        }

        [XmlIgnore]
        public string FilePath { get; private set; }

        public void Save(string filePath)
        {
            MessageLog = messageLogBuilder.ToString();
            SerializeObjectToFile(filePath, this);
            FilePath = filePath;
        }

        public static BuildLog Load(string filePath)
        {
            BuildLog log = null;

            using (var streamReader = new StreamReader(filePath))
            using (var reader = XmlReader.Create(streamReader))
            {
                var serializer = new XmlSerializer(typeof(BuildLog));
                log = (BuildLog)serializer.Deserialize(reader);
            }
            log.FilePath = filePath;

            return log;
        }

        private static void SerializeObjectToFile(string filePath, object objectToSerialize)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                IndentChars = "  "
            };

            using (var stream = new MemoryStream())
            using (var writer = XmlWriter.Create(stream, settings))
            {
                var serializer = new XmlSerializer(objectToSerialize.GetType());
                serializer.Serialize(writer, objectToSerialize, new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty }));
                var xml = Encoding.UTF8.GetString(stream.ToArray());
                File.WriteAllText(filePath, xml);
            }
        }

        private static bool TryGetBuildPropertyValue(IList<BuildKeyValue> properties, string propertyName, out string value)
        {
            var property = properties.FirstOrDefault(
                p => p.Name.Equals(propertyName, System.StringComparison.OrdinalIgnoreCase));

            if (property == null)
            {
                value = null;
                return false;
            }

            value = property.Value;
            return true;
        }
    }

    public class BuildKeyValue
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Value { get; set; }
    }

    public class BuildItem
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Value { get; set; }

        public List<BuildKeyValue> Metadata { get; set; }
    }
}
