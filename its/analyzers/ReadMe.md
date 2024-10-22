# Support for custom analyzers

Both the jar and the rules.template.xml files were generated with the SonarQube Roslyn SDK tool based on the [SonarAnalyzer.CSharp.Styling](https://www.nuget.org/packages/SonarAnalyzer.CSharp.Styling) analyzer.

# Steps to generate the analyzer

Download the Roslyn SDK from https://github.com/SonarSource-VisualStudio/sonarqube-roslyn-sdk

Run
```PowerShell
.\RoslynSonarQubePluginGenerator.exe /a:SonarAnalyzer.CSharp.Styling
```
