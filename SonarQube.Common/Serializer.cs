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

namespace SonarQube.Common
{
    /// <summary>
    /// Helper class to serialize objects to and from XML
    /// </summary>
    public static class Serializer
    {
        // Workaround for the file locking issue: retry after a short period.
        public static int MaxConfigRetryPeriodInMilliseconds = 2500; // Maximum time to spend trying to access the file
        public static int DelayBetweenRetriesInMilliseconds = 499; // Period to wait between retries


        #region Serialisation methods

        /// <summary>
        /// Save the object as XML
        /// </summary>
        public static void SaveModel<T>(T model, string fileName, ILogger logger)
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

            if (!Utilities.Retry(MaxConfigRetryPeriodInMilliseconds, DelayBetweenRetriesInMilliseconds, logger, () => SaveModelInternal(model, serializer, fileName, settings, logger)))
            {
                throw new InvalidOperationException(string.Format(Resources.Utilities_ErrorWritingFile, fileName));
            }
        }

        /// <summary>
        /// Loads and returns an instance of <typeparamref name="T"/> from the specified XML file
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

        /// <summary>
        /// Attempts to save the file, suppressing any IO errors that occur.
        /// This method is expected to be called inside a "retry"
        /// </summary>
        private static bool SaveModelInternal<T>(T model, XmlSerializer serializer, string fileName, XmlWriterSettings settings, ILogger logger)
        {
            try
            {
                using (XmlWriter writer = XmlWriter.Create(fileName, settings))
                {
                    serializer.Serialize(writer, model);
                }
            }
            catch (IOException e)
            {
                // Log this as a message for info. We'll log an error if all of the re-tries failed
                logger.LogMessage(Resources.Utilities_ErrorWritingFile, e.Message);
                return false;
            }
            return true;
        }
    }
}
