The assemblies in this folder are required for the Scanner for MSBuild when being called from
TFS XAML builds (now deprecated by Microsoft). They are only referenced by
SonarQube.TeamBuild.Integration.Classic.csproj project.

The assemblies are not distributed with the Scanner for MSBuild. They are included here so that
we can control exactly which version is used in the build and CI builds.
We are deliberately using an old version of the TFS assemblies to maintain compatiblity
with TFS 2013 onwards (there is not a NuGet package for this version of the TFS Object Model).

At runtime, the end user will have to install the TFS2013 Object Model on their XAML build agent.
The installer for the OM is available at https://visualstudiogallery.msdn.microsoft.com/19311823-5262-4e63-a586-2283384ae3bf
