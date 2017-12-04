using System;
using SonarIssues.Shared;

namespace SonarIssues.ConsoleApp1
{
    public class Foo : TestEventInvoke
    {
        int bar;
        public int Bar
        {
            get { return bar; }
            set { Set(ref bar, value); }
        }
    }

    static class Program
    {
        static void Main(string[] args)
        {
            var foo = new Foo();
            foo.Bar = 1;
            if (foo.Bar != 1)
                Console.WriteLine("Fail");
        }
    }
}
