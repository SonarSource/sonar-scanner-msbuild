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
- Make sure an issue exists in this repository. Otherwise, create an issue to describe the necessity of the change.
- Create a pull request in this repository and link it to the issue it solves (`Fixes #...`).

Please make sure that you follow our [code style](https://github.com/SonarSource/sonar-dotnet/blob/master/docs/coding-style.md).

Before submitting the PR, make sure all tests are passing (all checks must be green).

## Running tests

### Unit Tests

You can run the Unit Tests via the Test Explorer of Visual Studio.

### Integration Tests

1. Go to `PATH_TO_CLONED_REPOSITORY`
1. Run `powershell`
1. Run `.\scripts\ci-build.ps1`
1. Open the `PATH_TO_CLONED_REPOSITORY\its` directory using your favourite IDE for java (e.g. IntelliJ IDEA Community Edition)
1. Run the ITs
