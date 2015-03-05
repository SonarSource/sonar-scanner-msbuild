@echo Running Sonar pre-build script...

@set SonarRunnerProperties=%1
@set ProjectKey=%2
@set ConfigFolder=%3

@echo Sonar runner properties location: %SonarRunnerProperties%
@echo Sonar project key: %ProjectKey%
@echo Sonar config location: %ConfigFolder%

@echo Creating the Sonar config folder...
@rmdir %ConfigFolder% /S /Q
@mkdir %ConfigFolder%

@echo Dumping environment variables... (TODO - REMOVE)
@~dp0\DumpEnvironmentVars.exe

@echo Generating Sonar FxCop file...
@%~dp0\Sonar.FxCopRuleset.exe "%SonarRunnerProperties%" "%ProjectKey%" "%ConfigFolder%\SonarAnalysis.ruleset"
@echo ...done.

@echo Sonar pre-build steps complete.
