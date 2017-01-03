/**
 * The Scanner CLI task logic
 */

import os = require("os");
import path = require("path");
import tl = require("vsts-task-lib/task");
import { ToolRunner } from "vsts-task-lib/toolrunner";
import { SonarQubeHelper } from "vsts-sonarqube-common/sq-helper";
import { SonarQubeParameterHelper } from "vsts-sonarqube-common/parameter-helper";

async function run(): Promise<void> {
    try {
        SonarQubeHelper.exitOnPrBuild();

        var isWindows = os.type().match(/^Win/);
        var scannerScriptExtension: string = isWindows ? ".bat" : "";
        var sonarScannerToolPath: string = path.join(__dirname, "sonar-scanner/bin/sonar-scanner" + scannerScriptExtension);
        tl.checkPath(sonarScannerToolPath, "SonarQube Scanner CLI");
        var sonarScannerTool: ToolRunner = tl.tool(sonarScannerToolPath);
        SonarQubeParameterHelper.addSonarQubeParameters(sonarScannerTool);
        await sonarScannerTool.exec();

        tl.setResult(tl.TaskResult.Succeeded, "SonarQube Analysis succeeded.");
    } catch (error) {
        tl.error("Task failed with the following error: " + error);
        tl.setResult(tl.TaskResult.Failed, "SonarQube Analysis failed.");
    }
}

run();