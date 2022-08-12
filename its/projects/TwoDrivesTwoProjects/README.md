The solution uses a virtual disk as a path. This is by design.

The virtual disk is created with the setup.bat script (invoked by the test with the correct absolute path of DriveZ), and deleted with the cleanup.bat script.

When developing locally, in case the Integration Test fails before the end, the cleanup.bat script will not be invoked and needs to be invoked manually.
