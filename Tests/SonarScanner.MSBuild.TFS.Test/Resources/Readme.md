The `Sample.coverage` file is an input file for the Conv_ConvertToXml_ToolConvertsSampleFile test. It was generated in the following way:
* New .Net9.0 Console App called "ConsoleApp2"
* `Program.cs` looks like so:

```cs
namespace ConsoleApp2
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
        }

        public static bool TestMe() => true;
    }
}
```

* A second .Net9.0 MS Test project called "TestProject1" which references `ConsoleApp2.csproj`
* One test file `UnitTest1.cs`:

```cs
using ConsoleApp2;
namespace TestProject1
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var actual = Program.TestMe();
            Assert.IsTrue(actual);
        }
    }
}
```

The binary coverage file was created by calling `dotnet test --collect "Code Coverage"` in the sln directory.