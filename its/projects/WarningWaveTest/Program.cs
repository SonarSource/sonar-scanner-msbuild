// This code will trigger CS8981: The type name only contains lower-cased ascii characters
public class myclass
{
    public string Name { get; set; }
}

public class Program
{
    public static void Main()
    {
        var r = new myclass { Name = "test" };
        System.Console.WriteLine(r.Name);
    }
}
