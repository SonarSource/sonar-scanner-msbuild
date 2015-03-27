//-----------------------------------------------------------------------
// <copyright file="TargetConstants.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace Sonar.MSBuild.Tasks.IntegrationTests
{
    internal static class TargetConstants
    {
        // Target file names
        public const string AnalysisTargetFile = "Sonar.Integration.v0.1.targets";
        public const string SonarImportsBeforeFile = "Sonar.Integration.ImportBefore.targets";

        // Targets
        public const string SonarQubeImportBeforeInfoTarget = "SonarQubeImportBeforeInfo";
        
        public const string ExecuteSonarProcessingTarget = "ExecuteSonarProcessing";
        public const string WriteSonarProjectDataTarget = "WriteSonarProjectData";
        public const string SonarOverrideFxCopSettingsTarget = "OverrideCodeAnalysisProperties";
        public const string SonarSetFxCopResultsTarget = "SetFxCopAnalysisResult";

        public const string DefaultBuildTarget = "Build";
        
        // FxCop
        public const string FxCopTarget = "RunCodeAnalysis";
        public const string FxCopTask = "CodeAnalysis";

        public const string MsTestProjectTypeGuid = "3AC096D0-A1C2-E12C-1390-A8335801FDAB";
    }

    internal static class TargetProperties
    {
        public const string ProjectGuid = "ProjectGuid";
        public const string ProjectTypeGuid = "ProjectTypeGuid";

        public const string SonarTargetFilePath = "SonarTargetFilePath";
        public const string SonarTargetsPath = "SonarTargetsPath";

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


        public const string SonarTestProject = "SonarTestProject";
        public const string SonarTestProjectNameRegex = "SonarTestProjectNameRegex";
        public const string SonarExclude = "SonarExclude";

        public const string ItemType_Compile = "Compile";
        public const string ItemType_Content = "Content";
    }

}
