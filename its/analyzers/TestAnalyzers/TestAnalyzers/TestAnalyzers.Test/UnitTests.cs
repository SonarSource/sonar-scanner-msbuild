using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestAnalyzers;
using TestHelper;

namespace TestAnalyzers.Test
{
    [TestClass]
    public class UnitTest : DiagnosticVerifier
    {
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new OneIssuePerLineAnalyzer();
        }

        //No diagnostics expected to show up
        [TestMethod]
        public void TestMethod1()
        {
            var test = @"";
            VerifyCSharpDiagnostic(test);
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

            DiagnosticResult[] expectedResults =
            {
                createResult(2,5),
                createResult(3,5),
                createResult(4,5),
                createResult(5,5),
                createResult(6,5),
                createResult(7,5),
                createResult(9,5),
                createResult(10,5),
                createResult(11,9),
                createResult(12,9),
                createResult(13,9),
                createResult(14,5),
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        private DiagnosticResult createResult(int line, int column)
        {
            var id = "S1234";
            var message = "One issue per line";
            var severity = DiagnosticSeverity.Warning;

            return new DiagnosticResult
            {
                Id = id,
                Message = message,
                Severity = severity,
                Locations = new[] { new DiagnosticResultLocation("Test0.cs", line, column) }
            };
        }
    }
}