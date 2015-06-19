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
    /// <summary>
    /// Data class that stores a list of version numbers representing the supported bootstrapper versions 
    /// from an API perspective (i.e. not assembly versions)
    /// </summary>
    [XmlRoot(Namespace = XmlNamespace)]
    public class BootstrapperSupportedVersions
    {
        public const string XmlNamespace = "http://www.sonarsource.com/msbuild/integration/2015/1";


        public BootstrapperSupportedVersions()
        {
            this.Versions = new List<string>();
        }


        #region Public Properties

        [XmlElement("SupportedApiVersion")]
        public List<string> Versions { get; private set; }

        #endregion


        #region Serialization

        /// <summary>
        /// Saves the versions to the specified file as XML
        /// </summary>
        public /* for testing purposes */ void Save(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            Serializer.SaveModel(this, fileName);
        }


        /// <summary>
        /// Loads and returns the versions from the specified XML file
        /// </summary>
        public static BootstrapperSupportedVersions Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            BootstrapperSupportedVersions model = Serializer.LoadModel<BootstrapperSupportedVersions>(fileName);
            return model;
        }
    }

    #endregion
}
