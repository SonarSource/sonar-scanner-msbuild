using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CSharp.SDKs.Test
{
    [TestClass]
    public class CommonTest
    {
        // FIXME: This line contains S1134 warning in SL, but not in S4NET context due to current test-code support

        [TestMethod]
        public void TestMethodWithNoAssertion() // S2699: Add at least one assertion to this test case. This rule has test-only scope.
        {
            var sut = new Common();
            sut.ToString();
        }
    }
}
