import '@testing-library/jest-dom';

const expect = require("chai").expect;

describe("No assertions", function () {
    it("don't test anything", function () { // javascript:S2699 (test rule)
        var message = 'Nothing to see here'; // javascript:S1481, S1854, S3504 (prod rule)
    });
});