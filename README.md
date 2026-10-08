<!-- Sonar Marketing hosts these approved brand assets on its Kentico Kontent CDN (assets-eu-01.kc-usercontent.com). Shared URLs are intentional; consult Marketing before replacing them. -->
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://assets-eu-01.kc-usercontent.com/ef593040-b591-0198-9506-ed88b30bc023/a23fc7ba-23f0-489a-829d-ed88c0748521/Sonar_Logo_Dark%20Backgrounds.svg">
    <img src="https://assets-eu-01.kc-usercontent.com/ef593040-b591-0198-9506-ed88b30bc023/82c13eba-d95c-4bb8-8007-7ce77c14e043/Sonar_Logo_Light%20Backgrounds.svg" alt="Sonar logo" width="400">
  </picture>
</p>

[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=sonarscanner-msbuild&metric=alert_status)](https://sonarcloud.io/dashboard?id=sonarscanner-msbuild)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=sonarscanner-msbuild&metric=coverage)](https://sonarcloud.io/component_measures?id=sonarscanner-msbuild&metric=coverage)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](#contributing)

<!-- sonar-marketing:start -->
<!-- Marketing maintains this section. For wording changes, consult the relevant Product Marketing Manager (PMM). Repository CODEOWNERS review accuracy and merge changes. -->

# SonarScanner for .NET

SonarScanner for .NET is a tool to analyze .NET projects with SonarQube. It integrates SonarQube analysis into projects built with MSBuild or the `dotnet` command and sends the results to SonarQube Server or SonarQube Cloud.

Learn more about the [SonarQube product family](https://www.sonarsource.com/products/sonarqube/), then choose the scanner distribution that fits your build environment below.

<!-- sonar-marketing:end -->

SonarScanner for .NET is distributed as a

* [Standalone tool](https://github.com/SonarSource/sonar-scanner-msbuild)
* [Azure DevOps extension](https://github.com/SonarSource/sonar-scanner-azdo)
* [Jenkins plugin](https://github.com/SonarSource/sonar-scanner-jenkins)

For more info please look at our documentation page

* [On SonarQube Server](https://docs.sonarqube.org/latest/analysis/scan/sonarscanner-for-msbuild/)
* [On SonarQube Cloud](https://docs.sonarcloud.io/advanced-setup/ci-based-analysis/sonarscanner-for-net/)

and at our user guides

* [Generate Test Coverage Reports for C#, VB.NET](https://community.sonarsource.com/t/9871)
* [How to find logs about importing code coverage ](https://community.sonarsource.com/t/73317)
* [Troubleshooting guide for .NET code coverage import](https://community.sonarsource.com/t/37151)
* [The Sonar guide for investigating the performance of .NET analysis](https://community.sonarsource.com/t/47279/)
* [Configuration of WarningsAsErrors for .NET build](https://community.sonarsource.com/t/32393)

## Contributing

Check out the [contributing](CONTRIBUTING.md) page to see the best places to log issues and start discussions.

## Security Issues

If you believe you have discovered a security vulnerability in Sonar's products, please check [this document](./SECURITY.md)

## License

Copyright (C) SonarSource Sàrl.

Licensed under the [GNU Lesser General Public License, Version 3.0](http://www.gnu.org/licenses/lgpl.txt)
