//-----------------------------------------------------------------------
// <copyright file="AnalysisConfig.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All Rights Reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class to describe the analysis settings for a single SonarQube project
    /// </summary>
    /// <remarks>The class is XML-serializable</remarks>
    [XmlRoot(Namespace = XmlNamespace)]
    public class AnalysisConfig
    {
        public const string XmlNamespace = ProjectInfo.XmlNamespace;

        public string SonarConfigDir { get; set; }

        public string SonarOutputDir { get; set; }

        #region SonarQube project properties

        public string SonarProjectKey { get; set; }

        public string SonarProjectVersion { get; set; }

        public string SonarProjectName { get; set; }

        public string SonarRunnerPropertiesPath { get; set; }

        /// <summary>
        /// List of additional analysis settings
        /// </summary>
        public List<AnalysisSetting> AdditionalSettings { get; set; }

        #endregion

        #region Serialization

        /// <summary>
        /// Saves the project to the specified file as XML
        /// </summary>
        public void Save(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            Serializer.SaveModel(this, fileName);
        }

        /// <summary>
        /// Loads and returns project info from the specified XML file
        /// </summary>
        public static AnalysisConfig Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            AnalysisConfig model = Serializer.LoadModel<AnalysisConfig>(fileName);
            return model;
        }

        #endregion

    }
}
