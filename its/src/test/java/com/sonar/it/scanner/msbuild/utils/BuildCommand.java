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

import static org.junit.jupiter.api.Assertions.assertTrue;

public class BuildCommand extends BaseCommand<BuildCommand> {

  private static final String DEFAULT_DOTNET_COMMAND = "msbuild";
  private static final String MSBUILD_DEFAULT_PATH = "C:\\Program Files\\Microsoft Visual Studio\\2022\\Professional\\MSBuild\\Current\\Bin\\MSBuild.exe";
  private static final Logger LOG = LoggerFactory.getLogger(BuildCommand.class);

  private final ArrayList<String> arguments = new ArrayList<>();
  private int timeout = 60 * 1000;
  private String dotnetCommand;

  public BuildCommand(Path projectDir) {
    super(projectDir);
  }

  public BuildCommand setTimeout(int timeout) {
    this.timeout = timeout;
    return this;
  }

  public BuildCommand useDotNet() {
    return useDotNet("build");
  }

  public BuildCommand useDotNet(String dotnetCommand) {
    this.dotnetCommand = dotnetCommand;
    return this;
  }

  public BuildCommand addArgument(String... arguments) {
    this.arguments.addAll(Arrays.asList(arguments));
    return this;
  }

  public BuildResult execute() {
    var command = createCommand();
    var result = new BuildResult();
    LOG.info("Command line: {}", command.toCommandLine());
    result.addStatus(CommandExecutor.create().execute(command, new StreamConsumer.Pipe(result.getLogsWriter()), timeout));
    assertTrue(result.isSuccess());
    return result;
  }

  private Command createCommand() {
    Command command;
    if (dotnetCommand == null) {
      var os = System.getProperty("os.name").toLowerCase();
      if (os.contains("windows")) {
        command = createMsBuildBaseCommand();
      } else {
        command = createDotNetBaseCommand(DEFAULT_DOTNET_COMMAND);
      }
      // Adding default build arguments
      // Assuming that whenever we use MSBuild we always want to restore and rebuild
      command.addArgument("-nodeReuse:false");
      command.addArgument("/t:Restore,Rebuild");
    } else {
      command = createDotNetBaseCommand(dotnetCommand);
    }
    arguments.forEach(command::addArgument);
    environment.forEach(command::setEnvironmentVariable);
    command.setDirectory(projectDir.toFile());
    return command;
  }

  private Command createMsBuildBaseCommand() {
    var msBuildPathStr = Optional.ofNullable(System.getProperty("msbuild.path", System.getenv("MSBUILD_PATH"))).orElse(MSBUILD_DEFAULT_PATH);
    Path msBuildPath = Paths.get(msBuildPathStr).toAbsolutePath();
    if (!Files.exists(msBuildPath)) {
      throw new IllegalStateException("Unable to find MSBuild at " + msBuildPath
                                      + ". Please configure property 'msbuild.path' or 'MSBUILD_PATH' environment variable to the full path to MSBuild.exe.");
    }
    LOG.info("MSBUILD_PATH is set to {}", msBuildPath);
    return Command.create(msBuildPath.toString());
  }

  private static Command createDotNetBaseCommand(String dotnetCommand) {
    LOG.info("Using dotnet {}", dotnetCommand);
    return Command.create("dotnet").addArgument(dotnetCommand);
  }

  @Override
  protected BuildCommand self() {
    return this;
  }
}
