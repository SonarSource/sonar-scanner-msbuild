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

using SonarQube.Common;
using System.Collections.Generic;
using System.Linq;

namespace SonarScanner.Shim
{
    public class ProjectInfoAnalysisResult
    {
        #region Constructor(s)
        
        public ProjectInfoAnalysisResult()
        {
            this.Projects = new Dictionary<ProjectInfo, ProjectInfoValidity>();
        }

        #endregion

        #region Public properties

        public IDictionary<ProjectInfo, ProjectInfoValidity> Projects { get; private set; }

        public bool RanToCompletion { get; set; }

        public string FullPropertiesFilePath { get; set; }

        #endregion

        #region Public methods

        public IEnumerable<ProjectInfo> GetProjectsByStatus(ProjectInfoValidity status)
        {
            return this.Projects.Where(p => p.Value == status).Select(p => p.Key).ToArray();
        }

        #endregion

    }
}
