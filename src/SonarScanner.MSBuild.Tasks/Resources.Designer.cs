﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SonarScanner.MSBuild.Tasks {
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SonarScanner.MSBuild.Tasks.Resources", typeof(Resources).Assembly);
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
        ///   Looks up a localized string similar to Overwriting analysis settings for a test project....
        /// </summary>
        internal static string AnalyzerSettings_ConfiguringTestProjectAnalysis {
            get {
                return ResourceManager.GetString("AnalyzerSettings_ConfiguringTestProjectAnalysis", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Creating merged ruleset at &apos;{0}&apos;.
        /// </summary>
        internal static string AnalyzerSettings_CreatingMergedRuleset {
            get {
                return ResourceManager.GetString("AnalyzerSettings_CreatingMergedRuleset", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to External issues are not supported on this version of SonarQube. Version 7.4+ is required..
        /// </summary>
        internal static string AnalyzerSettings_ExternalIssueNotSupported {
            get {
                return ResourceManager.GetString("AnalyzerSettings_ExternalIssueNotSupported", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Analysis language is not specified.
        /// </summary>
        internal static string AnalyzerSettings_LanguageNotSpecified {
            get {
                return ResourceManager.GetString("AnalyzerSettings_LanguageNotSpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Merging analysis settings....
        /// </summary>
        internal static string AnalyzerSettings_MergingSettings {
            get {
                return ResourceManager.GetString("AnalyzerSettings_MergingSettings", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No analysis settings were found in the config file for the current language: {0}.
        /// </summary>
        internal static string AnalyzerSettings_NoSettingsFoundForCurrentLanguage {
            get {
                return ResourceManager.GetString("AnalyzerSettings_NoSettingsFoundForCurrentLanguage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Analyzer settings for language {0} have not been specified in the analysis config file..
        /// </summary>
        internal static string AnalyzerSettings_NotSpecifiedInConfig {
            get {
                return ResourceManager.GetString("AnalyzerSettings_NotSpecifiedInConfig", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Supplied ruleset path is not rooted: &apos;{0}&apos;.
        /// </summary>
        internal static string AnalyzerSettings_OriginalRulesetIsNotRooted {
            get {
                return ResourceManager.GetString("AnalyzerSettings_OriginalRulesetIsNotRooted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Supplied ruleset path is rooted: &apos;{0}&apos;.
        /// </summary>
        internal static string AnalyzerSettings_OriginalRulesetIsRooted {
            get {
                return ResourceManager.GetString("AnalyzerSettings_OriginalRulesetIsRooted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Original ruleset not specified. Using generated ruleset at &apos;{0}&apos;.
        /// </summary>
        internal static string AnalyzerSettings_OriginalRulesetNotSpecified {
            get {
                return ResourceManager.GetString("AnalyzerSettings_OriginalRulesetNotSpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Overwriting analysis settings....
        /// </summary>
        internal static string AnalyzerSettings_OverwritingSettings {
            get {
                return ResourceManager.GetString("AnalyzerSettings_OverwritingSettings", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Removing duplicate analyzers: {0}.
        /// </summary>
        internal static string AnalyzerSettings_RemovingDuplicateAnalyzers {
            get {
                return ResourceManager.GetString("AnalyzerSettings_RemovingDuplicateAnalyzers", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Removing duplicate files: {0}.
        /// </summary>
        internal static string AnalyzerSettings_RemovingDuplicateFiles {
            get {
                return ResourceManager.GetString("AnalyzerSettings_RemovingDuplicateFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Found ruleset file: &apos;{0}&apos;.
        /// </summary>
        internal static string AnalyzerSettings_ResolvedRulesetFound {
            get {
                return ResourceManager.GetString("AnalyzerSettings_ResolvedRulesetFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to find ruleset file: &apos;{0}&apos;.
        /// </summary>
        internal static string AnalyzerSettings_ResolvedRulesetNotFound {
            get {
                return ResourceManager.GetString("AnalyzerSettings_ResolvedRulesetNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The configured regular expression for detecting test projects is invalid.
        ///Check the property &quot;{2}&quot;.
        ///Expression: {0}
        ///Error: {1}.
        /// </summary>
        internal static string IsTest_InvalidRegularExpression {
            get {
                return ResourceManager.GetString("IsTest_InvalidRegularExpression", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The deprecated regular expression property sonar.msbuild.testProjectPattern was not set in the analysis config file. The project file name will not be checked..
        /// </summary>
        internal static string IsTest_NameNotChecked {
            get {
                return ResourceManager.GetString("IsTest_NameNotChecked", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No references were resolved for the current project..
        /// </summary>
        internal static string IsTest_NoReferences {
            get {
                return ResourceManager.GetString("IsTest_NoReferences", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No test reference was found for the current project..
        /// </summary>
        internal static string IsTest_NoTestReference {
            get {
                return ResourceManager.GetString("IsTest_NoTestReference", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Resolved test reference: {0}.
        /// </summary>
        internal static string IsTest_ResolvedReference {
            get {
                return ResourceManager.GetString("IsTest_ResolvedReference", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to parse assembly name &apos;{0}&apos;.
        /// </summary>
        internal static string IsTest_UnableToParseAssemblyName {
            get {
                return ResourceManager.GetString("IsTest_UnableToParseAssemblyName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Using regular expression for detecting test projects from analysis config file: {0}.
        /// </summary>
        internal static string IsTest_UsingRegExFromConfig {
            get {
                return ResourceManager.GetString("IsTest_UsingRegExFromConfig", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Moving directory {0} to {1}..
        /// </summary>
        internal static string MoveDirectory_FromTo {
            get {
                return ResourceManager.GetString("MoveDirectory_FromTo", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The source directory is invalid..
        /// </summary>
        internal static string MoveDirectory_InvalidSourceDirectory {
            get {
                return ResourceManager.GetString("MoveDirectory_InvalidSourceDirectory", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Config file could not be found.
        /// </summary>
        internal static string Shared_ConfigFileNotFound {
            get {
                return ResourceManager.GetString("Shared_ConfigFileNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error reading config file: {0}.
        /// </summary>
        internal static string Shared_ErrorReadingConfigFile {
            get {
                return ResourceManager.GetString("Shared_ErrorReadingConfigFile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to read the config file: {0}.
        /// </summary>
        internal static string Shared_ReadingConfigFailed {
            get {
                return ResourceManager.GetString("Shared_ReadingConfigFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Reading config file: {0} ....
        /// </summary>
        internal static string Shared_ReadingConfigFile {
            get {
                return ResourceManager.GetString("Shared_ReadingConfigFile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Read config file successfully.
        /// </summary>
        internal static string Shared_ReadingConfigSucceeded {
            get {
                return ResourceManager.GetString("Shared_ReadingConfigSucceeded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to resolve path relative to project file. Path: {0}.
        /// </summary>
        internal static string WPIF_FailedToResolvePath {
            get {
                return ResourceManager.GetString("WPIF_FailedToResolvePath", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No ProjectGuid has been found in neither the csproj nor the solution (Project {0}). A random one has been generated ({1})..
        /// </summary>
        internal static string WPIF_GeneratingRandomGuid {
            get {
                return ResourceManager.GetString("WPIF_GeneratingRandomGuid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Sonar: The project does not have a valid ProjectGuid. Analysis results for this project will not be uploaded. Project file: {0}.
        /// </summary>
        internal static string WPIF_MissingOrInvalidProjectGuid {
            get {
                return ResourceManager.GetString("WPIF_MissingOrInvalidProjectGuid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Resolved relative path. Path: {0}.
        /// </summary>
        internal static string WPIF_ResolvedPath {
            get {
                return ResourceManager.GetString("WPIF_ResolvedPath", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Attempting to resolve the file result path. Analysis type: {0}, path: {1}.
        /// </summary>
        internal static string WPIF_ResolvingRelativePath {
            get {
                return ResourceManager.GetString("WPIF_ResolvingRelativePath", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Analysis setting key &quot;{0}&quot; is invalid and will be ignored.
        /// </summary>
        internal static string WPIF_WARN_InvalidSettingKey {
            get {
                return ResourceManager.GetString("WPIF_WARN_InvalidSettingKey", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Analysis setting &quot;{0}&quot; does not have &quot;Value&quot; metadata and will be ignored.
        /// </summary>
        internal static string WPIF_WARN_MissingValueMetadata {
            get {
                return ResourceManager.GetString("WPIF_WARN_MissingValueMetadata", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Writing zero-length file:{0}.
        /// </summary>
        internal static string WriteZeroLengthFiles_WritingFile {
            get {
                return ResourceManager.GetString("WriteZeroLengthFiles_WritingFile", resourceCulture);
            }
        }
    }
}
