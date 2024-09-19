namespace ExclusionsAndCoverage.Test;

[TestClass]
public class CalculatorTest
{
    [DataTestMethod]
    [DataRow(1, 2, 3)]
    [DataRow(2, 3, 5)]
    public void Addition_ReturnExpected(int a, int b, int expected)
    {
        var actual = Calculator.Add(a, b);

        // Assert
        Assert.AreEqual(expected, actual);
    }
}
