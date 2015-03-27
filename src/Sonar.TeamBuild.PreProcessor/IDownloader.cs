//-----------------------------------------------------------------------
// <copyright file="IDownloader.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonarQube.TeamBuild.PreProcessor
{
    public interface IDownloader : IDisposable
    {
        bool TryDownloadIfExists(string url, out string contents);

        string Download(string url);
    }
}
