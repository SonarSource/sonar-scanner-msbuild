@echo Running Sonar post-build script...

@set ProjectKey=%1
@set ProjectName=%2	
@set ProjectVersion=%3
@set OutputFolder=%TF_BUILD_BUILDDIRECTORY%\SonarTemp\Output

@echo Performing Sonar post-processing...
@%~dp0\Sonar.TeamBuild.PostProcessor.exe

@echo Generating Sonar properties file to %OutputFolder% ...
@%~dp0\SonarProjectPropertiesGenerator.exe %ProjectKey% %ProjectName% %ProjectVersion% "%OutputFolder%"
@echo ...done.

@echo Calling SonarRunner...
cd "%OutputFolder%"
@sonar-runner
@echo ...done.
