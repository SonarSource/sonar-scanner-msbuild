﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SonarScanner.MSBuild.Shim {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SonarScanner.MSBuild.Shim.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Dumping content of sonar-project.properties
        ///------------------------------------------------------------------------
        ///{0}
        ///------------------------------------------------------------------------.
        /// </summary>
        internal static string DEBUG_DumpSonarProjectProperties {
            get {
                return ResourceManager.GetString("DEBUG_DumpSonarProjectProperties", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File was referenced by the following projects: &apos;{0}&apos;..
        /// </summary>
        internal static string DEBUG_FileReferencedByProjects {
            get {
                return ResourceManager.GetString("DEBUG_FileReferencedByProjects", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The SonarScanner for MSBuild integration failed: SonarQube was unable to collect the required information about your projects.
        ///Possible causes:
        ///  1. The project has not been built - the project must be built in between the begin and end steps.
        ///  2. An unsupported version of MSBuild has been used to build the project. Supported versions: MSBuild 16 and higher.
        ///  3. The begin, build and end steps have not all been launched from the same folder.
        ///  4. None of the analyzed projects have a valid ProjectGuid  [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ERR_NoProjectInfoFilesFound {
            get {
                return ResourceManager.GetString("ERR_NoProjectInfoFilesFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No analysable projects were found. SonarQube analysis will not be performed. Check the build summary report for details..
        /// </summary>
        internal static string ERR_NoValidProjectInfoFiles {
            get {
                return ResourceManager.GetString("ERR_NoValidProjectInfoFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The project base directory cannot be automatically detected. Please specify the &quot;/d:sonar.projectBaseDir&quot; on the begin step..
        /// </summary>
        internal static string ERR_ProjectBaseDirCannotBeAutomaticallyDetected {
            get {
                return ResourceManager.GetString("ERR_ProjectBaseDirCannotBeAutomaticallyDetected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The project base directory doesn&apos;t exist..
        /// </summary>
        internal static string ERR_ProjectBaseDirDoesNotExist {
            get {
                return ResourceManager.GetString("ERR_ProjectBaseDirDoesNotExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The SonarScanner did not complete successfully.
        /// </summary>
        internal static string ERR_SonarScannerExecutionFailed {
            get {
                return ResourceManager.GetString("ERR_SonarScannerExecutionFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The TFS Processor did not complete successfully.
        /// </summary>
        internal static string ERR_TFSProcessorExecutionFailed {
            get {
                return ResourceManager.GetString("ERR_TFSProcessorExecutionFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Generating SonarQube project properties file to {0}.
        /// </summary>
        internal static string MSG_GeneratingProjectProperties {
            get {
                return ResourceManager.GetString("MSG_GeneratingProjectProperties", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Analysis property is already correctly set: {0}={1}.
        /// </summary>
        internal static string MSG_MandatorySettingIsCorrectlySpecified {
            get {
                return ResourceManager.GetString("MSG_MandatorySettingIsCorrectlySpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Starting with Scanner for .NET v8 the way the `sonar.projectBaseDir` property is automatically detected has changed and this has an impact on the files that are analyzed and other properties that are resolved relative to it like `sonar.exclusions` and `sonar.test.exclusions`. If you would like to customize the behavior, please set the `sonar.projectBaseDir` property to point to a directory that contains all the source code you want to analyze. The path may be relative (to the directory from which the analys [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string MSG_ProjectBaseDirChange {
            get {
                return ResourceManager.GetString("MSG_ProjectBaseDirChange", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The exclude flag has been set so the project will not be analyzed. Project file: {0}.
        /// </summary>
        internal static string MSG_ProjectIsExcluded {
            get {
                return ResourceManager.GetString("MSG_ProjectIsExcluded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Generation of the sonar-properties file failed. Unable to complete the analysis..
        /// </summary>
        internal static string MSG_PropertiesGenerationFailed {
            get {
                return ResourceManager.GetString("MSG_PropertiesGenerationFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The supplied Code Analysis ErrorLog file is not a valid json file: {0}.
        /// </summary>
        internal static string MSG_SarifFileIsInvalid {
            get {
                return ResourceManager.GetString("MSG_SarifFileIsInvalid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The supplied Code Analysis ErrorLog file is a valid json file and does not need to be fixed: {0}.
        /// </summary>
        internal static string MSG_SarifFileIsValid {
            get {
                return ResourceManager.GetString("MSG_SarifFileIsValid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No Code Analysis ErrorLog file found at {0}..
        /// </summary>
        internal static string MSG_SarifFileNotFound {
            get {
                return ResourceManager.GetString("MSG_SarifFileNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fixed invalid Code Analysis ErrorLog file. Please check that VS 2015 Update 1 (or later) is installed. Fixed file: {0}.
        /// </summary>
        internal static string MSG_SarifFixSuccess {
            get {
                return ResourceManager.GetString("MSG_SarifFixSuccess", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Setting analysis property: {0}={1}.
        /// </summary>
        internal static string MSG_SettingAnalysisProperty {
            get {
                return ResourceManager.GetString("MSG_SettingAnalysisProperty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Setting the JAVA_HOME for the scanner cli to {0}..
        /// </summary>
        internal static string MSG_SettingJavaHomeEnvironmentVariable {
            get {
                return ResourceManager.GetString("MSG_SettingJavaHomeEnvironmentVariable", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Setting the JAVA_HOME for the scanner cli failed. `sonar.scanner.javaExePath` is `{0}`. {1}.
        /// </summary>
        internal static string MSG_SettingJavaHomeEnvironmentVariableFailed {
            get {
                return ResourceManager.GetString("MSG_SettingJavaHomeEnvironmentVariableFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Calling the SonarScanner CLI....
        /// </summary>
        internal static string MSG_SonarScannerCalling {
            get {
                return ResourceManager.GetString("MSG_SonarScannerCalling", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The SonarScanner CLI has finished.
        /// </summary>
        internal static string MSG_SonarScannerCompleted {
            get {
                return ResourceManager.GetString("MSG_SonarScannerCompleted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The SONAR_SCANNER_HOME environment variable is not required and will be ignored..
        /// </summary>
        internal static string MSG_SonarScannerHomeIsSet {
            get {
                return ResourceManager.GetString("MSG_SonarScannerHomeIsSet", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Calling the TFS Processor executable....
        /// </summary>
        internal static string MSG_TFSProcessorCalling {
            get {
                return ResourceManager.GetString("MSG_TFSProcessorCalling", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The TFS Processor has finished.
        /// </summary>
        internal static string MSG_TFSProcessorCompleted {
            get {
                return ResourceManager.GetString("MSG_TFSProcessorCompleted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Using TFS/Azure DevOps sources directory as project base directory: &apos;{0}&apos;..
        /// </summary>
        internal static string MSG_UsingAzDoSourceDirectoryAsProjectBaseDir {
            get {
                return ResourceManager.GetString("MSG_UsingAzDoSourceDirectoryAsProjectBaseDir", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Using longest common projects path as a base directory: &apos;{0}&apos;. Identified project paths:{1}.
        /// </summary>
        internal static string MSG_UsingLongestCommonBaseDir {
            get {
                return ResourceManager.GetString("MSG_UsingLongestCommonBaseDir", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Using the supplied value for {0}. Value: {1}.
        /// </summary>
        internal static string MSG_UsingSuppliedSonarScannerOptsValue {
            get {
                return ResourceManager.GetString("MSG_UsingSuppliedSonarScannerOptsValue", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Using user supplied project base directory: &apos;{0}&apos;.
        /// </summary>
        internal static string MSG_UsingUserSuppliedProjectBaseDir {
            get {
                return ResourceManager.GetString("MSG_UsingUserSuppliedProjectBaseDir", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Using working directory as project base directory: &apos;{0}&apos;..
        /// </summary>
        internal static string MSG_UsingWorkingDirectoryAsProjectBaseDir {
            get {
                return ResourceManager.GetString("MSG_UsingWorkingDirectoryAsProjectBaseDir", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Writing processing summary to {0}.
        /// </summary>
        internal static string MSG_WritingSummary {
            get {
                return ResourceManager.GetString("MSG_WritingSummary", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Excluded projects.
        /// </summary>
        internal static string REPORT_ExcludedProjectsTitle {
            get {
                return ResourceManager.GetString("REPORT_ExcludedProjectsTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid projects.
        /// </summary>
        internal static string REPORT_InvalidProjectsTitle {
            get {
                return ResourceManager.GetString("REPORT_InvalidProjectsTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {none}.
        /// </summary>
        internal static string REPORT_NoProjectsOfType {
            get {
                return ResourceManager.GetString("REPORT_NoProjectsOfType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Product projects.
        /// </summary>
        internal static string REPORT_ProductProjectsTitle {
            get {
                return ResourceManager.GetString("REPORT_ProductProjectsTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Skipped projects.
        /// </summary>
        internal static string REPORT_SkippedProjectsTitle {
            get {
                return ResourceManager.GetString("REPORT_SkippedProjectsTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test projects.
        /// </summary>
        internal static string REPORT_TestProjectsTitle {
            get {
                return ResourceManager.GetString("REPORT_TestProjectsTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to get {0} from: &apos;{1}&apos;..
        /// </summary>
        internal static string WARN_DirectoryGetContentFailure {
            get {
                return ResourceManager.GetString("WARN_DirectoryGetContentFailure", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Directory &apos;{0}&apos; is not located under the base directory &apos;{1}&apos; and will not be analyzed..
        /// </summary>
        internal static string WARN_DirectoryIsOutsideBaseDir {
            get {
                return ResourceManager.GetString("WARN_DirectoryIsOutsideBaseDir", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The support for multi-language analysis may not function correctly if {0} is set. If this is the case, please explicitly set &quot;sonar.scanner.scanAll=false&quot; to disable the multi-language analysis..
        /// </summary>
        internal static string WARN_DisableScanAllAnalysisWhenProvidingParameters {
            get {
                return ResourceManager.GetString("WARN_DisableScanAllAnalysisWhenProvidingParameters", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Duplicate ProjectGuid: &quot;{0}&quot;. The project will not be analyzed. Project file: &quot;{1}&quot;.
        /// </summary>
        internal static string WARN_DuplicateProjectGuid {
            get {
                return ResourceManager.GetString("WARN_DuplicateProjectGuid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The following projects do not have a valid ProjectGuid and were not built using a valid solution (.sln) thus will be skipped from analysis...
        ///{0}.
        /// </summary>
        internal static string WARN_EmptyProjectGuids {
            get {
                return ResourceManager.GetString("WARN_EmptyProjectGuids", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File &apos;{0}&apos; does not exist..
        /// </summary>
        internal static string WARN_FileDoesNotExist {
            get {
                return ResourceManager.GetString("WARN_FileDoesNotExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File &apos;{0}&apos; is not located under the base directory &apos;{1}&apos; and will not be analyzed..
        /// </summary>
        internal static string WARN_FileIsOutsideProjectDirectory {
            get {
                return ResourceManager.GetString("WARN_FileIsOutsideProjectDirectory", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The following paths contain invalid characters and will be excluded from this analysis: {0}.
        /// </summary>
        internal static string WARN_InvalidCharacterInPaths {
            get {
                return ResourceManager.GetString("WARN_InvalidCharacterInPaths", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The project has an invalid GUID &quot;{0}&quot;. The project will not be analyzed. Project file: {1}.
        /// </summary>
        internal static string WARN_InvalidProjectGuid {
            get {
                return ResourceManager.GetString("WARN_InvalidProjectGuid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Overriding analysis property. Effective value: {0}={1}.
        /// </summary>
        internal static string WARN_OverridingAnalysisProperty {
            get {
                return ResourceManager.GetString("WARN_OverridingAnalysisProperty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property &quot;{0}&quot; is defined, but will be ignored during analysis..
        /// </summary>
        internal static string WARN_PropertyIgnored {
            get {
                return ResourceManager.GetString("WARN_PropertyIgnored", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to fix Code Analysis ErrorLog file. Please check that VS 2015 Update 1 (or later) is installed..
        /// </summary>
        internal static string WARN_SarifFixFail {
            get {
                return ResourceManager.GetString("WARN_SarifFixFail", resourceCulture);
            }
        }
    }
}
