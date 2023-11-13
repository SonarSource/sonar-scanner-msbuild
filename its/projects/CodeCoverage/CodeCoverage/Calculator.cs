namespace CodeCoverage;

public class Calculator
{
    public int ConditionalAdd(int a, int b, bool condition)
    {
        if (condition)
        {
            return a + b;
        }
        else
        {
            return 0;
        }
    }
}
