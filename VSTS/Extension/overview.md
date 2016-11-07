SonarQube is an open platform to manage code quality. The platform covers the Seven Axes of Quality, also known as [Developers’ Seven Deadly Sins][dev7ds]: Duplications, Coding standards, Lack of coverage, Potential bugs, Complexity, Documentation and Design.

## SonarQube build tasks
----------
Use the **SonarQube analysis build tasks** in your continuous integration builds to understand the technical debt in your projects. This extension provides integration with MSBuild and Maven builds.

### SonarQube MSBuild integration

* The first of these tasks is used to define a step that start the SonarQube analysis, before any MSBuild build steps. The Begin Analysis task contacts the SonarQube server to retrieve the quality profile, and dynamically produces rulesets to be applied during the static analysis. It also sets things up so that the following MSBuild steps capture project data required to configure the analysis.

* The End Analysis task finalizes the analysis (computation of the clones, metrics, and analysis for languages other than .Net), and sends the analysis results to the SonarQube server. It should be executed after the “Visual Studio Test” task step if you want SonarQube to show code coverage data. In any case, it should be run after the “Visual Studio Build” step.

[dev7ds]: <http://docs.sonarqube.org/display/HOME/Developers'+Seven+Deadly+Sins>