//-----------------------------------------------------------------------
// <copyright file="IBuildAgentUpdater.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;

namespace SonarQube.Bootstrapper
{
    public interface IBuildAgentUpdater
    {
        void Update(string hostUrl, string targetDir, ILogger logger);
    }
}
