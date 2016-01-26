//-----------------------------------------------------------------------
// <copyright file="AnalyzerInstallerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet;
using SonarQube.TeamBuild.PreProcessor.Roslyn;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TestUtilities;

namespace SonarQube.TeamBuild.PreProcessor.Tests
{
    [TestClass]
    public class AnalyzerInstallerTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void AnalyzerInstaller_InstallMultiFilePackage_Succeeds()
        {
            // Arrange

            // Create dummy NuGet package containing multiple assemblies
            string fakeRemotePackageSource = TestUtils.CreateTestSpecificFolder(this.TestContext, "fakeNuGetSource");
            LocalPackageRepository fakeRemoteRepo = new LocalPackageRepository(fakeRemotePackageSource);

            PackageBuilder builder = CreatePackageBuilder("simplepackage1", "1.0.0");
            AddDummyFile(builder, "test.txt"); // not an assembly
            AddDummyFile(builder, "\\analyzer\\bbb.dll.xxx"); // not an assembly
            AddDummyFile(builder, "\\analyzer\\aaa.dll");
            AddDummyFile(builder, "\\supporting\\bbb.DLL");

            CreateAndInstallPackage(fakeRemoteRepo, builder);

            IEnumerable<NuGetPackageInfo> packages = new NuGetPackageInfo[] {
                new NuGetPackageInfo() { Id = "simplepackage1", Version = "1.0.0" }
            };

            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "localInstallCache");
            AnalyzerInstaller testSubject = new AnalyzerInstaller(fakeRemotePackageSource, localCacheDir, new TestLogger());

            // Act
            IEnumerable<string> assemblyPaths = testSubject.InstallAssemblies(packages);

