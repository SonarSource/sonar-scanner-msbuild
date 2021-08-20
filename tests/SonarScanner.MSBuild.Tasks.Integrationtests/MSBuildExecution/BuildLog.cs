/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2021 SonarSource SA
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

using System.Collections.Generic;
using System.Linq;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// XML-serializable data class used to record which targets and tasks were executed during the build
    /// </summary>
    public class BuildLog
    {
        public List<BuildKeyValue> BuildProperties { get; } = new List<BuildKeyValue>();
        public List<BuildKeyValue> CapturedProperties { get; } = new List<BuildKeyValue>();
        public List<BuildItem> CapturedItemValues { get; } = new List<BuildItem>();
        public List<string> Targets { get; } = new List<string>();
        public List<string> Tasks { get; } = new List<string>();
        /// <summary>
        /// List of messages emmited by the &lt;Message ... /&gt; task
        /// </summary>
        public List<string> Messages { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public bool BuildSucceeded { get; }

        #region Message logging

        private readonly StringBuilder messageLogBuilder = new StringBuilder();

        //FIXME: Reconsider if it's still needed
        // We want the normal messages to appear in the log file as a string rather than as a series of discrete messages to make them more readable.
        public string MessageLog { get; set; } // for serialization

        private BuildLog(bool buildSucceeded) =>
            BuildSucceeded = buildSucceeded;

        public string GetPropertyValue(string propertyName)
        {
            TryGetPropertyValue(propertyName, out var propertyValue);
            return propertyValue;
        }

        public bool TryGetPropertyValue(string propertyName, out string value) =>
            TryGetBuildPropertyValue(BuildProperties, propertyName, out value);

        public bool TryGetCapturedPropertyValue(string propertyName, out string value) =>
            TryGetBuildPropertyValue(CapturedProperties, propertyName, out value);

        public bool GetPropertyAsBoolean(string propertyName) =>
            // We treat a value as false if it is not set
            TryGetCapturedPropertyValue(propertyName, out string value)
            && !string.IsNullOrEmpty(value)
            && bool.Parse(value);

        public static BuildLog Load(string filePath)
        {
            //FIXME: Rewrite
            throw new System.NotImplementedException();

            //BuildLog log = null;

            //using (var streamReader = new StreamReader(filePath))
            //using (var reader = XmlReader.Create(streamReader))
            //{
            //    var serializer = new XmlSerializer(typeof(BuildLog));
            //    log = (BuildLog)serializer.Deserialize(reader);
            //}
            //log.FilePath = filePath;

            //return log;
        }

        private static bool TryGetBuildPropertyValue(IList<BuildKeyValue> properties, string propertyName, out string value)
        {
            var property = properties.FirstOrDefault(p => p.Name.Equals(propertyName, System.StringComparison.OrdinalIgnoreCase));

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
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class BuildItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public List<BuildKeyValue> Metadata { get; set; }
    }
}
