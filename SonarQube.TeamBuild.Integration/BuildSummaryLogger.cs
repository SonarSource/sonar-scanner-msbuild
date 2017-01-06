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

using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using System;

namespace SonarQube.TeamBuild.Integration
{
    /// <summary>
    /// Wrapper to help write custom build summary messages
    /// </summary>
    /// <remarks>The class will connect to TFS when the first message is written, and
    /// save all of the written messages to the server when the class is disposed</remarks>
    public class BuildSummaryLogger : IDisposable
    {
        /// <summary>
        /// The priority specifies where this summary section appears in the list of summary sections.
        /// </summary>
        private const int SectionPriority = 200;

        /// <summary>
        /// Unique id for the section
        /// </summary>
        private const string SectionName = "SonarTeamBuildSummary";

        bool disposed;

        private readonly string tfsUri;
        private readonly string buildUri;

        private TfsTeamProjectCollection teamProjectCollection;
        private IBuildDetail build;

        #region Public methods

        public BuildSummaryLogger(string tfsUri, string buildUri)
        {
            if (string.IsNullOrWhiteSpace(tfsUri))
            {
                throw new ArgumentNullException("tfsUri");
            }
            if (string.IsNullOrWhiteSpace(buildUri))
            {
                throw new ArgumentNullException("buildUri");
            }

            this.tfsUri = tfsUri;
            this.buildUri = buildUri;
        }

        /// <summary>
        /// Writes the custom build summary message
        /// </summary>
        public void WriteMessage(string message, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentNullException("message");
            }

            string finalMessage = message;
            if (args != null && args.Length > 0)
            {
                finalMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, message, args);
            }

            this.EnsureConnected();
            this.build.Information.AddCustomSummaryInformation(finalMessage, SectionName, Resources.SonarQubeSummarySectionHeader, SectionPriority).Save();
        }

        #endregion

        #region IDisposable interface

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed && disposing && this.teamProjectCollection != null)
            {
                this.build.Save();

                this.teamProjectCollection.Dispose();
                this.teamProjectCollection = null;
                this.build = null;
            }

            this.disposed = true;
        }

        #endregion

        private void EnsureConnected()
        {
            if (this.teamProjectCollection == null)
            {
                this.teamProjectCollection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(this.tfsUri));
                this.build = teamProjectCollection.GetService<IBuildServer>().GetBuild(new Uri(this.buildUri));
            }

        }
    }
}
