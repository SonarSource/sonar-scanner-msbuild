@echo Running Sonar post-build script...

@set ProjectKey=%1
@set ProjectName=%2
@set ProjectVersion=%3
@set OutputFolder=%4

@echo Generating Sonar properties file...
@%~dp0\SonarProjectPropertiesGenerator.exe %ProjectKey% %ProjectName% %ProjectVersion% %OutputFolder%
@echo ...done.


@echo Current path:
@echo %PATH%
REM TODO: adding java and sonar-runner to the path...
@set PATH=%PATH%;C:\ProgramData\Oracle\Java\javapath;c:\SonarQube\sonar-runner-2.4\bin\;

@echo Calling SonarRunner...
@cd %OutputFolder%
@sonar-runner
@echo ...done.
