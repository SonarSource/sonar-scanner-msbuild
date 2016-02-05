//-----------------------------------------------------------------------
// <copyright file="Plugin.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Xml.Serialization;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    /// <summary>
    /// XML-serializable data class for a single SonarQube plugin containing an analyzer
    /// </summary>
    public class Plugin
    {
        public Plugin()
        {
        }

        public Plugin(string key, string version, string staticResourceName)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException("key");
            }
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException("version");
            }
            if (string.IsNullOrWhiteSpace(staticResourceName))
            {
                throw new ArgumentNullException("staticResourceName");
            }
            this.Key = key;
            this.Version = version;
            this.StaticResourceName = staticResourceName;
        }

        [XmlAttribute("Key")]
        public string Key { get; set; }

        [XmlAttribute("Version")]
        public string Version { get; set; }

        /// <summary>
        /// Name of the static resource in the plugin that contains the analyzer artefacts
        /// </summary>
        [XmlAttribute("StaticResourceName")]
        public string StaticResourceName { get; set; }

    }
}
