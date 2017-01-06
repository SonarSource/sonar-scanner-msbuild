/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace SonarQube.Common
{
    /// <summary>
    /// Helper class to serialize objects to and from XML
    /// </summary>
    public static class Serializer
    {
        #region Public methods

        /// <summary>
        /// Save the object as XML
        /// </summary>
        public static void SaveModel<T>(T model, string fileName) where T : class
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            // Serialize to memory first to reduce the opportunity for intermittent
            // locking issues when writing the file
            using (MemoryStream stream = new MemoryStream())
            using (StreamWriter writer = new StreamWriter(stream))
            {
                Write(model, writer);
                File.WriteAllBytes(fileName, stream.ToArray());
            }
        }

        /// <summary>
        /// Return the object as an XML string
        /// </summary>
        public static string ToString<T>(T model) where T : class
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }
            StringBuilder sb = new StringBuilder();
            using (StringWriter writer = new StringWriter(sb, System.Globalization.CultureInfo.InvariantCulture))
            {
                Write(model, writer);
            }
            return sb.ToString();
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

            object o;
            using (FileStream fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                o = ser.Deserialize(fs);
            }

            T model = (T)o;
            return model;
        }

        #endregion

        #region Private methods

        private static void Write<T>(T model, TextWriter writer) where T : class
        {
            Debug.Assert(model != null);
            Debug.Assert(writer != null);

            XmlSerializer serializer = new XmlSerializer(typeof(T));

            XmlWriterSettings settings = new XmlWriterSettings();

            settings.CloseOutput = true;
            settings.ConformanceLevel = ConformanceLevel.Document;
            settings.Indent = true;
            settings.NamespaceHandling = NamespaceHandling.OmitDuplicates;
            settings.OmitXmlDeclaration = false;

            using (XmlWriter xmlWriter = XmlWriter.Create(writer, settings))
            {
                serializer.Serialize(xmlWriter, model);
                xmlWriter.Flush();
            }
        }

        #endregion
    }
}
