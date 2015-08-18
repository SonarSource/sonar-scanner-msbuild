//-----------------------------------------------------------------------
// <copyright file="ConfigSetting.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class to describe an additional analysis configuration setting
    /// /// </summary>
    /// <remarks>The class is XML-serializable</remarks>
    public class ConfigSetting
    {
        #region Data

        /// <summary>
        /// The identifier for the setting
        /// </summary>
        [XmlAttribute]
        public string Id { get; set; }

        /// <summary>
        /// The value of the setting
        /// </summary>
        [XmlAttribute]
        public string Value { get; set; }

        #endregion

        #region Static helper methods

        /// <summary>
        /// Comparer to use when comparing keys of analysis settings
        /// </summary>
        public static readonly IEqualityComparer<string> SettingKeyComparer = StringComparer.Ordinal;

        /// <summary>
        /// Comparer to use when comparing keys of analysis settings
        /// </summary>
        public static readonly IEqualityComparer<string> SettingValueComparer = StringComparer.Ordinal;

        #endregion
    }
}
