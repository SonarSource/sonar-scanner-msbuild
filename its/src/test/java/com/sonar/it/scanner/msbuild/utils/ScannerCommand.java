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
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.build.BuildResult;
import com.sonar.orchestrator.build.SynchronousAnalyzer;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import com.sonar.orchestrator.util.StreamConsumer;
import java.io.File;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;
import javax.annotation.Nullable;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class ScannerCommand {

  private enum Step {
    help,
    begin,
    end
  }

  private final static Logger LOG = LoggerFactory.getLogger(ScannerCommand.class);
  private Step step;  // ToDo: Make final in SCAN4NET-201.
  private final ScannerClassifier classifier;
  private final String token;
  private final Path projectDir;
  private String projectKey;  // ToDo: Make final in SCAN4NET-201.
  private final Map<String, String> properties = new HashMap();
  private final Map<String, String> environment = new HashMap();
  private String organization;

  private ScannerCommand(Step step, ScannerClassifier classifier, String token, Path projectDir, @Nullable String projectKey) {
    this.step = step;
    this.classifier = classifier;
    this.token = token;
    this.projectDir = projectDir;
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

  public ScannerCommand setProperty(String key, @Nullable String value) {
    if (value == null) {
      this.properties.remove(key);
    } else {
      this.properties.put(key, value);
    }
    return this;
  }

  public ScannerCommand setDebugLogs(boolean verbose) {
    return setProperty("sonar.verbose", Boolean.toString(verbose));
  }

  public ScannerCommand setEnvironmentVariable(String name, String value) {
    if (value == null) {
      environment.remove(name);
    } else {
      environment.put(name, value);
    }
    return this;
  }

  public ScannerCommand setOrganization(String organization) {
    this.organization = organization;
    return this;
  }

  @Deprecated
  public ScannerCommand addArgument(String argument) {
    // ToDo: Remove in SCAN4NET-201.
    if (argument == "begin") {
      step = Step.begin;
    } else if (argument == "end") {
      step = Step.end;
    } else {
      throw new IllegalArgumentException();
    }

    return this;
  }

  @Deprecated
  public ScannerCommand setProjectKey(String projectKey) {
    // ToDo: Remove in SCAN4NET-201.
    this.projectKey = projectKey;
    return this;
  }

  @Deprecated
  public ScannerCommand setProjectName(String name) {
    // ToDo: Remove in SCAN4NET-201.
    // It's no-op in-place replacement for now.
    return this;
  }

  @Deprecated
  public ScannerCommand setProjectVersion(String version) {
    // ToDo: Remove in SCAN4NET-201.
    // It's no-op in-place replacement for now.
    return this;
  }

  @Deprecated
  public ScannerCommand setProjectDir(File dir) {
    // ToDo: Remove in SCAN4NET-201.
    // It's no-op in-place replacement for now.
    return this;
  }

  @Deprecated
  public ScannerCommand setScannerVersion(String version) {
    // ToDo: Remove in SCAN4NET-201.
    // It's no-op in-place replacement for now.
    return this;
  }

  @Deprecated
  public ScannerCommand setUseDotNetCore(boolean value) {
    // ToDo: Remove in SCAN4NET-201.
    // It's no-op in-place replacement for now.
    return this;
  }

  public BuildResult execute(Orchestrator orchestrator) {
    var command = createCommand(orchestrator);
    var result = new BuildResult();
    LOG.info("Command line: {}", command.toCommandLine());
    result.addStatus(CommandExecutor.create().execute(command, new StreamConsumer.Pipe(result.getLogsWriter()), Constants.COMMAND_TIMEOUT));
    if (step == Step.end && orchestrator != null) {
      new SynchronousAnalyzer(orchestrator.getServer()).waitForDone();  // Wait for Compute Engine to finish processing (all) analysis
    }
    return result;
  }

  private Command createCommand(Orchestrator orchestrator) {
    var tokenProperty = orchestrator == null || orchestrator.getServer().version().isGreaterThanOrEquals(10, 0)
      ? "/d:sonar.token=" + token   // The `sonar.token` property was introduced in SonarQube 10.0
      : "/d:sonar.login=" + token;  // sonar.login is obsolete
    var command = classifier.createBaseCommand().setDirectory(projectDir.toFile());
    if (step == Step.help) {
      command.addArgument("/?");
    } else {
      command
        .addArgument(step.toString())
        .addArgument(tokenProperty);
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
    for (var entry : this.environment.entrySet()) {
      command.setEnvironmentVariable(entry.getKey(), entry.getValue());
    }
    return command;
  }
}
