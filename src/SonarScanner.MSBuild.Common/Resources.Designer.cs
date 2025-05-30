﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SonarScanner.MSBuild.Common {
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SonarScanner.MSBuild.Common.Resources", typeof(Resources).Assembly);
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
        ///   Looks up a localized string similar to /d:[key]=[value].
        /// </summary>
        internal static string CmdLine_ArgDescription_DynamicProperty {
            get {
                return ResourceManager.GetString("CmdLine_ArgDescription_DynamicProperty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [/s:{full path to the analysis settings file}].
        /// </summary>
        internal static string CmdLine_ArgDescription_PropertiesFilePath {
            get {
                return ResourceManager.GetString("CmdLine_ArgDescription_PropertiesFilePath", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The arguments &apos;sonar.host.url&apos; and &apos;sonar.scanner.sonarcloudUrl&apos; are both set to an invalid value..
        /// </summary>
        internal static string ERR_HostUrlAndSonarcloudUrlAreEmpty {
            get {
                return ResourceManager.GetString("ERR_HostUrlAndSonarcloudUrlAreEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The arguments &apos;sonar.host.url&apos; and &apos;sonar.scanner.sonarcloudUrl&apos; are both set and are different. Please set either &apos;sonar.host.url&apos; for SonarQube or &apos;sonar.scanner.sonarcloudUrl&apos; for SonarCloud..
        /// </summary>
        internal static string ERR_HostUrlDiffersFromSonarcloudUrl {
            get {
                return ResourceManager.GetString("ERR_HostUrlDiffersFromSonarcloudUrl", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to create an empty directory &apos;{0}&apos;. Please check that there are no open or read-only files in the directory and that you have the necessary read/write permissions.
        ///  Detailed error message: {1}.
        /// </summary>
        internal static string ERROR_CannotCreateEmptyDirectory {
            get {
                return ResourceManager.GetString("ERROR_CannotCreateEmptyDirectory", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The property &apos;{0}&apos; is automatically set by the SonarScanner for MSBuild and cannot be overridden on the command line..
        /// </summary>
        internal static string ERROR_CmdLine_CannotSetPropertyOnCommandLine {
            get {
                return ResourceManager.GetString("ERROR_CmdLine_CannotSetPropertyOnCommandLine", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A value has already been supplied for this argument: {0}. Existing: &apos;{1}&apos;.
        /// </summary>
        internal static string ERROR_CmdLine_DuplicateArg {
            get {
                return ResourceManager.GetString("ERROR_CmdLine_DuplicateArg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A value has already been supplied for this property. Key: {0}, existing value: {1}.
        /// </summary>
        internal static string ERROR_CmdLine_DuplicateProperty {
            get {
                return ResourceManager.GetString("ERROR_CmdLine_DuplicateProperty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The format of the analysis property {0} is invalid.
        /// </summary>
        internal static string ERROR_CmdLine_InvalidAnalysisProperty {
            get {
                return ResourceManager.GetString("ERROR_CmdLine_InvalidAnalysisProperty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A required argument is missing: {0}.
        /// </summary>
        internal static string ERROR_CmdLine_MissingRequiredArgument {
            get {
                return ResourceManager.GetString("ERROR_CmdLine_MissingRequiredArgument", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Please use the parameter prefix &apos;/o:&apos; to define the organization of the project instead of injecting this organization with the help of the &apos;sonar.organization&apos; property..
        /// </summary>
        internal static string ERROR_CmdLine_MustUseOrganization {
            get {
                return ResourceManager.GetString("ERROR_CmdLine_MustUseOrganization", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Please use the parameter prefix &apos;/k:&apos; to define the key of the project instead of injecting this key with the help of the &apos;sonar.projectKey&apos; property..
        /// </summary>
        internal static string ERROR_CmdLine_MustUseProjectKey {
            get {
                return ResourceManager.GetString("ERROR_CmdLine_MustUseProjectKey", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Please use the parameter prefix &apos;/n:&apos; to define the name of the project instead of injecting this name with the help of the &apos;sonar.projectName&apos; property..
        /// </summary>
        internal static string ERROR_CmdLine_MustUseProjectName {
            get {
                return ResourceManager.GetString("ERROR_CmdLine_MustUseProjectName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Please use the parameter prefix &apos;/v:&apos; to define the version of the project instead of injecting this version with the help of the &apos;sonar.projectVersion&apos; property..
        /// </summary>
        internal static string ERROR_CmdLine_MustUseProjectVersion {
            get {
                return ResourceManager.GetString("ERROR_CmdLine_MustUseProjectVersion", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unrecognized command line argument: {0}.
        /// </summary>
        internal static string ERROR_CmdLine_UnrecognizedArg {
            get {
                return ResourceManager.GetString("ERROR_CmdLine_UnrecognizedArg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not connect to the SonarQube server. Check that the URL is correct and that the server is available. URL: {0}.
        /// </summary>
        internal static string ERROR_ConnectionFailed {
            get {
                return ResourceManager.GetString("ERROR_ConnectionFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to parse properties from the environment variable &apos;{0}&apos; because &apos;{1}&apos;..
        /// </summary>
        internal static string ERROR_FailedParsePropertiesEnvVar {
            get {
                return ResourceManager.GetString("ERROR_FailedParsePropertiesEnvVar", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not find a file on the SonarQube server. URL: {0}.
        /// </summary>
        internal static string ERROR_FileNotFound {
            get {
                return ResourceManager.GetString("ERROR_FileNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to At least one property name is missing. Please check that the settings file is valid..
        /// </summary>
        internal static string ERROR_InvalidPropertyName {
            get {
                return ResourceManager.GetString("ERROR_InvalidPropertyName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Descriptor ids must be unique.
        /// </summary>
        internal static string ERROR_Parser_UniqueDescriptorIds {
            get {
                return ResourceManager.GetString("ERROR_Parser_UniqueDescriptorIds", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Execution failed. The specified executable does not exist: {0}.
        /// </summary>
        internal static string ERROR_ProcessRunner_ExeNotFound {
            get {
                return ResourceManager.GetString("ERROR_ProcessRunner_ExeNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to find the analysis settings file &apos;{0}&apos;. Please fix the path to this settings file..
        /// </summary>
        internal static string ERROR_Properties_GlobalPropertiesFileDoesNotExist {
            get {
                return ResourceManager.GetString("ERROR_Properties_GlobalPropertiesFileDoesNotExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to read the analysis settings file &apos;{0}&apos;. Please fix the content of this file..
        /// </summary>
        internal static string ERROR_Properties_InvalidPropertiesFile {
            get {
                return ResourceManager.GetString("ERROR_Properties_InvalidPropertiesFile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The settings file could not be found.
        /// </summary>
        internal static string ERROR_SettingsFileNotFound {
            get {
                return ResourceManager.GetString("ERROR_SettingsFileNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A server certificate could not be validated. Possible cause: you are using a self-signed SSL certificate but the certificate has not been installed on the client machine. Please make sure that you can access {0} without encountering certificate errors..
        /// </summary>
        internal static string ERROR_TrustFailure {
            get {
                return ResourceManager.GetString("ERROR_TrustFailure", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not authorize while connecting to the SonarQube server. Check your credentials and try again..
        /// </summary>
        internal static string ERROR_UnauthorizedConnection {
            get {
                return ResourceManager.GetString("ERROR_UnauthorizedConnection", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unsupported region &apos;{0}&apos;. List of supported regions: &apos;us&apos;. Please check the &apos;sonar.region&apos; property..
        /// </summary>
        internal static string ERROR_UnsupportedRegion {
            get {
                return ResourceManager.GetString("ERROR_UnsupportedRegion", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The name of the SonarQube server could not be resolved. Check the url is correct and that the server is available. Url: {0}.
        /// </summary>
        internal static string ERROR_UrlNameResolutionFailed {
            get {
                return ResourceManager.GetString("ERROR_UrlNameResolutionFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to WARNING: .
        /// </summary>
        internal static string Logger_WarningPrefix {
            get {
                return ResourceManager.GetString("Logger_WarningPrefix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not add &apos;{0}&apos; to the analysis. {1}.
        /// </summary>
        internal static string MSG_AnalysisFileCouldNotBeAdded {
            get {
                return ResourceManager.GetString("MSG_AnalysisFileCouldNotBeAdded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Commencing retry-able operation. Max wait (milliseconds): {0}, pause between tries (milliseconds): {1}.
        /// </summary>
        internal static string MSG_BeginningRetry {
            get {
                return ResourceManager.GetString("MSG_BeginningRetry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;sensitive data removed&gt;.
        /// </summary>
        internal static string MSG_CmdLine_SensitiveCmdLineArgsAlternativeText {
            get {
                return ResourceManager.GetString("MSG_CmdLine_SensitiveCmdLineArgsAlternativeText", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not import the truststore &apos;{0}&apos; with the default password at index {1}. Reason: {2}.
        /// </summary>
        internal static string MSG_CouldNotImportTruststoreWithDefaultPassword {
            get {
                return ResourceManager.GetString("MSG_CouldNotImportTruststoreWithDefaultPassword", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Creating directory: {0}.
        /// </summary>
        internal static string MSG_CreatingDirectory {
            get {
                return ResourceManager.GetString("MSG_CreatingDirectory", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Removing the existing directory: {0}.
        /// </summary>
        internal static string MSG_DeletingDirectory {
            get {
                return ResourceManager.GetString("MSG_DeletingDirectory", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The directory already exists: {0}.
        /// </summary>
        internal static string MSG_DirectoryAlreadyExists {
            get {
                return ResourceManager.GetString("MSG_DirectoryAlreadyExists", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Executing file {0}
        ///  Args: {1}
        ///  Working directory: {2}
        ///  Timeout (ms):{3}
        ///  Process id: {4}.
        /// </summary>
        internal static string MSG_ExecutingFile {
            get {
                return ResourceManager.GetString("MSG_ExecutingFile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Process returned exit code {0}.
        /// </summary>
        internal static string MSG_ExecutionExitCode {
            get {
                return ResourceManager.GetString("MSG_ExecutionExitCode", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Default properties file was found at {0}.
        /// </summary>
        internal static string MSG_Properties_DefaultPropertiesFileFound {
            get {
                return ResourceManager.GetString("MSG_Properties_DefaultPropertiesFileFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Default properties file was not found at {0}.
        /// </summary>
        internal static string MSG_Properties_DefaultPropertiesFileNotFound {
            get {
                return ResourceManager.GetString("MSG_Properties_DefaultPropertiesFileNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Loading analysis properties from {0}.
        /// </summary>
        internal static string MSG_Properties_LoadingPropertiesFromFile {
            get {
                return ResourceManager.GetString("MSG_Properties_LoadingPropertiesFromFile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Retrying....
        /// </summary>
        internal static string MSG_RetryingOperation {
            get {
                return ResourceManager.GetString("MSG_RetryingOperation", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Operation timed out, Elapsed time (ms): {0}.
        /// </summary>
        internal static string MSG_RetryOperationFailed {
            get {
                return ResourceManager.GetString("MSG_RetryOperationFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Operation succeeded. Elapsed time (ms): {0}.
        /// </summary>
        internal static string MSG_RetryOperationSucceeded {
            get {
                return ResourceManager.GetString("MSG_RetryOperationSucceeded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Overwriting the value of environment variable &apos;{0}&apos;. Old value: {1}, new value: {2}.
        /// </summary>
        internal static string MSG_Runner_OverwritingEnvVar {
            get {
                return ResourceManager.GetString("MSG_Runner_OverwritingEnvVar", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Setting environment variable &apos;{0}&apos;. Value: {1}.
        /// </summary>
        internal static string MSG_Runner_SettingEnvVar {
            get {
                return ResourceManager.GetString("MSG_Runner_SettingEnvVar", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Api Url: {0}.
        /// </summary>
        internal static string MSG_ServerInfo_ApiUrlDetected {
            get {
                return ResourceManager.GetString("MSG_ServerInfo_ApiUrlDetected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Is SonarCloud: {0}.
        /// </summary>
        internal static string MSG_ServerInfo_IsSonarCloudDetected {
            get {
                return ResourceManager.GetString("MSG_ServerInfo_IsSonarCloudDetected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Server Url: {0}.
        /// </summary>
        internal static string MSG_ServerInfo_ServerUrlDetected {
            get {
                return ResourceManager.GetString("MSG_ServerInfo_ServerUrlDetected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to sonar.log.level={0} was specified - setting the log verbosity to &apos;DEBUG&apos;.
        /// </summary>
        internal static string MSG_SonarLogLevelWasSpecified {
            get {
                return ResourceManager.GetString("MSG_SonarLogLevelWasSpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to sonar.verbose={0} was specified - setting the log verbosity to &apos;{1}&apos;.
        /// </summary>
        internal static string MSG_SonarVerboseWasSpecified {
            get {
                return ResourceManager.GetString("MSG_SonarVerboseWasSpecified", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timed out after waiting {0} ms for process {1} to complete: it has been terminated, but its child processes may still be running..
        /// </summary>
        internal static string WARN_ExecutionTimedOutKilled {
            get {
                return ResourceManager.GetString("WARN_ExecutionTimedOutKilled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timed out after waiting {0} ms for process {1} to complete: it could not be terminated and might still be running..
        /// </summary>
        internal static string WARN_ExecutionTimedOutNotKilled {
            get {
                return ResourceManager.GetString("WARN_ExecutionTimedOutNotKilled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot detect the operating system. {0}.
        /// </summary>
        internal static string WARN_FailedToReadFile {
            get {
                return ResourceManager.GetString("WARN_FailedToReadFile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The arguments &apos;sonar.host.url&apos; and &apos;sonar.scanner.sonarcloudUrl&apos; are both set. Please set only &apos;sonar.scanner.sonarcloudUrl&apos;..
        /// </summary>
        internal static string WARN_HostUrlAndSonarcloudUrlSet {
            get {
                return ResourceManager.GetString("WARN_HostUrlAndSonarcloudUrlSet", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified value `{0}` for `{1}` cannot be parsed. The default value of {2}s will be used. Please remove the parameter or specify the value in seconds, greater than 0..
        /// </summary>
        internal static string WARN_InvalidTimeoutValue {
            get {
                return ResourceManager.GetString("WARN_InvalidTimeoutValue", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The {0} parameter is set to &quot;{1}&quot;. The setting will be overriden by one or more of the properties {2}, {3}, or {4}..
        /// </summary>
        internal static string WARN_RegionIsOverriden {
            get {
                return ResourceManager.GetString("WARN_RegionIsOverriden", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Expecting the sonar.verbose property to be set to either &apos;true&apos; or &apos;false&apos; (case-sensitive) but it was set to &apos;{0}&apos;..
        /// </summary>
        internal static string WARN_SonarVerboseNotBool {
            get {
                return ResourceManager.GetString("WARN_SonarVerboseNotBool", resourceCulture);
            }
        }
    }
}
