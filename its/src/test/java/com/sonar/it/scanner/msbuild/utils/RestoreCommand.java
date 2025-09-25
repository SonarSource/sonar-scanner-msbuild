/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto:info AT sonarsource DOT com
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
package com.sonar.it.scanner.msbuild.utils;

import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static com.sonar.it.scanner.msbuild.utils.SonarAssertions.assertThat;

public class RestoreCommand extends BaseCommand<RestoreCommand> {
  private static final Logger LOG = LoggerFactory.getLogger(RestoreCommand.class);

  public RestoreCommand(Path projectDir) {
    super(projectDir);
  }

  public BuildResult execute() {
    var command = createCommand();
    var result = new BuildResult();
    LOG.info("Nuget command start: '{}' in {}", command.toCommandLine(), command.getDirectory());
    result.addStatus(CommandExecutor.create().execute(command, new StreamConsumer.Pipe(result.getLogsWriter()), timeout.miliseconds));
    assertThat(result.isSuccess()).describedAs("NuGet failed. Logs: " + result.getLogs()).isTrue();
    LOG.info("Nuget command finish: '{}' in {}", command.toCommandLine(), command.getDirectory());
    return result;
  }

  private Command createCommand() {
    Command command;
    if (OSPlatform.isWindows()) {
      command = Command.create(nuGetPath());
      command.addArgument("restore");
      // We have multiple versions of MSBuild installed in the CI.
      // When using NuGet, we want to use the same MSBuild version as the one used by the BuildCommand.
      command.addArgument("-MSBuildPath");
      command.addArgument(Path.of(BuildCommand.msBuildPath()).getParent().toString());
    } else {
      command = Command.create("dotnet");
      command.addArgument("restore");
    }
    command.replaceEnvironment(environment);
    command.setDirectory(projectDir.toFile());
    return command;
  }

  private static String nuGetPath() {
    var path = System.getenv("NUGET_PATH");
    if (path == null || path.isEmpty()) {
      throw new IllegalStateException("Environment variable 'NUGET_PATH' is not set. Please configure the environment variable 'NUGET_PATH'");
    }
    Path nugetPath = Paths.get(path).toAbsolutePath();
    if (!Files.exists(nugetPath)) {
      throw new IllegalStateException("Unable to find NuGet at '" + nugetPath + "'. Please configure the environment variable 'NUGET_PATH'");
    }
    return nugetPath.toString();
  }

  @Override
  protected RestoreCommand self() {
    return this;
  }
}
