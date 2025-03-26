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
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.junit.jupiter.api.Assertions.assertTrue;

public class BuildCommand extends BaseCommand<BuildCommand> {

  private static final String DEFAULT_DOTNET_COMMAND = "msbuild";

  private static final Logger LOG = LoggerFactory.getLogger(BuildCommand.class);
  private final ArrayList<String> arguments = new ArrayList<>();
  private int timeout = 60 * 1000;
  private String dotnetCommand;

  private BuildCommand(Path projectDir) {
    super(projectDir);
  }

  public static BuildCommand create(Path projectDir) {
    return new BuildCommand(projectDir);
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
    addDefaultArguments();
    var command = createCommand();
    var result = new BuildResult();
    LOG.info("Command line: {}", command.toCommandLine());
    result.addStatus(CommandExecutor.create().execute(command, new StreamConsumer.Pipe(result.getLogsWriter()), timeout));
    assertTrue(result.isSuccess());
    return result;
  }

  private void addDefaultArguments() {
    // If the provided command is not the default one, we assume the arguments are already set
    if (dotnetCommand != null && !DEFAULT_DOTNET_COMMAND.equals(dotnetCommand)) {
      return;
    }
    // Adding default build arguments
    // Assuming that whenever we use MSBuild we always want to restore and rebuild
    arguments.add(0, "-nodeReuse:false");
    arguments.add(0, "/t:Restore,Rebuild");
  }

  private Command createCommand() {
    determineBuildCommand();
    Command command;
    if (dotnetCommand == null) {
      command = createMsBuildBaseCommand();
    } else {
      command = createDotNetBaseCommand();
    }
    arguments.forEach(command::addArgument);
    environment.forEach(command::setEnvironmentVariable);
    command.setDirectory(projectDir.toFile());
    return command;
  }

  private void determineBuildCommand() {
    if (dotnetCommand != null) {
      return;
    }
    var os = System.getProperty("os.name").toLowerCase();
    if (!os.contains("windows")) {
      dotnetCommand = DEFAULT_DOTNET_COMMAND;
    }
  }

  private Command createMsBuildBaseCommand() {
    var msBuildPathStr = System.getProperty("msbuild.path", System.getenv("MSBUILD_PATH"));
    Path msBuildPath = Paths.get(msBuildPathStr).toAbsolutePath();
    if (!Files.exists(msBuildPath)) {
      throw new IllegalStateException("Unable to find MSBuild at " + msBuildPath
                                      + ". Please configure property 'msbuild.path' or 'MSBUILD_PATH' environment variable to the full path to MSBuild.exe.");
    }
    LOG.info("MSBUILD_PATH is set to {}", msBuildPath);
    return Command.create(msBuildPath.toString());
  }

  private Command createDotNetBaseCommand() {
    LOG.info("Using dotnet {}", dotnetCommand);
    return Command.create("dotnet").addArgument(dotnetCommand);
  }

  @Override
  protected BuildCommand self() {
    return this;
  }
}
