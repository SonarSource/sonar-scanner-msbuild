//-----------------------------------------------------------------------
// <copyright file="Plugin.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Xml.Serialization;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    /// <summary>
    /// XML-serializable data class for a single SonarQube plugin containing an analyzer
    /// </summary>
    public class Plugin
    {
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
