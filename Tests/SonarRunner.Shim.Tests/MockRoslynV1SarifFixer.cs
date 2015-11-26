using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SonarQube.Common;

namespace SonarRunner.Shim.Tests
{
    class MockRoslynV1SarifFixer : IRoslynV1SarifFixer
    {

        #region Test Hooks

        public bool ReturnVal { get; set; }

        public int CallCount { get; set; }

        public MockRoslynV1SarifFixer(bool returnVal)
        {
            this.ReturnVal = returnVal;
            this.CallCount = 0;
        }

        #endregion

        #region IRoslynV1SarifFixer

        public bool FixRoslynV1SarifFile(string sarifFilePath, ILogger logger)
        {
            CallCount++;
            return ReturnVal;
        }

        #endregion
    }
}
