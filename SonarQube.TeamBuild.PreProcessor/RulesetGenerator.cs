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

using SonarQube.TeamBuild.PreProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor
{
    public sealed class RulesetGenerator : IRulesetGenerator
    {
        #region #region IRulesetGenerator interface

        /// <summary>
        /// Generates an FxCop file on disc containing all internalKeys from rules belonging to the given repo.
        /// </summary>
        /// <param name="fxCopRepositoryKey">The key of the FxCop repository</param>
        /// <param name="outputFilePath">The full path to the file to be generated</param>
        public void Generate(string fxCopRepositoryKey, IList<ActiveRule> activeRules, string outputFilePath)
        {
            if (string.IsNullOrWhiteSpace(fxCopRepositoryKey))
            {
                throw new ArgumentNullException("fxCopRepositoryKey");
            }
            if (activeRules == null)
            {
                throw new ArgumentNullException("activeRules");
            }

            IEnumerable<ActiveRule> fxCopActiveRules = activeRules.Where(r => r.RepoKey.Equals(fxCopRepositoryKey));

            if (fxCopActiveRules.Any())
            {
                var ids = fxCopActiveRules.Select(r => r.InternalKeyOrKey);
                File.WriteAllText(outputFilePath, RulesetWriter.ToString(ids));
            }
            else
            {
                File.Delete(outputFilePath);
            }
        }

        #endregion
    }
}
