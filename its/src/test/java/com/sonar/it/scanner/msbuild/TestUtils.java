/*
 * Scanner for MSBuild :: Integration Tests
 * Copyright (C) 2016-2016 SonarSource SA
 * mailto:contact AT sonarsource DOT com
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
package com.sonar.it.scanner.msbuild;

import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.build.ScannerForMSBuild;
import com.sonar.orchestrator.config.Configuration;
import com.sonar.orchestrator.locator.FileLocation;
import com.sonar.orchestrator.util.Command;
import com.sonar.orchestrator.util.CommandExecutor;
import java.io.File;
import java.io.IOException;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.nio.file.DirectoryStream;
import java.nio.file.FileSystem;
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.nio.file.StandardCopyOption;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import javax.annotation.CheckForNull;
import org.apache.commons.io.FileUtils;
import org.junit.rules.TemporaryFolder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static org.assertj.core.api.Assertions.assertThat;

public class TestUtils {
  public static final String MSBUILD_PATH = "msbuild.path";
  private final static Logger LOG = LoggerFactory.getLogger(ScannerMSBuildTest.class);

  @CheckForNull
  public static String getScannerVersion() {
    Configuration configuration = Orchestrator.builderEnv().build().getConfiguration();
    return configuration.getString("scannerForMSBuild.version");
  }

  public static ScannerForMSBuild newScanner(Path projectDir) {
    String scannerVersion = getScannerVersion();

    if (scannerVersion != null) {
      LOG.info("Using Scanner for MSBuild " + scannerVersion);
      return ScannerForMSBuild.create(projectDir.toFile())
        .setScannerVersion(scannerVersion);
    } else {
      // run locally
      LOG.info("Using Scanner for MSBuild from the local build");
      Path scannerZip = Paths.get("../DeploymentArtifacts/BuildAgentPayload/Release/SonarQube.Scanner.MSBuild.zip");
      return ScannerForMSBuild.create(projectDir.toFile())
        .setScannerLocation(FileLocation.of(scannerZip.toFile()));
    }
  }

  public static Path getCustomRoslynPlugin() {
    Path customPluginDir = Paths.get("").resolve("analyzers");

    DirectoryStream.Filter<Path> jarFilter = new DirectoryStream.Filter<Path>() {
      public boolean accept(Path file) throws IOException {
        return Files.isRegularFile(file) && file.toString().endsWith(".jar");
      }
    };
    List<Path> jars = new ArrayList<>();
    try {
      Files.newDirectoryStream(customPluginDir, jarFilter).forEach(p -> jars.add(p));
    } catch (IOException e) {
      throw new IllegalStateException(e);
    }
    if (jars.isEmpty()) {
      throw new IllegalStateException("No jars found in " + customPluginDir.toString());
    } else if (jars.size() > 1) {
      throw new IllegalStateException("Several jars found in " + customPluginDir.toString());
    }

    return jars.get(0);
  }

  public static Path projectDir(TemporaryFolder temp, String projectName) throws IOException {
    Path projectDir = Paths.get("projects").resolve(projectName);
    FileUtils.deleteDirectory(new File(temp.getRoot(), projectName));
    Path tmpProjectDir = temp.newFolder(projectName).toPath();
    FileUtils.copyDirectory(projectDir.toFile(), tmpProjectDir.toFile());
    return tmpProjectDir;
  }

  public static void runMSBuildWithBuildWrapper(Orchestrator orch, Path projectDir, File buildWrapperPath, File outDir, String... arguments) {
    Path msBuildPath = getMsBuildPath(orch);

    int r = CommandExecutor.create().execute(Command.create(buildWrapperPath.toString())
      .setEnvironmentVariable("CommandPromptType", "Native")
      .setEnvironmentVariable("DevEnvDir", "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\Common7\\IDE\\")
      .setEnvironmentVariable("ExtensionSdkDir", "C:\\Program Files (x86)\\Microsoft SDKs\\Windows Kits\\10\\ExtensionSDKs")
      .setEnvironmentVariable("Framework40Version", "v4.0")
      .setEnvironmentVariable("FrameworkDir", "C:\\\\Windows\\\\Microsoft.NET\\Framework\\")
      .setEnvironmentVariable("FrameworkDIR32", "C:\\Windows\\Microsoft.NET\\Framework\\")
      .setEnvironmentVariable("FrameworkVersion", "v4.0.30319")
      .setEnvironmentVariable("FrameworkVersion32", "v4.0.30319")
      .setEnvironmentVariable("INCLUDE",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VC\\Tools\\MSVC\\14.10.25017\\ATLMFC\\include;C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VC\\Tools\\MSVC\\14.10.25017\\include;C:\\Program Files (x86)\\Windows Kits\\NETFXSDK\\4.6.1\\include\\um;C:\\Program Files (x86)\\Windows Kits\\10\\include\\10.0.15063.0\\ucrt;C:\\Program Files (x86)\\Windows Kits\\10\\include\\10.0.15063.0\\shared;C:\\Program Files (x86)\\Windows Kits\\10\\include\\10.0.15063.0\\um;C:\\Program Files (x86)\\Windows Kits\\10\\include\\10.0.15063.0\\winrt;")
      .setEnvironmentVariable("LIB",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VC\\Tools\\MSVC\\14.10.25017\\ATLMFC\\lib\\x86;C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VC\\Tools\\MSVC\\14.10.25017\\lib\\x86;C:\\Program Files (x86)\\Windows Kits\\NETFXSDK\\4.6.1\\lib\\um\\x86;C:\\Program Files (x86)\\Windows Kits\\10\\lib\\10.0.15063.0\\ucrt\\x86;C:\\Program Files (x86)\\Windows Kits\\10\\lib\\10.0.15063.0\\um\\x86;")
      .setEnvironmentVariable("LIBPATH",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VC\\Tools\\MSVC\\14.10.25017\\ATLMFC\\lib\\x86;C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VC\\Tools\\MSVC\\14.10.25017\\lib\\x86;C:\\Program Files (x86)\\Windows Kits\\10\\UnionMetadata\\10.0.15063.0\\;C:\\Program Files (x86)\\Windows Kits\\10\\References\\10.0.15063.0\\;C:\\Windows\\Microsoft.NET\\Framework\\v4.0.30319;")
      .setEnvironmentVariable("NETFXSDKDir", "C:\\Program Files (x86)\\Windows Kits\\NETFXSDK\\4.6.1\\")
      .setEnvironmentVariable("VCIDEInstallDir", "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\Common7\\IDE\\VC\\")
      .setEnvironmentVariable("VCINSTALLDIR", "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VC\\")
      .setEnvironmentVariable("VCToolsInstallDir", "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VC\\Tools\\MSVC\\14.10.25017\\")
      .setEnvironmentVariable("VCToolsRedistDir", "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VC\\Redist\\MSVC\\14.10.25017\\")
      .setEnvironmentVariable("VisualStudioVersion", "15.0")
      .setEnvironmentVariable("VS150COMNTOOLS", "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\Common7\\Tools\\")
      .setEnvironmentVariable("VSCMD_ARG_app_plat", "Desktop")
      .setEnvironmentVariable("VSCMD_ARG_HOST_ARCH", "x86")
      .setEnvironmentVariable("VSCMD_ARG_TGT_ARCH", "x86")
      .setEnvironmentVariable("VSCMD_VER", "15.0.26430.6")
      .setEnvironmentVariable("VSINSTALLDIR", "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\")
      .setEnvironmentVariable("VSSDK150INSTALL", "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Enterprise\\VSSDK")
      .setEnvironmentVariable("WindowsLibPath",
        "C:\\Program Files (x86)\\Windows Kits\\10\\UnionMetadata\\10.0.15063.0\\;C:\\Program Files (x86)\\Windows Kits\\10\\References\\10.0.15063.0\\")
      .setEnvironmentVariable("WindowsSdkBinPath", "C:\\Program Files (x86)\\Windows Kits\\10\\bin\\")
      .setEnvironmentVariable("WindowsSdkDir", "C:\\Program Files (x86)\\Windows Kits\\10\\")
      .setEnvironmentVariable("WindowsSDKLibVersion", "10.0.15063.0\\")
      .setEnvironmentVariable("WindowsSdkVerBinPath", "C:\\Program Files (x86)\\Windows Kits\\10\\bin\\10.0.15063.0\\")
      .setEnvironmentVariable("WindowsSDKVersion", "10.0.15063.0\\")
      .setEnvironmentVariable("WindowsSDK_ExecutablePath_x64", "C:\\Program Files (x86)\\Microsoft SDKs\\Windows\\v10.0A\\bin\\NETFX 4.6.1 Tools\\x64\\")
      .setEnvironmentVariable("WindowsSDK_ExecutablePath_x86", "C:\\Program Files (x86)\\Microsoft SDKs\\Windows\\v10.0A\\bin\\NETFX 4.6.1 Tools\\")
      .addArgument("--out-dir")
      .addArgument(outDir.toString())
      .addArgument(msBuildPath.toString())
      .addArguments(arguments)
      .setDirectory(projectDir.toFile()), 60 * 1000);
    assertThat(r).isEqualTo(0);

  }

  public static void runMSBuild(Orchestrator orch, Path projectDir, String... arguments) {
    Path msBuildPath = getMsBuildPath(orch);

    int r = CommandExecutor.create().execute(Command.create(msBuildPath.toString())
      .addArguments(arguments)
      .setDirectory(projectDir.toFile()), 60 * 1000);
    assertThat(r).isEqualTo(0);
  }

  private static Path getMsBuildPath(Orchestrator orch) {
    String msBuildPathStr = orch.getConfiguration().getString(MSBUILD_PATH, "C:\\Program Files (x86)\\MSBuild\\14.0\\bin\\MSBuild.exe");
    Path msBuildPath = Paths.get(msBuildPathStr).toAbsolutePath();
    if (!Files.exists(msBuildPath)) {
      throw new IllegalStateException("Unable to find MSBuild at " + msBuildPath.toString() + ". Please configure property '" + MSBUILD_PATH + "'");
    }
    return msBuildPath;
  }

  private static void replaceInZip(URI zipUri, Path src, String dest) {
    Map<String, String> env = new HashMap<>();
    env.put("create", "true");
    // locate file system by using the syntax
    // defined in java.net.JarURLConnection
    URI uri = URI.create("jar:" + zipUri);
    try (FileSystem zipfs = FileSystems.newFileSystem(uri, env)) {
      Path pathInZipfile = zipfs.getPath(dest);
      LOG.info("Replacing the file " + pathInZipfile + " in the zip " + zipUri + " with " + src);
      Files.copy(src, pathInZipfile, StandardCopyOption.REPLACE_EXISTING);
    } catch (IOException e) {
      throw new IllegalStateException(e);
    }
  }

  protected static String parseVersion() {
    try {
      String content = FileUtils.readFileToString(new File("../AssemblyInfo.Shared.cs"), StandardCharsets.UTF_8);
      return parseVersion(content);
    } catch (IOException e) {
      throw new IllegalStateException(e);
    }
  }

  private static String parseVersion(String content) {
    Pattern p = Pattern.compile("(?s).*\\[assembly: AssemblyVersion\\(\"(.*?)\"\\)].*");
    Matcher matcher = p.matcher(content);
    if (matcher.matches()) {
      return matcher.group(1);
    }
    throw new IllegalStateException("Unable to parse version from " + content);
  }
}
