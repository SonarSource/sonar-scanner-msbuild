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
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Optional;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.assertj.core.api.Assertions.assertThat;

public class BuildCommand extends BaseCommand<BuildCommand> {

  private static final String MSBUILD_DEFAULT_PATH = "C:\\Program Files\\Microsoft Visual Studio\\2022\\Professional\\MSBuild\\Current\\Bin\\MSBuild.exe";
  private static final Logger LOG = LoggerFactory.getLogger(BuildCommand.class);

  private final ArrayList<String> arguments = new ArrayList<>();
  private String dotnetCommand;
  private boolean shouldInvokeNugetRestore;

  public BuildCommand(Path projectDir) {
    super(projectDir);
  }

  public BuildCommand useDotNet() {
    return useDotNet("build");
  }

  public BuildCommand useDotNet(String dotnetCommand) {
    this.dotnetCommand = dotnetCommand;
    return this;
  }

  public BuildCommand withNuGetRestore() {
    this.shouldInvokeNugetRestore = true;
    return this;
  }

  public BuildCommand addArgument(String... arguments) {
    this.arguments.addAll(Arrays.asList(arguments));
    return this;
  }

  public BuildResult execute() {
    if (shouldInvokeNugetRestore) {
      executeNuGetRestore();
    }
    var command = createCommand();
    var result = new BuildResult();
    LOG.info("Build command start: '{}' in {}", command.toCommandLine(), command.getDirectory());
    result.addStatus(CommandExecutor.create().execute(command, new StreamConsumer.Pipe(result.getLogsWriter()), timeout.miliseconds));
    assertThat(result.isSuccess()).describedAs("BUILD step failed. Logs: " + result.getLogs()).isTrue();
    LOG.info("Build command finish: '{}' in {}", command.toCommandLine(), command.getDirectory());
    return result;
  }

  private void executeNuGetRestore() {
    var command = new RestoreCommand(projectDir);
    command.setTimeout(this.timeout);
    environment.forEach(command::setEnvironmentVariable);
    var result = command.execute();
    assertThat(result.isSuccess()).describedAs("RESTORE failed. Logs: " + result.getLogs()).isTrue();
  }

  private Command createCommand() {
    Command command;
    if (dotnetCommand == null) {
      command = OSPlatform.isWindows()
        ? Command.create(msBuildPath())
        // Using the msbuild command from the dotnet CLI allows to use the same parameters as the Windows version
        // https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build#msbuild
        : Command.create("dotnet").addArgument("msbuild");
      command
        .addArgument("/t:Restore,Rebuild")
        .addArgument("/warnaserror:AD0001")
        .addArgument("-nodeReuse:false"); // Equivalent of dotnet --disable-build-servers
    } else {
      command = Command.create("dotnet")
        .addArgument(dotnetCommand)
        .addArgument("-warnaserror:AD0001")
        .addArgument("--disable-build-servers");
    }
    arguments.forEach(command::addArgument);
    command.replaceEnvironment(environment);
    command.setDirectory(projectDir.toFile());
    return command;
  }

  public static String msBuildPath() {
    var input = Optional.ofNullable(System.getProperty("msbuild.path", System.getenv("MSBUILD_PATH"))).orElse(MSBUILD_DEFAULT_PATH);
    Path path = Paths.get(input).toAbsolutePath();
    if (!Files.exists(path)) {
      throw new IllegalStateException("Unable to find MSBuild at " + path
        + ". Please configure property 'msbuild.path' or 'MSBUILD_PATH' environment variable to the full path to MSBuild.exe.");
    }
    return path.toString();
  }

  @Override
  protected BuildCommand self() {
    return this;
  }
}
