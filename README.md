### License

Copyright 2016-2018 SonarSource.

Licensed under the [GNU Lesser General Public License, Version 3.0](http://www.gnu.org/licenses/lgpl.txt)

## Build status
[![Build status](https://ci.appveyor.com/api/projects/status/ik8mfx97hnvkhjfm/branch/master?svg=true)](https://ci.appveyor.com/project/SonarSource/sonar-scanner-msbuild/branch/master)

## Documentation

http://redirect.sonarsource.com/doc/msbuild-sq-runner.html

## Issue Tracker

https://jira.sonarsource.com/browse/SONARMSBRU

## Building

Requirements:

- Visual Studio 2015
- [Team Foundation Server 2013 Update 4 Object Model Installer](https://visualstudiogallery.msdn.microsoft.com/19311823-5262-4e63-a586-2283384ae3bf)
- [MsBuild 12](https://www.microsoft.com/en-us/download/confirmation.aspx?id=40760)

Install NuGet packages: right click on the solution in Visual Studio, and select **Restore NuGet Packages**.
Alternatively, using the [nuget.exe](https://dist.nuget.org/index.html) on the command line:

    nuget restore path\to\solution.sln
    
Or inside the project's directory, simply `nuget restore` without parameters.
