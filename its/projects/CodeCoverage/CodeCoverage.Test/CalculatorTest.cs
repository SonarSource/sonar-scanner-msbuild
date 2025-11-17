namespace CodeCoverage.Test;

[TestClass]
public class CalculatorTest
{
    [TestMethod]
    public void WhenConditionIsTrue_ReturnSum()
    {
        var calculator = new Calculator();

        var result = calculator.ConditionalAdd(1, 2, true);

        Assert.AreEqual(3, result);
    }
}
