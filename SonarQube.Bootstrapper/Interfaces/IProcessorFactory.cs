//-----------------------------------------------------------------------
// <copyright file="IBootstrapperSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.TeamBuild.PostProcessor.Interfaces;
using SonarQube.TeamBuild.PreProcessor;

namespace SonarQube.Bootstrapper
{
    public interface IProcessorFactory
    {
        IMSBuildPostProcessor CreatePostProcessor();
        ITeamBuildPreProcessor CreatePreProcessor();
    }
}
