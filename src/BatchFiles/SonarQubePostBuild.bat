@echo Running SonarQube post-build script...

@set ProjectKey=%1
@set ProjectName=%2	
@set ProjectVersion=%3
@set OutputFolder=%TF_BUILD_BUILDDIRECTORY%\SQTemp\Output

@echo Performing SonarQube post-processing...
@%~dp0\Sonar.TeamBuild.PostProcessor.exe

@echo Generating SonarQube properties file to %OutputFolder% ...
@%~dp0\SonarProjectPropertiesGenerator.exe %ProjectKey% %ProjectName% %ProjectVersion% "%OutputFolder%"
@echo ...done.

@echo Calling SonarRunner...
cd "%OutputFolder%"
@sonar-runner
@echo ...done.
