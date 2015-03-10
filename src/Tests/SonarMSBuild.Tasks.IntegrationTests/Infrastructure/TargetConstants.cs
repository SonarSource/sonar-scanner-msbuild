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
        public const string AnalysisTargetFile = "Sonar.Integration.v0.1.targets";
        public const string SonarImportsBeforeFile = "Sonar.Integration.ImportBefore.targets";

        // Targets
        public const string WriteSonarProjectDataTarget = "WriteSonarProjectData";
        public const string SonarOverrideFxCopSettingsTarget = "OverrideCodeAnalysisProperties";
        public const string SonarSetFxCopResultsTarget = "SetFxCopAnalysisResult";

        public const string DefaultBuildTarget = "Build";
        
        // FxCop
        public const string FxCopTarget = "RunCodeAnalysis";
        public const string FxCopTask = "CodeAnalysis";

    }

    internal static class TargetProperties
    {
        public const string ProjectGuid = "ProjectGuid";
        public const string ProjectName = "ProjectName";

        public const string SonarTargets = "SonarTargets";
        public const string SonarBinPath = "SonarBinPath";

        public const string RunSonarAnalysis = "RunSonarAnalysis";
        public const string SonarConfigPath = "SonarConfigPath";
        public const string SonarOutputPath = "SonarOutputPath";
        public const string SonarTempPath = "SonarTempPath";
        public const string SonarBuildTasksAssemblyFile = "SonarBuildTasksAssemblyFile";

        public const string RunCodeAnalysis = "RunCodeAnalysis";
        public const string CodeAnalysisRuleset = "CodeAnalysisRuleSet";
        public const string CodeAnalysisLogFile = "CodeAnalysisLogFile";

        public const string TeamBuildBuildDirectory = "TF_BUILD_BUILDDIRECTORY";
        public const string MSBuildExtensionsPath = "MSBuildExtensionsPath";
    }

}
