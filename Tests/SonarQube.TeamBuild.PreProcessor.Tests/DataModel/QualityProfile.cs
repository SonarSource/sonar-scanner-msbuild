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

using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    internal class QualityProfile
    {
        private readonly string id;
        private readonly string language;
        private readonly ISet<string> projectIds;

        private readonly IList<string> inactiveRules;
        private readonly IList<ActiveRule> activeRules;

        public QualityProfile(string id, string language)
        {
            this.id = id;
            this.language = language;
            this.projectIds = new HashSet<string>();
            this.inactiveRules = new List<string>();
            this.activeRules = new List<ActiveRule>();
        }

        public QualityProfile AddProject(string projectKey, string projectBranch = null)
        {
            string projectId = projectKey;
            if (!String.IsNullOrWhiteSpace(projectBranch))
            {
                projectId = projectKey + ":" + projectBranch;
            }

            this.projectIds.Add(projectId);
            return this;
        }

        public QualityProfile AddRule(ActiveRule rule)
        {
            this.activeRules.Add(rule);
            return this;
        }

        public QualityProfile AddInactiveRule(string ruleKey)
        {
            this.inactiveRules.Add(ruleKey);
            return this;
        }

        public string Id { get { return this.id; } }
        public string Language { get { return this.language; } }
        public IEnumerable<string> Projects { get { return this.projectIds; } }
        public IList<ActiveRule> ActiveRules { get { return this.activeRules; } }
        public IList<string> InactiveRules { get { return this.inactiveRules; } }

    }
}
