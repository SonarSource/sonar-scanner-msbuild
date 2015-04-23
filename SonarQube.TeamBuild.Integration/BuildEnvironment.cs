//-----------------------------------------------------------------------
// <copyright file="BuildEnvironment.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.TeamBuild.Integration
{
    /// <summary>
    /// Lists the recognised build environments
    /// </summary>
    public enum BuildEnvironment
    {
        NotTeamBuild,
        LegacyTeamBuild,
        TeamBuild
    }
}