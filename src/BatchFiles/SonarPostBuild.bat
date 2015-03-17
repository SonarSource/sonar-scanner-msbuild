@echo Running Sonar post-build script...

@set ProjectKey=%1
@set ProjectName=%2	
@set ProjectVersion=%3

@REM Set the output folder to the command line parameter, if supplied
@set OutputFolder=%4
@if "%OutputFolder%"=="" set OutputFolder=%TF_BUILD_BUILDDIRECTORY%\SonarTemp\Output\
@echo Sonar output folder = %OutputFolder%

@echo Performing Sonar post-processing...
@%~dp0\Sonar.TeamBuild.PostProcessor.exe

@echo Generating Sonar properties file to %OutputFolder% ...
@%~dp0\SonarProjectPropertiesGenerator.exe %ProjectKey% %ProjectName% %ProjectVersion% %OutputFolder%
@echo ...done.

@echo Calling SonarRunner...
@cd %OutputFolder%
@sonar-runner
@echo ...done.
