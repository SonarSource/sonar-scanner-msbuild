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

using System.Collections.Generic;
using SonarQube.TeamBuild.PreProcessor.Roslyn.Model;

namespace SonarQube.TeamBuild.PreProcessor
{

    /// <summary>
    /// Provides an abstraction for the interactions with the SonarQube server
    /// </summary>
    public interface ISonarQubeServer
    {
        IList<string> GetInactiveRules(string qprofile, string language);

        IList<ActiveRule> GetActiveRules(string qprofile);

        /// <summary>
        /// Get all keys of all available languages
        /// </summary>
        IEnumerable<string> GetAllLanguages();

        /// <summary>
        /// Get all the properties of a project
        /// </summary>
        IDictionary<string, string> GetProperties(string projectKey, string projectBranch);

        /// <summary>
        /// Get the name of the quality profile (of the given language) to be used by the given project key
        /// </summary>
        bool TryGetQualityProfile(string projectKey, string projectBranch, string language, out string qualityProfileKey);

        /// <summary>
        /// Attempts to download a file embedded in the "static" folder in a plugin jar
        /// </summary>
        /// <param name="pluginKey">The key of the plugin containing the file</param>
        /// <param name="embeddedFileName">The name of the file to download</param>
        /// <param name="targetDirectory">The directory to which the file should be downloaded</param>
        bool TryDownloadEmbeddedFile(string pluginKey, string embeddedFileName, string targetDirectory);
    }
}