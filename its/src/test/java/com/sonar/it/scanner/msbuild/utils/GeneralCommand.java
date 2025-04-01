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
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

import static com.sonar.it.scanner.msbuild.utils.TestUtils.LOG;
import static com.sonar.it.scanner.msbuild.utils.TestUtils.TIMEOUT_LIMIT;
import static org.assertj.core.api.Assertions.assertThat;

public class GeneralCommand extends BaseCommand<GeneralCommand> {
  private final String command;
  private long timeout = TIMEOUT_LIMIT;
  private final ArrayList<String> arguments = new ArrayList<>();

  public GeneralCommand(String command, Path workingDirectory) {
    super(workingDirectory);
    this.command = command;
  }

  public GeneralCommand setTimeout(long timeout) {
    this.timeout = timeout;
    return this;
  }

  public GeneralCommand addArgument(String arg) {
    arguments.add(arg);
    return this;
  }

  public GeneralCommand addArguments(String... arguments) {
    this.arguments.addAll(List.of(arguments));
    return this;
  }

  public BuildResult execute() {
    var command = Command.create(this.command).setDirectory(projectDir.toFile()).addArguments(arguments);
    environment.forEach(command::setEnvironmentVariable);
    var commandLine = command.toCommandLine();
    LOG.info("Command line start: '{}' in {}", commandLine, command.getDirectory());
    var result = new BuildResult();
    var returnCode = CommandExecutor.create().execute(command, new StreamConsumer.Pipe(result.getLogsWriter()), timeout);
    result.addStatus(returnCode);
    assertThat(result.isSuccess()).describedAs("Command '" + commandLine + "' failed with logs: " + result.getLogs()).isTrue();
    LOG.info("Command line finish: '{}' in {}", commandLine, command.getDirectory());
    return result;
  }

  @Override
  protected GeneralCommand self() {
    return this;
  }
}
