
import tl = require("vsts-task-lib/task");

import {ToolRunner} from "vsts-task-lib/toolrunner";
import {SonarQubeEndpoint} from "./endpoint";
import {Utils} from "./utils";
import {VstsServerUtils} from "./vsts-utils";

export class SonarQubeParameterHelper {

     /**
      * Applies parameters for SonarQube features enabled by the user.
      * @param toolRunner     ToolRunner to add parameters to
      * @returns {ToolRunner} ToolRunner with parameters added
      */
    public static addSonarQubeParameters(toolRunner: ToolRunner): ToolRunner {
        toolRunner = SonarQubeParameterHelper.addSonarQubeConnectionParams(toolRunner);
        toolRunner = SonarQubeParameterHelper.addSonarQubeProjectParams(toolRunner);
        toolRunner = SonarQubeParameterHelper.addSonarQubeSourcesParams(toolRunner);
        toolRunner = SonarQubeParameterHelper.addSonarQubeSettingsParams(toolRunner);
        toolRunner = SonarQubeParameterHelper.addSonarQubeIssuesModeInPrBuild(toolRunner);
        return toolRunner;
}

    /**
     * Applies required parameters for connecting a Java-based plugin to SonarQube.
     * @param toolRunner     ToolRunner to add parameters to
     * @returns {ToolRunner} ToolRunner with parameters added
     */
    public static addSonarQubeConnectionParams(toolRunner: ToolRunner): ToolRunner {
        let sqEndpoint: SonarQubeEndpoint = SonarQubeEndpoint.getTaskSonarQubeEndpoint();
        toolRunner.arg("-Dsonar.host.url=" + sqEndpoint.Url);
        toolRunner.arg("-Dsonar.login=" + sqEndpoint.Token);
        return toolRunner;
    }

    /**
     * Applies parameters for manually specifying the project name, key and version to SonarQube.
     * This will override any settings that may have been specified manually by the user.
     * @param toolRunner     ToolRunner to add parameters to
     * @returns {ToolRunner} ToolRunner with parameters added
     */
    public static addSonarQubeProjectParams(toolRunner: ToolRunner): ToolRunner {
        let projectName: string = tl.getInput("projectName", true);
        let projectKey: string = tl.getInput("projectKey", true);
        let projectVersion: string = tl.getInput("projectVersion", true);
        toolRunner.arg("-Dsonar.projectKey=" + projectKey);
        toolRunner.arg("-Dsonar.projectName=" + projectName);
        toolRunner.arg("-Dsonar.projectVersion=" + projectVersion);
        return toolRunner;
    }

    /**
     * Applies parameters for manually specifying the sources path and the project base directory.
     * This will override any settings that may have been specified manually by the user.
     * @param toolRunner     ToolRunner to add parameters to
     * @returns {ToolRunner} ToolRunner with parameters added
     */
    public static addSonarQubeSourcesParams(toolRunner: ToolRunner): ToolRunner {
        let sources: string = tl.getPathInput("sources", true, true);
        toolRunner.arg("-Dsonar.sources=" + sources);
        // could also use tl.getVariable("Build.SourcesDirectory") (not sure which one is the best)
        toolRunner.arg("-Dsonar.projectBaseDir=" + tl.getVariable("System.DefaultWorkingDirectory"));
        return toolRunner;
    }

    /**
     * Applies parameters for manually specifying the settings or the settings file.
     * @param toolRunner     ToolRunner to add parameters to
     * @returns {ToolRunner} ToolRunner with parameters added
     */
    public static addSonarQubeSettingsParams(toolRunner: ToolRunner): ToolRunner {
        let settingsFile: string = tl.getPathInput("configFile", false, false);
        let settings: string = tl.getInput("cmdLineArgs", false);

        if (tl.filePathSupplied("configFile")) {
            toolRunner.arg("-Dproject.settings=" + settingsFile);
        }
        if (!Utils.isNullOrEmpty(settings)) {
            toolRunner.arg(settings); // user should take care of escaping the extra settings
        }

        return toolRunner;
    }

    /**
     * Applies parameters that will run SQ analysis in issues mode if this is a pull request build
     * @param toolRunner     ToolRunner to add parameters to
     * @returns {ToolRunner} ToolRunner with parameters added
     */
    public static addSonarQubeIssuesModeInPrBuild(toolrunner: ToolRunner): ToolRunner {
        if (VstsServerUtils.isPrBuild()) {
            console.log("Detected a PR build - running the SonarQube analysis in issues mode");

            toolrunner.arg("-Dsonar.analysis.mode=issues");
            toolrunner.arg("-Dsonar.report.export.path=sonar-report.json");
        } else {
            tl.debug("Running a full SonarQube analysis");
        }

        return toolrunner;
    }
}