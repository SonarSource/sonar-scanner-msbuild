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

import com.sonar.it.scanner.msbuild.sonarcloud.Constants;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import java.io.File;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class BuildCommand {

  private final static Logger LOG = LoggerFactory.getLogger(BuildCommand.class);
  private final Set<String> arguments = new HashSet<>();
  private final Map<String, String> environment = new HashMap<>();

  private long timeout = Constants.COMMAND_TIMEOUT;
  private File workingDirectory;

  private BuildCommand() {

  }

  public static BuildCommand create() {
    return new BuildCommand();
  }

  public BuildCommand setEnvironmentVariable(String name, String value) {
    if (value == null) {
      environment.remove(name);
    } else {
      environment.put(name, value);
    }
    return this;
  }

  public BuildCommand setWorkingDirectory(Path path) {
    workingDirectory = path.toFile();
    return this;
  }

  public BuildCommand setTimeout(long timeout) {
    this.timeout = timeout;
    return this;
  }

  public BuildCommand addArgument(String argument) {
    arguments.add(argument);
    return this;
  }

  public BuildResult execute() {
    var command = createCommand();
    var result = new BuildResult();
    LOG.info("Command line: {}", command.toCommandLine());
    result.addStatus(CommandExecutor.create().execute(command, new StreamConsumer.Pipe(result.getLogsWriter()), timeout));
    return result;
  }

  private Command createCommand() {
    var os = System.getProperty("os.name").toLowerCase();
    Command command;
    if (os.equals("windows")) {
      command = createMsBuildBaseCommand();
    } else {
      command = createDotNetBaseCommand();
    }
    command.addArgument("-nodeReuse:false");
    arguments.forEach(command::addArgument);
    environment.forEach(command::setEnvironmentVariable);
    command.setDirectory(workingDirectory);
    return command;
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
    LOG.info("Using dotnet msbuild");
    return Command.create("dotnet").addArgument("msbuild");
  }
}
