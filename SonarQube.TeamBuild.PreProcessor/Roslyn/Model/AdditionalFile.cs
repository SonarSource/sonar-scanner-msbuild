//-----------------------------------------------------------------------
// <copyright file="AdditionalFile.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Xml;
using System.Xml.Serialization;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    /// <summary>
    /// XML-serializable data class for a single analyzer AdditionalFile
    /// </summary>
    public class AdditionalFile
    {
        [XmlAttribute]
        /// <summary>
        /// The name of the file the content should be saved to
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The content of the file
        /// </summary>
        [XmlAnyElement]
        public XmlElement Content { get; set; }
    }
}