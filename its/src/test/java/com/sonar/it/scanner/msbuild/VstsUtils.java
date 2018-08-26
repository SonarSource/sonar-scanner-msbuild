package com.sonar.it.scanner.msbuild;

public class VstsUtils {

  static Boolean isRunningUnderVsts(){
    return System.getenv("BUILD_SOURCESDIRECTORY") != null;
  }

  static String getSourcesDirectory(){
    return GetVstsEnvironmentVariable("BUILD_SOURCESDIRECTORY");
  }

  static String getArtifactsDowloadDirectory(){
    return GetVstsEnvironmentVariable("SYSTEM_ARTIFACTSDIRECTORY");
  }

  private static String GetVstsEnvironmentVariable(String name){
    String value = System.getenv(name);
    if (name == null){
      throw new IllegalStateException("Unable to find VSTS environment variable: " + name);
    }
    return value;
  }
}
