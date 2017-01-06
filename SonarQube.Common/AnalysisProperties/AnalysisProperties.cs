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