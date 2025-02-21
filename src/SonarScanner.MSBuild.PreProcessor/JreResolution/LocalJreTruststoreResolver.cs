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

namespace SonarScanner.MSBuild.PreProcessor.JreResolution;

public class LocalJreTruststoreResolver(IFileWrapper fileWrapper, IDirectoryWrapper directoryWrapper, IProcessRunner processRunner, ILogger logger)
{
    public string TruststorePath(ProcessedArgs args)
    {
        _ = args ?? throw new ArgumentNullException(nameof(args));

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (javaHome is null)
        {
            logger.LogDebug(Resources.MSG_JavaHomeNotSet);
            if (ResolveJavaExecutable(args) is not { } javaExecutable
                || string.IsNullOrWhiteSpace(javaExecutable))
            {
                logger.LogDebug(Resources.MSG_CouldNotInferJavaHome);
                return null;
            }
            javaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaExecutable));
        }

        if (!directoryWrapper.Exists(javaHome))
        {
            logger.LogDebug(Resources.MSG_JavaHomeDoesNotExist, javaHome);
            return null;
        }

        var truststorePath = Path.Combine(javaHome, "lib", "security", "cacerts");
        if (!fileWrapper.Exists(truststorePath))
        {
            logger.LogDebug(Resources.MSG_JavaHomeCacertsNotFound, truststorePath);
            return null;
        }

        logger.LogDebug(Resources.MSG_JavaHomeCacertsFound, truststorePath);

        return truststorePath;
    }

    private string ResolveJavaExecutable(ProcessedArgs args)
    {
        var javaExePath = args.JavaExePath;

        if (javaExePath is null || !fileWrapper.Exists(javaExePath))
        {
            var commandArgs = new ProcessRunnerArguments("/bin/bash", false) { CmdLineArgs = ["-c", "command -v java"], LogOutput = false };
            if (processRunner.Execute(commandArgs))
            {
                javaExePath = processRunner.StandardOutput.ReadToEnd();
            }
            else
            {
                logger.LogDebug(Resources.MSG_UnableToLocateJavaExecutable, processRunner.ErrorOutput.ReadToEnd());
                return null;
            }
        }
        else
        {
            logger.LogDebug(Resources.MSG_JavaExecutableSpecified);
        }

        logger.LogDebug(Resources.MSG_JavaExecutableLocated, javaExePath);

        var readlinkArgs = new ProcessRunnerArguments("/bin/bash", false) { CmdLineArgs = ["-c", $"readlink -f {javaExePath}"], LogOutput = false };
        if (processRunner.Execute(readlinkArgs))
        {
            javaExePath = processRunner.StandardOutput.ReadToEnd();
            logger.LogDebug(Resources.MSG_JavaExecutableSymlinkResolved, javaExePath);
        }
        else
        {
            logger.LogDebug(Resources.MSG_UnableToResolveSymlink, javaExePath, processRunner.ErrorOutput.ReadToEnd());
            return null;
        }

        return javaExePath;
    }
}
