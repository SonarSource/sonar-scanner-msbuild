namespace SonarBuildFailRepro
{
    class Program
    {
        static void Main(string[] args)
        {
            Foo f = new Foo();
            f.Hello();
        }

        public void IntentionallyEmpty()
        {

        }
    }
}
