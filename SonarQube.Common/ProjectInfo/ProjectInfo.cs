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
    /// Data class to describe a single project
    /// </summary>
    /// <remarks>The class is XML-serializable</remarks>
    [XmlRoot(Namespace = XmlNamespace)]
    public class ProjectInfo
    {
        public const string XmlNamespace = "http://www.sonarsource.com/msbuild/integration/2015/1";

        #region Public properties

        /// <summary>
        /// The project file name
        /// </summary>
        public string ProjectName
        {
            get;
            set;
        }

        /// <summary>
        /// The project language
        /// </summary>
        public string ProjectLanguage
        {
            get;
            set;
        }

        /// <summary>
        /// The kind of the project
        /// </summary>
        public ProjectType ProjectType
        {
            get;
            set;
        }

        /// <summary>
        /// Unique identifier for the project
        /// </summary>
        public Guid ProjectGuid
        {
            get;
            set;
        }

        /// <summary>
        /// The full name and path of the project file
        /// </summary>
        public string FullPath
        {
            get;
            set;
        }

        /// <summary>
        /// Flag indicating whether the project should be excluded from processing
        /// </summary>
        public bool IsExcluded
        {
            get;
            set;
        }

        /// <summary>
        /// Encoding used for source files if no BOM is present
        /// </summary>
        public string Encoding
        {
            get;
            set;
        }

        /// <summary>
        /// List of analysis results for the project
        /// </summary>
        public List<AnalysisResult> AnalysisResults { get; set; }


        /// <summary>
        /// List of additional analysis settings
        /// </summary>
        public AnalysisProperties AnalysisSettings { get; set; }

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
        public static ProjectInfo Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException("fileName");
            }

            ProjectInfo model = Serializer.LoadModel<ProjectInfo>(fileName);
            return model;
        }

        #endregion

    }
}
