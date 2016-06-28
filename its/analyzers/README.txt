TestAnalyzers projects implements a custom Roslyn analyzers, for testing purposes.

Some documentation about writing analyzers:
https://github.com/dotnet/roslyn#get-started
https://msdn.microsoft.com/en-us/magazine/dn879356


The project generates a nuget package, which can be tested directly with Visual Studio, either in a sandbox or in production.

From the nuget package, a Java SonarQube plugin needs to be generated. The plugin will create the corresponding rules in the SonarQube platform 
and make available the Roslyn analyzers as static resources to the Scanner for MSBuild.

For information on how to generate the plugin: https://github.com/SonarSource-VisualStudio/sonarqube-roslyn-sdk

This folder already contains a generated SonarQube plugin.