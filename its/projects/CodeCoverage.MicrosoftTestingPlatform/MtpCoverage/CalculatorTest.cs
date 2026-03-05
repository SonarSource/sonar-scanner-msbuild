[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace MtpCoverage;

[TestClass]
public class CalculatorTest
{
    [TestMethod]
    public void Addition_ReturnsCorrectResult()
    {
        int a = 2;
        int b = 2;
        Assert.AreEqual(4, a + b);
    }
}
