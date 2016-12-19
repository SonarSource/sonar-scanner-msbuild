import tl = require("vsts-task-lib/task");

/**
 * Data class for the SonarQube specific endpoint.
 *
 * @export
 * @class SonarQubeEndpoint
 */
export class SonarQubeEndpoint {
    constructor(public Url: string, public Token: string) {
    }

    /**
     * This static method retrieves the SonarQube endpoint associated to the current task.
     *
     * @static
     * @returns {SonarQubeEndpoint}
     *
     * @memberOf SonarQubeEndpoint
     */
    public static getTaskSonarQubeEndpoint(): SonarQubeEndpoint {
        if (tl.getEndpointUrl == null) {
            tl.debug("Could not decode the SonarQube endpoint. Please ensure you are running an agent with version 0.3.2+.");
            throw new Error();
        }

        var endpointName: string = tl.getInput("connectedServiceName", true);
        var hostUrl: string = tl.getEndpointUrl(endpointName, false);

        tl.debug("SonarQube endpoint: ${hostUrl}");

        var hostUsername: string = SonarQubeEndpoint.getSonarQubeAuthParameter(endpointName, "username");

        return new SonarQubeEndpoint(hostUrl, hostUsername);
    }

    private static getSonarQubeAuthParameter(endpoint: string, paramName: string): string {
        var scheme: string = tl.getEndpointAuthorizationScheme(endpoint, false);
        if (scheme !== "UsernamePassword") {
            throw new Error("The authorization scheme " + scheme +
                " is not supported for a SonarQube endpoint. Please use a username and a password.");
        }

        var parameter:string = tl.getEndpointAuthorizationParameter(endpoint, paramName, false);

        return parameter;
    }
}