﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SonarScanner.MSBuild.PreProcessor.Roslyn; 
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
internal class RoslynResources {
    
    private static global::System.Resources.ResourceManager resourceMan;
    
    private static global::System.Globalization.CultureInfo resourceCulture;
    
    [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal RoslynResources() {
    }
    
    /// <summary>
    ///   Returns the cached ResourceManager instance used by this class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static global::System.Resources.ResourceManager ResourceManager {
        get {
            if (object.ReferenceEquals(resourceMan, null)) {
                global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SonarScanner.MSBuild.PreProcessor.Roslyn.RoslynResources", typeof(RoslynResources).Assembly);
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
    ///   Looks up a localized string similar to Cache hit: using plugin files from {0}.
    /// </summary>
    internal static string EAI_CacheHit {
        get {
            return ResourceManager.GetString("EAI_CacheHit", resourceCulture);
        }
    }
    
    /// <summary>
    ///   Looks up a localized string similar to Cache miss: plugin files were not found in the local cache.
    /// </summary>
    internal static string EAI_CacheMiss {
        get {
            return ResourceManager.GetString("EAI_CacheMiss", resourceCulture);
        }
    }
    
    /// <summary>
    ///   Looks up a localized string similar to Fetching resource for plugin: {0}, version {1}. Resource: {2}.
    /// </summary>
    internal static string EAI_FetchingPluginResource {
        get {
            return ResourceManager.GetString("EAI_FetchingPluginResource", resourceCulture);
        }
    }
    
    /// <summary>
    ///   Looks up a localized string similar to Installing required Roslyn analyzers....
    /// </summary>
    internal static string EAI_InstallingAnalyzers {
        get {
            return ResourceManager.GetString("EAI_InstallingAnalyzers", resourceCulture);
        }
    }
    
    /// <summary>
    ///   Looks up a localized string similar to Local analyzer cache: {0}.
    /// </summary>
    internal static string EAI_LocalAnalyzerCache {
        get {
            return ResourceManager.GetString("EAI_LocalAnalyzerCache", resourceCulture);
        }
    }
    
    /// <summary>
    ///   Looks up a localized string similar to No plugins were specified.
    /// </summary>
    internal static string EAI_NoPluginsSpecified {
        get {
            return ResourceManager.GetString("EAI_NoPluginsSpecified", resourceCulture);
        }
    }
    
    /// <summary>
    ///   Looks up a localized string similar to Plugin resource not found: {0}, version {1}. Resource: {2}..
    /// </summary>
    internal static string EAI_PluginResourceNotFound {
        get {
            return ResourceManager.GetString("EAI_PluginResourceNotFound", resourceCulture);
        }
    }
    
    /// <summary>
    ///   Looks up a localized string similar to Processing plugin: {0} version {1}.
    /// </summary>
    internal static string EAI_ProcessingPlugin {
        get {
            return ResourceManager.GetString("EAI_ProcessingPlugin", resourceCulture);
        }
    }
}
