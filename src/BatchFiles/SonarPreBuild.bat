@echo Running Sonar pre-build script...

@set ProjectKey=%1
@set ProjectName=%2
@set ProjectVersion=%3
@set SonarRunnerProperties=%4


@REM TODO: get from the config file
@set ConfigFolder=%TF_BUILD_BUILDDIRECTORY%\SonarTemp\Config

@echo Sonar project key: %ProjectKey%
@echo Sonar project key: %ProjectName%
@echo Sonar project key: %ProjectVersion%

@echo Sonar runner properties location: %SonarRunnerProperties%
@echo Sonar config location: %ConfigFolder%

@echo Current user:
whoami

@%~dp0\Sonar.TeamBuild.PreProcessor.exe %ProjectKey% %ProjectName% %ProjectVersion% %SonarRunnerProperties%

@REM %~dp0\WriteBuildMessage "Generating Sonar FxCop file..."
@echo Generating Sonar FxCop file...
@%~dp0\Sonar.FxCopRuleset.exe "%SonarRunnerProperties%" "%ProjectKey%" "%ConfigFolder%\SonarAnalysis.ruleset"
@echo ...done.

@echo Sonar pre-build steps complete.
@REM %~dp0\WriteBuildMessage "Sonar pre-build steps complete."
