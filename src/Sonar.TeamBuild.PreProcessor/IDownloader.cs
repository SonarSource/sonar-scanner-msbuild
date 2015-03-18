//-----------------------------------------------------------------------
// <copyright file="IDownloader.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sonar.TeamBuild.PreProcessor
{
    public interface IDownloader : IDisposable
    {
        bool TryDownloadIfExists(string url, out string contents);

        string Download(string url);
    }
}
