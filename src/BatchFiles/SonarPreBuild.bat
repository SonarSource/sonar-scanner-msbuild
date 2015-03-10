@echo Running Sonar pre-build script...

@set SonarRunnerProperties=%1
@set ProjectKey=%2

@REM Set the config folder to the command line parameter, if supplied
@set ConfigFolder=%3
@if "%ConfigFolder%"=="" ( 
	set ConfigFolder=%TF_BUILD_BUILDDIRECTORY%\SonarTemp\Config
	@echo Creating the root SonarTemp directory...
	if EXIST %TF_BUILD_BUILDDIRECTORY%\SonarTemp rmdir %TF_BUILD_BUILDDIRECTORY%\SonarTemp /S /Q
	mkdir %TF_BUILD_BUILDDIRECTORY%\SonarTemp
)
@echo ConfigFolder = %ConfigFolder%

@echo Sonar runner properties location: %SonarRunnerProperties%
@echo Sonar project key: %ProjectKey%
@echo Sonar config location: %ConfigFolder%

@echo Creating the Sonar config folder...
@if EXIST %ConfigFolder% rmdir %ConfigFolder% /S /Q
@mkdir %ConfigFolder%

@echo Generating Sonar FxCop file...
@%~dp0\Sonar.FxCopRuleset.exe "%SonarRunnerProperties%" "%ProjectKey%" "%ConfigFolder%\SonarAnalysis.ruleset"
@echo ...done.

@echo Sonar pre-build steps complete.
