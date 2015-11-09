//-----------------------------------------------------------------------
// <copyright file="TargetConstants.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

namespace SonarQube.MSBuild.Tasks.IntegrationTests
{
    internal static class TargetConstants
    {
        // Target file names
        public const string AnalysisTargetFile = TestUtilities.TestUtils.AnalysisTargetFile;
        public const string ImportsBeforeFile = TestUtilities.TestUtils.ImportsBeforeFile;

        // Targets
        public const string ImportBeforeInfoTarget = "SonarQubeImportBeforeInfo";

        public const string CategoriseProjectTarget = "SonarQubeCategoriseProject";
        public const string CalculateFilesToAnalyzeTarget = "CalculateSonarQubeFilesToAnalyze";
        public const string WriteProjectDataTarget = "WriteSonarQubeProjectData";
        public const string OverrideFxCopSettingsTarget = "OverrideCodeAnalysisProperties";
        public const string SetFxCopResultsTarget = "SetFxCopAnalysisResult";

        public const string DefaultBuildTarget = "Build";
        
        // FxCop
        public const string FxCopTarget = "RunCodeAnalysis";
        public const string FxCopTask = "CodeAnalysis";

        // Roslyn
        public const string OverrideRoslynSettingsTarget = "OverrideRoslynCodeAnalysisProperties";
        public const string SetRoslynResultsTarget = "SetRoslynAnalysisResults";

        // StyleCop
        public const string SetStyleCopSettingsTarget = "SetStyleCopAnalysisSettings";
        public const string StyleCopProjectPathItemName = "sonar.stylecop.projectFilePath";

        public const string MsTestProjectTypeGuid = "3AC096D0-A1C2-E12C-1390-A8335801FDAB";
    }

    internal static class TargetProperties
    {
        // SonarQube Integration constants
        public const string SonarQubeTargetFilePath = "SonarQubeTargetFilePath";
        public const string SonarQubeTargetsPath = "SonarQubeTargetsPath";

        public const string SonarQubeConfigPath = "SonarQubeConfigPath";
        public const string SonarQubeOutputPath = "SonarQubeOutputPath";
        public const string SonarQubeTempPath = "SonarQubeTempPath";
        public const string SonarBuildTasksAssemblyFile = "SonarQubeBuildTasksAssemblyFile";
        public const string AutoImportProperty = "AutoImportSonarQubeAnalysisTargets";

        public const string SonarQubeTestProject = "SonarQubeTestProject";
        public const string SonarQubeExcludeMetadata = "SonarQubeExclude";
        public const string SonarQubeRulesetFormat = "SonarQubeFxCop-{0}.ruleset";

        // Non-SonarQube constants
        public const string ProjectGuid = "ProjectGuid";
        public const string ProjectTypeGuids = "ProjectTypeGuids";

        public const string RunCodeAnalysis = "RunCodeAnalysis";
        public const string CodeAnalysisRuleset = "CodeAnalysisRuleSet";
        public const string CodeAnalysisLogFile = "CodeAnalysisLogFile";

        public const string AssemblyName = "AssemblyName";

        public const string IsInTeamBuild = "TF_Build"; // Common to legacy and non-legacy TeamBuilds

        public const string TargetDir = "TargetDir"; // bin directory into which output will be dropped
        public const string ErrorLog = "ErrorLog"; // file path to which the Roslyn error log should be written
        public const string Language = "Language"; // Language of the project: normally "C#" or "VB"

        // Legacy TeamBuild environment variables (XAML Builds)
        public const string TfsCollectionUri_Legacy = "TF_BUILD_COLLECTIONURI";
        public const string BuildUri_Legacy = "TF_BUILD_BUILDURI";
        public const string BuildDirectory_Legacy = "TF_BUILD_BUILDDIRECTORY";

        // TFS 2015 Environment variables
        public const string TfsCollectionUri_TFS2015 = "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI";
        public const string BuildUri_TFS2015 = "BUILD_BUILDURI";
        public const string BuildDirectory_TFS2015 = "AGENT_BUILDDIRECTORY";
        
        public const string MSBuildExtensionsPath = "MSBuildExtensionsPath";

        public const string ItemType_Compile = "Compile";
        public const string ItemType_Content = "Content";
        public const string AutoGenMetadata = "AutoGen";
    }

}
