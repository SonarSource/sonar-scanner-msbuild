1. How to build the steps

Building the Packaging project will create a directory called VsoBuildSteps in the root of the project. This will contain up-to-date assemblies and targets files. 

2. How to author in the steps to VSO

Currently there is no published tool to upload the tasks to your vso account but there is an unofficial tool. Please contact any of the project owners to get a copy of it.

3. How to auto-generate the json localization files

Please note that currently only the json UI manifest is localizable - the powershell scripts are not. If you make changes to the json file please see the build steps on the 
vso-agent-tasks project on github on how to auto-generate the resource files. 