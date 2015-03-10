@echo Running Sonar pre-build script...

@set SonarRunnerProperties=%1
@set ConfigFolder=%2

@echo Sonar runner properties location: %SonarRunnerProperties%
@echo Sonar config location: %ConfigFolder%

@echo Creating the Sonar config folder...
@rmdir %ConfigFolder% /S /Q
@mkdir %ConfigFolder%

@echo Generating Sonar FxCop file...
REM @%~dp0\Sonar.FxCopRuleset.exe %SonarRunnerProperties% "%ConfigFolder%\SonarAnalysis.ruleset"
@echo ...done.

@echo Copying a dummy ruleset for the time being...
copy %~dp0\Example.ruleset %ConfigFolder%\SonarAnalysis.ruleset


@echo Sonar pre-build steps complete.
