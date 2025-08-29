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
    public string UnixTruststorePath(ProcessedArgs args)
    {
        _ = args ?? throw new ArgumentNullException(nameof(args));

        var javaHome = Environment.GetEnvironmentVariable(EnvironmentVariables.JavaHomeVariableName);
        if (javaHome is null)
        {
            logger.LogDebug(Resources.MSG_JavaHomeNotSet);
            if (ResolveJavaExecutable(args)?.Trim() is { Length: > 0} javaExecutable)
            {
                javaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaExecutable));
            }
            else
            {
                logger.LogDebug(Resources.MSG_CouldNotInferJavaHome);
                return null;
            }
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
        var shellPath = ResolveBourneShellExecutable();

        if (shellPath is null)
        {
            logger.LogDebug(Resources.MSG_BourneShellNotFound);
            return null;
        }

        if (javaExePath is null || !fileWrapper.Exists(javaExePath))
        {
            var commandArgs = new ProcessRunnerArguments(shellPath, false) { CmdLineArgs = ["-c", "command -v java"], LogOutput = false };
            var commandResult = processRunner.Execute(commandArgs);
            if (commandResult.Succeeded)
            {
                javaExePath = commandResult.StandardOutput;
            }
            else
            {
                logger.LogDebug(Resources.MSG_UnableToLocateJavaExecutable, commandResult.ErrorOutput);
                return null;
            }
        }
        else
        {
            logger.LogDebug(Resources.MSG_JavaExecutableSpecified);
        }

        logger.LogDebug(Resources.MSG_JavaExecutableLocated, javaExePath);

        var readlinkArgs = new ProcessRunnerArguments(shellPath, false) { CmdLineArgs = ["-c", $"readlink -f {javaExePath}"], LogOutput = false };
        var readlinkResult = processRunner.Execute(readlinkArgs);
        if (readlinkResult.Succeeded)
        {
            javaExePath = readlinkResult.StandardOutput;
            logger.LogDebug(Resources.MSG_JavaExecutableSymlinkResolved, javaExePath);
        }
        else
        {
            logger.LogDebug(Resources.MSG_UnableToResolveSymlink, javaExePath, readlinkResult.ErrorOutput);
            return null;
        }

        return javaExePath;
    }

    private string ResolveBourneShellExecutable() =>
        Environment.GetEnvironmentVariable(EnvironmentVariables.System.Path)?.Split(Path.PathSeparator).Select(x => Path.Combine(x, "sh")).FirstOrDefault(fileWrapper.Exists);
}
