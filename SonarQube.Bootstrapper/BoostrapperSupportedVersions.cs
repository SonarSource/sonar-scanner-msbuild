//-----------------------------------------------------------------------
// <copyright file="BoostrapperSupportedVersions.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SonarQube.Bootstrapper
{
    [XmlRoot(Namespace = XmlNamespace)]
    public class BoostrapperSupportedVersions
    {
        public const string XmlNamespace = "http://www.sonarsource.com/msbuild/integration/2015/1";


        public BoostrapperSupportedVersions()
        {
            this.Versions = new List<string>();
        }


        #region Public Properties

        [XmlElement("SupportedVersion")]
        public List<string> Versions { get; private set; }

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
        public static BoostrapperSupportedVersions Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            BoostrapperSupportedVersions model = Serializer.LoadModel<BoostrapperSupportedVersions>(fileName);
            return model;
        }
    }

    #endregion
}