            // Assert
            AssertExpectedAssembliesReturned(assemblyPaths,
                "\\analyzer\\aaa.dll",
                "\\supporting\\bbb.dll");
        }

        [TestMethod]
        public void AnalyzerInstaller_InstallMultiplePackages_Succeeds()
        {
            // Arrange

            // Create dummy NuGet package containing multiple assemblies
            string fakeRemotePackageSource = TestUtils.CreateTestSpecificFolder(this.TestContext, "fakeNuGetSource");
            LocalPackageRepository fakeRemoteRepo = new LocalPackageRepository(fakeRemotePackageSource);
            
            CreateAndInstallPackageWithDummyContent(fakeRemoteRepo, "package1", "1.0-rc1", "package1.dll");
            CreateAndInstallPackageWithDummyContent(fakeRemoteRepo, "package2", "22.2", "package2.dll");

            IEnumerable<NuGetPackageInfo> packages = new NuGetPackageInfo[] {
                new NuGetPackageInfo() { Id = "package1", Version = "1.0-rc1" },
                new NuGetPackageInfo() { Id = "package2", Version = "22.2" }
            };

            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "localInstallCache");
            AnalyzerInstaller testSubject = new AnalyzerInstaller(fakeRemotePackageSource, localCacheDir, new TestLogger());

            // Act
            IEnumerable<string> assemblyPaths = testSubject.InstallAssemblies(packages);

            // Assert
            AssertExpectedAssembliesReturned(assemblyPaths,
                "package1.dll",
                "package2.dll");
        }

        [TestMethod]
        public void AnalyzerInstaller_MissingPackage_Fails()
        {
            // Arrange

            // Create dummy NuGet package with the correct id but different version
            string fakeRemotePackageSource = TestUtils.CreateTestSpecificFolder(this.TestContext, "fakeNuGetSource");
            LocalPackageRepository fakeRemoteRepo = new LocalPackageRepository(fakeRemotePackageSource);
            CreateAndInstallPackageWithDummyContent(fakeRemoteRepo, "missing.package", "1.0.1", "dummy.txt");

            IEnumerable<NuGetPackageInfo> packages = new NuGetPackageInfo[] {
                new NuGetPackageInfo() { Id = "missing.package", Version = "1.0.0" }
            };

            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "localInstallCache");
            AnalyzerInstaller testSubject = new AnalyzerInstaller(fakeRemotePackageSource, localCacheDir, new TestLogger());

            // Act and assert
            AssertException.Expects<InvalidOperationException>(() => testSubject.InstallAssemblies(packages));
        }

        [TestMethod]
        public void AnalyzerInstaller_NoPackagesSpecified_Succeeds()
        {
            // Arrange

            // No need to create dummy packages
            string fakeRemotePackageSource = TestUtils.CreateTestSpecificFolder(this.TestContext, "fakeNuGetSource");

            IEnumerable<NuGetPackageInfo> packages = new NuGetPackageInfo[] { };

            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "localInstallCache");
            AnalyzerInstaller testSubject = new AnalyzerInstaller(fakeRemotePackageSource, localCacheDir, new TestLogger());

            // Act
            IEnumerable<string> assemblyPaths = testSubject.InstallAssemblies(packages);

            // Assert
            AssertExpectedAssembliesReturned(assemblyPaths /* none */ );
        }

        [TestMethod]
        [Ignore, WorkItem(209)] // https://jira.sonarsource.com/browse/SONARMSBRU-209 - should install analyzer dependencies
        public void AnalyzerInstaller_InstallPackageWithDependencies_Succeeds()
        {
            // Arrange

            // Create dummy NuGet packages
            string fakeRemotePackageSource = TestUtils.CreateTestSpecificFolder(this.TestContext, "fakeNuGetSource");
            LocalPackageRepository fakeRemoteRepo = new LocalPackageRepository(fakeRemotePackageSource);

            IPackage level3a = CreateAndInstallPackageWithDummyContent(fakeRemoteRepo, "level3a", "1.1", "\\file\\level3.dll");
            IPackage level3b = CreateAndInstallPackageWithDummyContent(fakeRemoteRepo, "level3b", "1.3", "\\file\\not a dll file so should be ignored");
            IPackage level2 = CreateAndInstallPackageWithDummyContent(fakeRemoteRepo, "level2a", "2.0", "\\aaa\\bbb\\level2.dll", level3a, level3b);
            CreateAndInstallPackageWithDummyContent(fakeRemoteRepo, "root.package", "2.1", "\\analyzer\\root.dll", level2);

            IEnumerable<NuGetPackageInfo> packages = new NuGetPackageInfo[] {
                new NuGetPackageInfo() { Id = "root.package", Version = "2.1" }
            };

            string localCacheDir = TestUtils.CreateTestSpecificFolder(this.TestContext, "localInstallCache");
            AnalyzerInstaller testSubject = new AnalyzerInstaller(fakeRemotePackageSource, localCacheDir, new TestLogger());

            // Act
            IEnumerable<string> assemblyPaths = testSubject.InstallAssemblies(packages);

            // Assert
            AssertExpectedAssembliesReturned(assemblyPaths,
                "\\analyzer\\root.dll",
                "\\aaa\\bbb\\level2.dll",
                "\\file\\level3.dll");
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Creates and installs a package with a single content file.
        /// Optionally sets the dependencies for the package
        /// </summary>
        private IPackage CreateAndInstallPackageWithDummyContent(LocalPackageRepository localRepo, string packageId, string packageVersion, string contentFileTargetPath, params IPackage[] dependsOn)
        {
            PackageBuilder builder = new PackageBuilder();
            ManifestMetadata metadata = CreateNuGetManifest(packageId, packageVersion);
            builder.Populate(metadata);

            if (dependsOn.Length > 0)
            {
                AddDependencySet(metadata, dependsOn);
            }

            AddDummyFile(builder, contentFileTargetPath);

            return CreateAndInstallPackage(localRepo, builder);
        }

        private static IPackage CreateAndInstallPackage(LocalPackageRepository localRepo, PackageBuilder builder)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(builder.Id), "Package id should be set");
            Debug.Assert(builder.Version != null, "Package version should be set");

            // Write the next package to the appropriate folder under the local repository
            // i.e. the location to which it would be installed
            string fileName = localRepo.PathResolver.GetPackageFileName(builder.Id, builder.Version);
            string destinationName = Path.Combine(localRepo.Source, fileName);
            using (Stream fileStream = File.Open(destinationName, FileMode.OpenOrCreate))
            {
                builder.Save(fileStream);
            }

            // Check we can retrieve the package from the local repository
            IPackage newPackage = localRepo.FindPackage(builder.Id, builder.Version);
            Assert.IsNotNull(newPackage, "Test setup error: failed to retrieve the test NuGet package. Id: {0}, Version: {1}", builder.Id, builder.Version);
            return newPackage;
        }

        private static PackageBuilder CreatePackageBuilder(string id, string version)
        {
            PackageBuilder builder = new PackageBuilder();
            ManifestMetadata metadata = CreateNuGetManifest(id, version);
            builder.Populate(metadata);
            return builder;
        }

        private static ManifestMetadata CreateNuGetManifest(string id, string version)
        {
            ManifestMetadata metadata = new ManifestMetadata()
            {
                Authors = "dummy author",
                Version = version,
                Id = id,
                Description = "dummy description",
                LicenseUrl = "http://choosealicense.com/"
            };
            return metadata;
        }

        private void AddDummyFile(PackageBuilder builder, string targetPath)
        {
            string dummyContentFilesDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "dummyContent");
            string fileName = "dummy" + System.Guid.NewGuid().ToString() + ".txt";

            string fullFilePath = TestUtils.CreateTextFile(dummyContentFilesDir, fileName, "dummy content");

            PhysicalPackageFile file = new PhysicalPackageFile();
            file.SourcePath = fullFilePath;
            file.TargetPath = targetPath;
            builder.Files.Add(file);
        }

        private static void AddDependencySet(ManifestMetadata metadata, params IPackage[] dependsOn)
        {
            if (metadata.DependencySets == null)
            {
                metadata.DependencySets = new List<ManifestDependencySet>();
            }

            ManifestDependencySet newDependencySet = new ManifestDependencySet();
            newDependencySet.Dependencies = new List<ManifestDependency>();
            foreach (IPackage dependency in dependsOn)
            {
                newDependencySet.Dependencies.Add(new ManifestDependency()
                {
                    Id = dependency.Id,
                    Version = dependency.Version.ToString(),
                });
            }

            metadata.DependencySets.Add(newDependencySet);
        }

        #endregion

        #region Checks

        private static void AssertExpectedAssembliesReturned(IEnumerable<string> actualPaths, params string[] partialAssemblyNames)
        {
            Assert.IsNotNull(actualPaths, "Returned assembly paths should not be null");
            foreach (string partialName in partialAssemblyNames)
            {
                AssertExpectedAssemblyReturned(partialName, actualPaths);
            }
            Assert.AreEqual(partialAssemblyNames.Length, actualPaths.Count(), "Unexpected number of assembly paths returned");
        }

        private static void AssertExpectedAssemblyReturned(string partialAssemblyName, IEnumerable<string> actual)
        {
            Assert.IsTrue(actual.Any(a => a.EndsWith(partialAssemblyName, System.StringComparison.OrdinalIgnoreCase)),
                "Expected assembly not found: {0}", partialAssemblyName);
        }

        #endregion

    }
}
