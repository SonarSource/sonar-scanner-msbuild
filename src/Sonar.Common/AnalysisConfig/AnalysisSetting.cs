//-----------------------------------------------------------------------
// <copyright file="AnalysisSetting.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Xml.Serialization;

namespace Sonar.Common
{
    /// <summary>
    /// Data class to describe an additional analysis configuration setting
    /// /// </summary>
    /// <remarks>The class is XML-serializable.
    public class AnalysisSetting
    {
        /// <summary>
        /// The identifier for the setting
        /// </summary>
        /// <remarks>Each type </remarks>
        [XmlAttribute]
        public string Id { get; set; }

        /// <summary>
        /// The value of the setting
        /// </summary>
        [XmlAttribute]
        public string Value { get; set; }
    }
}
