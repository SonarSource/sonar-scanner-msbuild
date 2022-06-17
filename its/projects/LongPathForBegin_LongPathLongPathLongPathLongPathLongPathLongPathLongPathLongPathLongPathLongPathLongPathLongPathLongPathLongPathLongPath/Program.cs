using System;

namespace LongPathProject
{
    class Program
    {
        static void Main(string[] args)
        {
            string input = Foo.ThisIsAMethodNameWithAVeryLongMethodToForceLongUcfgName(args[0]);
            Console.WriteLine(input);
        }
    }
}
