/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.Globalization;

namespace SonarScanner.MSBuild.PreProcessor;

/// <summary>
/// Handlers copying targets to well known locations and warning the user about existing targets file.
/// </summary>
public class TargetsInstaller : ITargetsInstaller
{
    /// <summary>
    /// Controls the default value for installing the loader targets.
    /// </summary>
    /// <remarks>Can be overridden from the command line.</remarks>
    public const bool DefaultInstallSetting = true;

    private static readonly string AssemblyLocation = Path.GetDirectoryName(typeof(ArgumentProcessor).Assembly.Location);

    private readonly IRuntime runtime;
    private readonly IMsBuildPathsSettings msBuildPathsSettings;

    public TargetsInstaller(IRuntime runtime, IMsBuildPathsSettings msBuildPathsSettings = null)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.msBuildPathsSettings = msBuildPathsSettings ?? new MsBuildPathSettings(runtime.OperatingSystem);
    }

    public void InstallLoaderTargets(string workDirectory)
    {
        WarnOnGlobalTargetsFile();
        InternalCopyTargetsFile();
        InternalCopyTargetFileToProject(workDirectory);
    }

    private void InternalCopyTargetFileToProject(string workDirectory) =>
        CopyIfDifferent(
            TargetSourcePath(FileConstants.IntegrationTargetsName),
            [Path.Combine(workDirectory, "bin", "targets")]);

    private void InternalCopyTargetsFile()
    {
        runtime.LogInfo(Resources.MSG_UpdatingMSBuildTargets);

        CopyIfDifferent(
            TargetSourcePath(FileConstants.ImportBeforeTargetsName),
            msBuildPathsSettings.ImportBeforePaths());
    }

    private void CopyIfDifferent(string sourcePath, IEnumerable<string> destinationDirs)
    {
        Debug.Assert(runtime.File.Exists(sourcePath), string.Format(CultureInfo.InvariantCulture, "Could not find the loader .targets file at {0}", sourcePath));

        var sourceContent = runtime.File.ReadAllText(sourcePath);
        var fileName = Path.GetFileName(sourcePath);

        foreach (var destinationDir in destinationDirs)
        {
            var destinationPath = Path.Combine(destinationDir, fileName);

            try
            {
                if (runtime.File.Exists(destinationPath))
                {
                    if (string.Equals(sourceContent, runtime.File.ReadAllText(destinationPath), StringComparison.Ordinal))
                    {
                        runtime.LogDebug(Resources.MSG_InstallTargets_UpToDate, fileName, destinationDir);
                    }
                    else
                    {
                        runtime.File.Copy(sourcePath, destinationPath, overwrite: true);
                        runtime.LogDebug(Resources.MSG_InstallTargets_Overwrite, fileName, destinationDir);
                    }
                }
                else
                {
                    runtime.Directory.CreateDirectory(destinationDir); // creates all the directories in the path if needed

                    // always overwrite to avoid intermittent exceptions: https://github.com/SonarSource/sonar-scanner-msbuild/issues/647
                    runtime.File.Copy(sourcePath, destinationPath, overwrite: true);
                    runtime.LogDebug(Resources.MSG_InstallTargets_Copy, fileName, destinationDir);
                }
            }
            catch (Exception e)
            {
                runtime.LogWarning(Resources.MSG_InstallTargets_Error, destinationPath, e.Message);
                runtime.LogDebug(e.StackTrace);
            }
        }
    }

    /// <summary>
    /// Logs a warning when \Program Files (x86)\MSBuild\14.0\Microsoft.Common.Targets\ImportBefore\SonarQube.Integration.ImportBefore.targets exists.
    /// </summary>
    private void WarnOnGlobalTargetsFile()
    {
        // Giving a warning is best effort - if the user has installed MSBUILD in a non-standard location then this will not work
        foreach (var path in msBuildPathsSettings.GlobalTargetsPaths().Where(ImportBeforeTargetExists))
        {
            LogWarning(path);
        }

        bool ImportBeforeTargetExists(string globalTargetPath) =>
            runtime.File.Exists(Path.Combine(globalTargetPath, FileConstants.ImportBeforeTargetsName));

        void LogWarning(string globalTargetPath) =>
            runtime.LogWarning(Resources.WARN_ExistingGlobalTargets, FileConstants.ImportBeforeTargetsName, globalTargetPath);
    }

    private static string TargetSourcePath(string targetFileName) =>
        Path.Combine(AssemblyLocation, "Targets", targetFileName);
}
