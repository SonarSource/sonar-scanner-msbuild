//-----------------------------------------------------------------------
// <copyright file="Serializer.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Sonar.Common
{
    /// <summary>
    /// Helper class to serialize objects to and from XML
    /// </summary>
    internal static class Serializer
    {
        #region Serialisation methods

        /// <summary>
        /// Save the object as XML
        /// </summary>
        public static void SaveModel<T>(T model, string fileName)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            XmlSerializer serializer = new XmlSerializer(typeof(T));

            XmlWriterSettings settings = new XmlWriterSettings();

            settings.CloseOutput = true;
            settings.ConformanceLevel = ConformanceLevel.Document;
            settings.Indent = true;
            settings.NamespaceHandling = NamespaceHandling.OmitDuplicates;
            settings.OmitXmlDeclaration = false;

            using (XmlWriter writer = XmlWriter.Create(fileName, settings))
            {
                serializer.Serialize(writer, model);
            }
        }

        /// <summary>
        /// Loads and returns an instnace of <typeparamref name="T"/> from the specified XML file
        /// </summary>
        public static T LoadModel<T>(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException(fileName);
            }

            XmlSerializer ser = new XmlSerializer(typeof(T));

            object o = null;
            using (FileStream fs = File.Open(fileName, FileMode.Open, FileAccess.Read))
            {
                o = ser.Deserialize(fs);
            }

            T model = (T)o;
            return model;
        }

        #endregion

    }
}
