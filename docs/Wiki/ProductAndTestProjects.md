This page refers to the SonarScanner for .NET (S4NET), also known as SonarScanner for MSBuild (S4MSB). You can read more about how to install and use it in the ["SonarScanner for .NET documentation"](https://redirect.sonarsource.com/doc/install-configure-scanner-msbuild.html).

### Analyzer references

* the `SonarAnalyzer.CSharp`/`SonarAnalyzer.VB` analyzers will be added to the build even if those analyzers are not referenced in the MSBuild project (S4NET will add references to those analyzers on the fly during the build). The set of rules that is executed is determined by the project type and Quality Profile for the Sonar project in SonarQube/SonarCloud.
* any third-party analyzers referenced by the MSBuild project will be executed as part of the build (e.g. via NuGet package references). Issues from those analyzers will be uploaded to SonarQube/SonarCloud as [External Issues](https://sonarcloud.io/documentation/analysis/external-issues/). You can configure the third-party analyzer rules as normal using a `Ruleset`. During the build, S4NET will merge your custom ruleset with a ruleset generated from the Quality Profile. In the event of a conflict, the settings in the generated ruleset take precedence.

### Differences between analysis of test projects and product projects

The S4NET analyses MSBuild projects containing product code differently from projects containing test code.

#### Analysis of product projects

* analysis rules will be run against product projects, and the issues raised will be uploaded to SonarQube/SonarCloud.
* only rules related to product code will execute.
* metrics and Lines of Code ("LOC") limit for commercial versions of SonarQube or private SonarCloud projects are calculated.

#### Analysis of test projects

* analysis rules will be run against test projects, unless excluded (see below), and the issues raised will be uploaded to SonarQube/SonarCloud.
* only rules related to test code will execute.
* test projects do not count towards to the Lines of Code ("LOC") limit for commercial versions of SonarQube or private SonarCloud projects.
* metrics are not calculated for test projects, although syntax colourisation and symbol highlighting are supported.

#### Analysis of excluded test projects

* test projects are excluded from analysis on SonarQube up to version 8.8
* test projects can be excluded from analysis on SonarCloud and SonarQube from version 8.9 by adding `/d:sonar.dotnet.excludeTestProjects=true` S4NET parameter.
* analysis rules are not run against excluded test projects i.e. no issues will be reported to SonarQube/SonarCloud. This is the case even if the test project references third-party NuGet analyzer packages - those analyzers will not be executed.
* excluded test projects do not count towards to the Lines of Code ("LOC") limit for commercial versions of SonarQube or private SonarCloud projects.
* metrics are not calculated for test projects, although syntax colourisation and symbol highlighting are supported.

#### Analysis of projects excluded with `<SonarQubeExclude>true</SonarQubeExclude>`

* any project can be excluded from analysis using `<SonarQubeExclude>true</SonarQubeExclude>`.
* no analyzer rules, metrics, syntax colourisation nor symbol highlighting will be calculated.

### Project categorisation
S4NET decides whether an MSBuild project contains test code or product code by looking the data in the project file. The categorisation is done at project level i.e. either the code will be treated as all product code or all test code. It is not possible to treat some of the code as test code and some as product code.

S4NET will treat the project as containing test code if any of the following are true:
* the project file contains the `MSTest` `ProjectTypeGuid`: `3AC096D0-A1C2-E12C-1390-A8335801FDAB`
* the project file contains the legacy Service GUID `{82A7F48D-3B50-4B1E-B82E-3ADA8210C358}` that is added by the Test Explorer to mark a project as a containing tests
* the project file contains the `ProjectCapability` `TestContainer` (for new SDK-style MSBuild projects). Note: this property can be set indirectly as a result of importing a NuGet package. See below for more information.
* the project file name matches the RegEx set in the deprecated property sonar.msbuild.testProjectPattern

There are a few special project types for which MSBuild will create and build a temporary project (e.g. Microsoft Fakes, WPF) as part of the "main" build. Such temporary projects are ignored by S4NET. The "main" project will be categorised and treated as normal.

#### Importing third-party unit test NuGet packages
It is possible for a project to be classified as a test project as a result of it referencing a third-party unit test NuGet package. This is because packages can add MSBuild targets into the build.

For example, if your project references e.g. XUnit as follows:
```
<PackageReference Include="xunit" Version="2.4.1" />
```
... then the XUnit package will add a target to the build containing the following property assignment:
```
  <ItemGroup>
    <ProjectCapability Include="TestContainer" />
  </ItemGroup>
```
This will cause your project to be classified as a test project, and the MSBuild output will contain a message like the following:

```
Sonar: (MyProject.csproj) project has the ProjectCapability 'TestContainer' -> test project
```

### Explicit setting the project type
It is possible to explicitly mark a project as being a test/product project by setting the MSBuild property `SonarQubeTestProject` to `true` or `false` e.g.

```
<PropertyGroup>
  <!-- Project is not a test project -->
  <SonarQubeTestProject>false</SonarQubeTestProject>
</PropertyGroup>
```
Setting this property takes precedence over the default project categorisation behaviour.


### Understanding why a project was categorised in a particular way
S4NET writes information about the project categorisation to the output log. The information will appear in logs at `Normal` verbosity or greater.
NB this logging was added in S4NET [v4.7](https://github.com/SonarSource/sonar-scanner-msbuild/releases/tag/4.7.0.2295).

#### Examples
The following examples are taken from the analysis of the [SonarLint for Visual Studio](https://github.com/sonarsource/sonarlint-visualstudio) repo:

```
...
SonarQubeCategoriseProject:
  Sonar: (Progress.csproj) Categorizing project as test or product code...
  Sonar: (Progress.csproj) Project categorized. SonarQubeTestProject=False
...

...
SonarQubeCategoriseProject:
  Sonar: (Progress.TestFramework.csproj) Categorizing project as test or product code...
  Sonar: (Progress.TestFramework.csproj) SonarQubeTestProject has been set explicitly to true
  Sonar: (Progress.TestFramework.csproj) Project categorized. SonarQubeTestProject=true
...

...
SonarQubeCategoriseProject:
  Sonar: (SonarQube.Client.Tests.csproj) Categorizing project as test or product code...
  Sonar: (SonarQube.Client.Tests.csproj) project is evaluated as a test project based on the project name
  Sonar: (SonarQube.Client.Tests.csproj) Project categorized. SonarQubeTestProject=True
...
```
