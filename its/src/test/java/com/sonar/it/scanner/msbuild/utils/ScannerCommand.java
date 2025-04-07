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

import com.sonar.it.scanner.msbuild.sonarcloud.CloudUtils;
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.build.SynchronousAnalyzer;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;
import javax.annotation.Nullable;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.junit.jupiter.api.Assertions.assertFalse;

public class ScannerCommand extends BaseCommand<ScannerCommand> {

  private boolean shouldExpandEnvVars;

  private enum Step {
    help,
    begin,
    end
  }

  private static final Logger LOG = LoggerFactory.getLogger(ScannerCommand.class);
  private final Step step;
  private final ScannerClassifier classifier;
  private final String token;
  private final String projectKey;
  private final Map<String, String> properties = new HashMap();
  private String organization;

  private ScannerCommand(Step step, ScannerClassifier classifier, String token, Path projectDir, @Nullable String projectKey) {
    super(projectDir);
    this.step = step;
    this.classifier = classifier;
    this.token = token;
    this.projectKey = projectKey;
  }

  public static ScannerCommand createBeginStep(ScannerClassifier classifier, String token, Path projectDir, String projectKey) {
    return new ScannerCommand(Step.begin, classifier, token, projectDir, projectKey)
      .setProperty("sonar.projectBaseDir", projectDir.toAbsolutePath().toString())
      // Default values provided by Orchestrator to ScannerForMSBuild in adjustedProperties
      .setProperty("sonar.scm.disabled", "true")
      .setProperty("sonar.branch.autoconfig.disabled", "true")
      .setProperty("sonar.scanner.skipJreProvisioning", "true");  // Desired default behavior in ITs. Specific tests should set null or false to remove this.
  }

  public static ScannerCommand createEndStep(ScannerClassifier classifier, String token, Path projectDir) {
    return new ScannerCommand(Step.end, classifier, token, projectDir, null);
  }

  public static ScannerCommand createHelpStep(ScannerClassifier classifier, Path projectDir) {
    return new ScannerCommand(Step.help, classifier, null, projectDir, null);
  }

  public ScannerCommand setProperty(Property... properties) {
    for (var property : properties) {
      setProperty(property.name(), property.value());
    }
    return this;
  }

  public ScannerCommand setProperty(String key, @Nullable String value) {
    if (value == null) {
      this.properties.remove(key);
    } else {
      this.properties.put(key, value);
    }
    return this;
  }

  public ScannerCommand setDebugLogs() {
    return setProperty("sonar.verbose", "true");
  }

  public ScannerCommand setOrganization(String organization) {
    this.organization = organization;
    return this;
  }

  // We need this method to expand environment variables within the command line arguments.
  // While this automatic on Windows with cmd, it is not on Linux.
  // This is useful for SonarQube Cloud tests as we pass sensitive data through environment.
  public ScannerCommand expandEnvironmentVariables() {
    this.shouldExpandEnvVars = !OSPlatform.isWindows();
    return this;
  }

  public BuildResult execute(Orchestrator orchestrator) {
    var command = createCommand(orchestrator);
    var result = new BuildResult();
    LOG.info("Scanner command start: '{}' in {}", command.toCommandLine(), command.getDirectory());
    result.addStatus(CommandExecutor.create().execute(command, new StreamConsumer.Pipe(result.getLogsWriter()), timeout.miliseconds));
    LOG.info("Scanner command finish: '{}' in {}", command.toCommandLine(), command.getDirectory());
    if (step == Step.end) {
      if (orchestrator == null) {
        CloudUtils.waitForTaskProcessing(result.getLogs());
      } else {
        new SynchronousAnalyzer(orchestrator.getServer()).waitForDone();  // Wait for Compute Engine to finish processing (all) analysis
      }
    }
    return result;
  }

  @Override
  protected ScannerCommand self() {
    return this;
  }

  private Command createCommand(Orchestrator orchestrator) {
    var command = classifier.createBaseCommand().setDirectory(projectDir.toFile());
    if (step == Step.help) {
      command.addArgument("/?");
    } else {
      command.addArgument(step.toString());
      if (token != null) {
        var tokenProperty = orchestrator == null || orchestrator.getServer().version().isGreaterThanOrEquals(10, 0)
          ? "/d:sonar.token=" + token   // The `sonar.token` property was introduced in SonarQube 10.0
          : "/d:sonar.login=" + token;  // sonar.login is obsolete
        command.addArgument(tokenProperty);
      }
    }
    if (step == Step.begin) {
      command.addArgument("/k:" + projectKey);
      if (organization != null) {
        command.addArgument("/o:" + organization);
      }
      if (orchestrator != null && !properties.containsKey("sonar.host.url") && !properties.containsKey("sonar.scanner.sonarcloudUrl")) {
        command.addArgument("/d:sonar.host.url=" + orchestrator.getServer().getUrl());
      }
    }
    for (var entry : properties.entrySet()) {
      command.addArgument("/d:" + entry.getKey() + "=" + entry.getValue());
    }
    if (shouldExpandEnvVars) {
      assertFalse(OSPlatform.isWindows(), "Trying to expand environment variables on Windows. This is not supposed to happen.");
      command = Command.create("sh")
        .addArgument("-c")
        .setDirectory(projectDir.toFile())
        .addArgument(command.toCommandLine());
    }
    for (var entry : this.environment.entrySet()) {
      command.setEnvironmentVariable(entry.getKey(), entry.getValue());
    }
    return command;
  }
}
