//-----------------------------------------------------------------------
// <copyright file="AnalyzerInstaller.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using NuGet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SonarQube.TeamBuild.PreProcessor.Roslyn
{
    public class AnalyzerInstaller : IAnalyzerInstaller
    {
        private const string DefaultNuGetPackageSource = "https://www.nuget.org/api/v2/";

        private readonly string packageSource;
        private readonly string localCacheDirectory;
        private readonly Common.ILogger logger;

        private IPackageManager manager;
        private IPackageRepository remoteRepository;

        public AnalyzerInstaller(Common.ILogger logger)
            : this(DefaultNuGetPackageSource, GetLocalCacheDirectory(), logger)
        {
        }

        /// <summary>
        /// Constructor for testing
        /// </summary>
        public AnalyzerInstaller(string packageSource, string localCacheDirectory, Common.ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(packageSource))
            {
                throw new ArgumentNullException("packageSource");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (string.IsNullOrWhiteSpace(localCacheDirectory))
            {
                throw new ArgumentNullException("localCacheDirectory");
            }

            this.packageSource = packageSource;
            this.localCacheDirectory = localCacheDirectory;
            this.logger = logger;
        }

        #region IAnalyzerInstaller

        /// <summary>
        /// Installs the specified packages and the dependencies needed to run them
        /// </summary>
        /// <param name="packages">The list of packages to install</param>
        /// <returns>The list of paths of the installed assemblies</returns>
        public IEnumerable<string> InstallAssemblies(IEnumerable<NuGetPackageInfo> packages)
        {
            if (packageSource == null)
            {
                throw new ArgumentNullException("packageSource");
            }

            if (!packages.Any())
            {
                this.logger.LogInfo(RoslynResources.NoAnalyzerPackages);
                return Enumerable.Empty<string>();
            }

            this.logger.LogInfo(RoslynResources.InstallingPackages);

            this.logger.LogDebug(RoslynResources.CreatingRepository, this.packageSource);
            this.remoteRepository = PackageRepositoryFactory.Default.CreateRepository(this.packageSource);

            // Create the local NuGet cache
            Directory.CreateDirectory(this.localCacheDirectory);
            this.manager = new PackageManager(remoteRepository, this.localCacheDirectory);
            this.manager.Logger = new NuGetLoggerAdapter(this.logger);
            
            ISet<string> assemblyPaths = new HashSet<string>();

            foreach(NuGetPackageInfo package in packages)
            {
                ProcessPackage(package, assemblyPaths);
            }

            this.logger.LogInfo(RoslynResources.PackagesInstalled);

            return assemblyPaths;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// We want the NuGet cache to be in a well-known location so we can re-use packages that have
        /// already been installed (although this won't help for e.g. hosted build agents
        /// </summary>
        private static string GetLocalCacheDirectory()
        {
            string localCache = Path.Combine(Path.GetTempPath(), ".sonarqube", ".nuget");
            return localCache;
        }

        private void ProcessPackage(NuGetPackageInfo packageInfo, ISet<string> assemblyPaths)
        {
            Debug.Assert(packageInfo != null, "Not expecting the package info to be null");

            IPackage package = FetchPackage(packageInfo.Id, packageInfo.Version);
            if (package != null)
            {
                IEnumerable<string> filesFromPackage = GetFilesFromPackage(package);
                assemblyPaths.AddRange(filesFromPackage);
            }
        }

        /// <summary>
        /// Attempts to download a NuGet package with the specified id and version
        /// </summary>
        private IPackage FetchPackage(string packageId, string packageVersion)
        {
            SemanticVersion version = new SemanticVersion(packageVersion);

            try
            {
                logger.LogDebug(RoslynResources.LocatingPackage, packageId, version);
                
                // "InstallPackage" is a no-op if the package already exists in the local cache
                this.manager.InstallPackage(packageId, version, ignoreDependencies: false, allowPrereleaseVersions: true);
                this.logger.LogDebug(RoslynResources.PackageInstalled);
            }
            catch (InvalidOperationException e)
            {
                logger.LogError(RoslynResources.PackageInstallFailed, e.Message);
                throw;
            }

            IPackage package = this.manager.LocalRepository.FindPackage(packageId, version);
            Debug.Assert(package != null, "Package should be located as it has just been installed");

            return package;
        }
        
        private IEnumerable<string> GetFilesFromPackage(IPackage package)
        {
            Debug.Assert(this.manager.FileSystem != null);
            Debug.Assert(this.manager.PathResolver != null);
            string packageDirectory = this.manager.FileSystem.GetFullPath(this.manager.PathResolver.GetPackageDirectory(package));

            Debug.Assert(Directory.Exists(packageDirectory), "Expecting the package directory to exist: {0}", packageDirectory);
            string[] files = Directory.GetFiles(packageDirectory, "*.dll", SearchOption.AllDirectories);
            return files;
        }

        #endregion

        private class NuGetLoggerAdapter : NuGet.ILogger
        {
            private readonly Common.ILogger logger;

            public NuGetLoggerAdapter(Common.ILogger logger)
            {
                if (logger == null)
                {
                    throw new ArgumentNullException("logger");
                }
                this.logger = logger;
            }

            public void Log(MessageLevel level, string message, params object[] args)
            {
                switch (level)
                {
                    case MessageLevel.Debug:
                        this.logger.LogDebug(message, args);
                        break;
                    case MessageLevel.Error:
                        this.logger.LogError(message, args);
                        break;
                    case MessageLevel.Warning:
                        this.logger.LogWarning(message, args);
                        break;
                    default:
                        this.logger.LogInfo(message, args);
                        break;
                }
            }

            public FileConflictResolution ResolveFileConflict(string message)
            {
                this.logger.LogDebug(RoslynResources.ResolveFileConflict, message);
                return FileConflictResolution.Ignore;
            }
        }
    }
}
