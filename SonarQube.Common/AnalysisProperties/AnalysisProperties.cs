//-----------------------------------------------------------------------
// <copyright file="AnalysisProperties.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Data class to describe global analysis properties
    /// /// </summary>
    /// <remarks>The class is XML-serializable.
    /// We want the serialized representation to be a simple list of elements so the class inherits directly from the generic List</remarks>
    [XmlRoot(Namespace = XmlNamespace, ElementName = XmlElementName)]
    public class AnalysisProperties : List<Property>
    {
        public const string XmlNamespace = ProjectInfo.XmlNamespace;
        public const string XmlElementName = "SonarQubeAnalysisProperties";

        #region Serialization

        [XmlIgnore]
        public string FilePath
        {
            get; private set;
        }

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

            this.FilePath = fileName;
        }

        /// <summary>
        /// Loads and returns project info from the specified XML file
        /// </summary>
        public static AnalysisProperties Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            AnalysisProperties model = Serializer.LoadModel<AnalysisProperties>(fileName);
            model.FilePath = fileName;
            return model;
        }

        #endregion

    }
}