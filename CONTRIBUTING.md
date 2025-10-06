# Contributing to Sonar Scanner for .NET

There are many ways you can contribute to the SonarScanner for .NET project, some very easy and others more
involved. We want to be friendly and welcoming to all potential contributors, so we ask that everyone involved abide
by some simple guidelines outlined in our [Code of Conduct](./CODE_OF_CONDUCT.md).

## Easy Ways to Contribute

One of the easiest ways to contribute is to participate in discussions and discuss issues on our
[Community Forum](https://community.sonarsource.com/).

## General feedback and discussions?

Even if you plan to implement a new rule or fix one by yourself and then submit it back to the community, please start
by creating a new thread on our [Community Forum](https://community.sonarsource.com/) to get some 
early feedback on this idea.

## Developing with Eclipse or IntelliJ

When working with Eclipse or IntelliJ please follow the [sonar guidelines](https://github.com/SonarSource/sonar-developer-toolset)

## Pull Request (PR)

To submit a contribution:
- Make sure an issue exists in [SCAN4NET project](https://sonarsource.atlassian.net/browse/SCAN4NET). Otherwise, create a community thread first.
- Create a pull request in this repository and prefix the PR title with the issue ID.

Please make sure that you follow our [code style](https://github.com/SonarSource/sonar-dotnet/blob/master/docs/coding-style.md).

Before submitting the PR, make sure all tests are passing (all checks must be green).

## Build

- Install Developer Pack for .NET Framework 4.6.2 from [.NET SDKs for Visual Studio](https://aka.ms/msbuild/developerpacks)

- If you are Sonar internal you need to set these environment variables:
    1. `ARTIFACTORY_USER`: your repox.jfrog username (see e.g. `orchestrator.properties`)
    1. `ARTIFACTORY_PASSWORD`: the identity token for repox.jfrog

- If you are an external contributor you'll have to delete the `NuGet.config` file.

## Running tests

### Unit Tests

You can run the Unit Tests via the Test Explorer of Visual Studio.

### Integration Tests

1. Go to `PATH_TO_CLONED_REPOSITORY`
1. Run `powershell`
1. Run `.\scripts\its-build.ps1`
1. Open the `PATH_TO_CLONED_REPOSITORY\its` directory using your favorite IDE for Java (e.g. IntelliJ IDEA Community Edition)
1. Run the ITs

#### SonarCloud ITs prerequisites

In order to be able to run the ITs for SonarCloud the following environment variables need to be set:
- SONARCLOUD_URL
- SONARCLOUD_ORGANIZATION
- SONARCLOUD_PROJECT_KEY
- SONARCLOUD_PROJECT_TOKEN

In our CI/CD pipeline, we use the following:
- SONARCLOUD_URL=https://sc-staging.io
- SONARCLOUD_ORGANIZATION=team-lang-dotnet
- SONARCLOUD_PROJECT_KEY=team-lang-dotnet_incremental-pr-analysis
- SONARCLOUD_PROJECT_TOKEN=[user-token]

These can be set either on the operating system or your preferred IDE test run configuration.
