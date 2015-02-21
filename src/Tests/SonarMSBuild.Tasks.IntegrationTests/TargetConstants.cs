//-----------------------------------------------------------------------
// <copyright file="TargetConstants.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarMSBuild.Tasks.IntegrationTests
{
    internal static class TargetConstants
    {
        // Target file names
        public const string AnalysisTargetFileName = "Sonar.Integration.v0.1.targets";

        // Targets
        public const string WriteSonarProjectDataTargetName = "WriteSonarProjectData";
    }

    internal static class TargetProperties
    {
        public const string ProjectGuid = "ProjectGuid";
        public const string ProjectName = "ProjectName";

        public const string SonarOutputPath = "SonarOutputPath";

        public const string SonarBuildTasksAssemblyFile = "SonarBuildTasksAssemblyFile";
    }

}
