﻿namespace ProjectWithSourceGenAndAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            // The foo class is generated by the source generator.
            Foo f = new Foo();
        }

        public void IntentionallyEmpty()
        {

        }
    }
}
