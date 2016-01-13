//-----------------------------------------------------------------------
// <copyright file="RoslynExportProfile.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using SonarQube.Common;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    /// <summary>
    /// XML-serializable data class for Roslyn export profile information
    /// </summary>
    [XmlRoot]
    public class RoslynExportProfile
    {
        [XmlAttribute]
        public string Version { get; set; }

        public Deployment Deployment { get; set; }

        public Configuration Configuration { get; set; }

        #region Serialization

        [XmlIgnore]
        public string FileName { get; private set; }

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
            this.FileName = fileName;
        }

        /// <summary>
        /// Loads and returns project info from the specified XML file
        /// </summary>
        public static RoslynExportProfile Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            RoslynExportProfile model = Serializer.LoadModel<RoslynExportProfile>(fileName);
            model.FileName = fileName;
            return model;
        }

        public static RoslynExportProfile Load(TextReader reader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(RoslynExportProfile));
            RoslynExportProfile profile = serializer.Deserialize(reader) as RoslynExportProfile;
            return profile;
        }

        #endregion
    }

}
