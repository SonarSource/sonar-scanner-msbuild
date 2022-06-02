using Microsoft.CodeAnalysis;
using Google.ProtoBuf;

namespace SourceGen
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // This has no implementation because there is nothing to do on init and
            // the method is required to satisfy the interface.
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // This usage is here to make sure the correct reference of the Google.Protobuf is loaded.
            var uselessObject = new SomeClass();

            string source = $@"
using System;

namespace SonarBuildFailRepro
{{
    public class Foo
    {{
        public void Hello()
        {{
            Console.WriteLine(""Hello World!"");
        }}
    }}
}}";
            context.AddSource($"Foo.cs", source);
        }
    }
}
