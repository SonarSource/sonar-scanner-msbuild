export class Utils {

    /**
     * Utility method, returns true if the given string is undefined, null or has length 0
     * @param str String to examine
     * @returns {boolean}
     */
    public static isNullOrEmpty(str:string): boolean {
        return str === undefined || str === null || str.length === 0;
    }
}