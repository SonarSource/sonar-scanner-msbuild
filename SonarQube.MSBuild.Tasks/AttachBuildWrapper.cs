//-----------------------------------------------------------------------
// <copyright file="AttachBuildWrapper.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SonarQube.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace SonarQube.MSBuild.Tasks
{
    /// <summary>
    /// MSBuild task to notify the BuildWrapper that the build of C++ project has started
    /// </summary>
    public class AttachBuildWrapper : Task
    {
        // See https://jira.sonarsource.com/browse/CPP-1458 for the specification of the files contained in the C++ static resource zip
        private const string BuildWrapperExeName = "build-wrapper-win-x86-64.exe";
        private const string AttachedBinaryFileName32 = "interceptor32.dll";
        private const string AttachedBinaryFileName64 = "interceptor64.dll";
        private const string BuildWrapperSubDirName = "build-wrapper-win-x86";

        private const int BuildWrapperTimeoutInMs = 5000;

        private static readonly string[] RequiredFileNames = new string[] { BuildWrapperExeName, AttachedBinaryFileName32, AttachedBinaryFileName64 };

        #region Input properties

        /// <summary>
        /// Path to the .sonarqube binary directory containing the build wrapper
        /// </summary>
        [Required]
        public string BinDirectoryPath { get; set; }

        /// <summary>
        /// Directory in which the build wrapper to write the collected data
        /// </summary>
        [Required]
        public string OutputDirectoryPath { get; set; }

        #endregion Input properties

        public override bool Execute()
        {
            bool success = CheckRequiredFilesExist();
            if (!success)
            {
                return false;
            }

            try
            {
                if (this.IsAlreadyAttached())
                {
                    this.Log.LogMessage(MessageImportance.Low, Resources.BuildWrapper_AlreadyAttached);
                    success = true;
                }
                else
                {
                    Directory.CreateDirectory(this.OutputDirectoryPath);

                    success = this.Attach();
                }
            }
            catch (Exception ex)
            {
                this.Log.LogErrorFromException(ex, true, true, null);
                success = false;
            }

            return success;
        }

        #region Private methods

        /// <summary>
        /// Returns the full path to the directory that should contain the build wrapper binaries
        /// </summary>
        private string BuildWrapperBinaryDir
        {
            get
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(this.BinDirectoryPath), "Expecting the BinDirectoryPath to havbe been set");
                return Path.Combine(this.BinDirectoryPath, BuildWrapperSubDirName);
            }
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private bool IsAlreadyAttached()
        {
            string markerDll32 = Path.Combine(this.BuildWrapperBinaryDir, AttachedBinaryFileName32);
            string markerDll64 = Path.Combine(this.BuildWrapperBinaryDir, AttachedBinaryFileName64);

            return IsModuleLoaded(markerDll32) ||  IsModuleLoaded(markerDll64);
        }

        private static bool IsModuleLoaded(string fullPath)
        {
            IntPtr h = GetModuleHandle(fullPath);
            if (h != IntPtr.Zero)
            {
                return true;
            }
            return false;
        }

        private bool Attach()
        {
            string currentPID = Process.GetCurrentProcess().Id.ToString();

            string monitorExeFilePath = Path.Combine(this.BuildWrapperBinaryDir, BuildWrapperExeName);
            ProcessRunnerArguments args = new ProcessRunnerArguments(monitorExeFilePath, new MSBuildLoggerAdapter(this.Log));
            args.TimeoutInMilliseconds = BuildWrapperTimeoutInMs;

            // See https://jira.sonarsource.com/browse/CPP-1469 for the specification of the arguments to pass to the C++ plugin
            // --msbuild - task < PID > < OUTPUT_DIR >
            args.CmdLineArgs = new string[] { "--msbuild-task", currentPID, OutputDirectoryPath };

            this.Log.LogMessage(MessageImportance.Low, Resources.BuildWrapper_WaitingForAttach, currentPID);

            ProcessRunner runner = new ProcessRunner();
            bool success = runner.Execute(args);

            if (success)
            {
                this.Log.LogMessage(MessageImportance.Low, Resources.BuildWrapper_AttachedSuccessfully);
            }
            else
            {
                this.Log.LogError(Resources.BuildWrapper_FailedToAttach);
            }

            return success;
        }

        private bool CheckRequiredFilesExist()
        {
            bool allExist = true;

            string binDir = this.BuildWrapperBinaryDir;

            foreach (string fileName in RequiredFileNames)
            {
                string fullName = Path.Combine(binDir, fileName);
                if (!File.Exists(fullName))
                {
                    this.Log.LogError(Resources.BuildWrapper_RequiredFileMissing, fullName);
                    allExist = false;
                }
            }
            return allExist;
        }

        #endregion Private methods

    }
}