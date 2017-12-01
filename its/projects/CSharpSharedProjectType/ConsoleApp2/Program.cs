using SonarIssues.Shared;
using System;

namespace SonarIssues.ConsoleApp2
{
	public class Foo : TestEventInvoke
	{
		int bar;
		public int Bar { get => bar; set => Set(ref bar, value); }
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
