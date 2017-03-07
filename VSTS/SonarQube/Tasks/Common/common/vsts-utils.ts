import tl = require("vsts-task-lib/task");

import { Utils } from "./utils";

/**
 * Class provides functions for effecting change on the VSTS serverside.
 */
export class VstsServerUtils {

  /**
   * Determine if the current build was triggered by a pull request.
   *
   * Remark: this logic is temporary until the platform provides a more robust way of determining PR builds;
   * Note that PR builds are only supported on TfsGit
   * @returns {boolean} True if the build is a PR build, false otherwise.
   */
  public static isPrBuild(): boolean {
    let sourceBranch: string = tl.getVariable("build.sourceBranch");
    let sccProvider: string = tl.getVariable("build.repository.provider");

    tl.debug("Source Branch: " + sourceBranch);
    tl.debug("Scc Provider: " + sccProvider);

    return !Utils.isNullOrEmpty(sccProvider) &&
      sccProvider.toLowerCase() === "tfsgit" &&
      !Utils.isNullOrEmpty(sourceBranch) &&
      sourceBranch.toLowerCase().startsWith("refs/pull/");
  }

  public static isFeatureEnabled(featureName: string, defaultValue: boolean): boolean {
    var featureValue: string = tl.getVariable(featureName);

    if (featureValue === "true") {
      return true;
    } else if (featureValue === "false") {
      return false;
    } else {
      return defaultValue;
    }
  }
}