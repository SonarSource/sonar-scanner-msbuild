//-----------------------------------------------------------------------
// <copyright file="BuildSummaryLogger.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using System;

namespace Sonar.TeamBuild.Integration
{
    public class BuildSummaryLogger : IDisposable
    {
        private const int SectionPriority = 200;
        private const string SectionName = "SonarBuildSummary";

        bool disposed;

        private string tfsUri;
        private string buildUri;

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

        public void WriteMessage(string message, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentNullException("message");
            }

            if (args != null && args.Length > 0)
            {
                message = string.Format(System.Globalization.CultureInfo.CurrentCulture, message, args);
            }

            this.EnsureConnected();
            this.build.Information.AddCustomSummaryInformation(message, SectionName, Resources.SonarSummarySectionHeader, SectionPriority).Save();
        }

        #endregion

        #region IDisposable interface

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (this.teamProjectCollection != null)
                {
                    this.build.Save();

                    this.teamProjectCollection.Dispose();
                    this.teamProjectCollection = null;
                    this.build = null;
                }
                this.disposed = true;
            }
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
