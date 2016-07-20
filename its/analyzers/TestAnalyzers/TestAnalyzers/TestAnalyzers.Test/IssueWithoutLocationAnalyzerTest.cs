using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace TestAnalyzers.Test
{
    [TestClass]
    public class IssueWithoutLocationAnalyzerTest : DiagnosticVerifier
    {
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new IssueWithoutLocationAnalyzer();
        }

        //No diagnostics expected to show up
        [TestMethod]
        public void TestMethod1()
        {
            var test = @"";
            VerifyCSharpDiagnostic(test, createResult());
        }

        //Diagnostic triggered and checked for
        [TestMethod]
        public void TestMethod2()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
        }
    }";

            VerifyCSharpDiagnostic(test, createResult());
        }

        private DiagnosticResult createResult()
        {
            var id = "S1235";
            var message = "Issue without location";
            var severity = DiagnosticSeverity.Warning;

            return new DiagnosticResult
            {
                Id = id,
                Message = message,
                Severity = severity,
                Locations = null
            };
        }
    }
}