//-----------------------------------------------------------------------
// <copyright file="TeamBuildEnvironmentVariables.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Sonar.TeamBuild.Integration
{
    public static class TeamBuildEnvironmentVariables
    {
        public const string IsInTeamBuild = "TF_Build";
        public const string TfsCollectionUri = "TF_BUILD_COLLECTIONURI";
        public const string BuildUri = "TF_BUILD_BUILDURI";
        public const string BuildDirectory = "TF_BUILD_BUILDDIRECTORY";
        public const string BinariesDirectory = "TF_BUILD_BINARIESDIRECTORY";

        // Other available environment variables:
        //TF_BUILD_BUILDDEFINITIONNAME: SimpleBuild1
        //TF_BUILD_BUILDNUMBER: SimpleBuild1_20150310.4
        //TF_BUILD_BUILDREASON: Manual
        //TF_BUILD_DROPLOCATION: 
        //TF_BUILD_SOURCEGETVERSION: C14
        //TF_BUILD_SOURCESDIRECTORY: C:\Builds\1\Demos\SimpleBuild1\src
        //TF_BUILD_TESTRESULTSDIRECTORY: C:\Builds\1\Demos\SimpleBuild1\tst
    }
}
