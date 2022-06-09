using Microsoft.CodeAnalysis;
using Google.Protobuf;

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
            var uselessObject = new FakeGoogleProtobufClass();

            context.AddSource($"Foo.cs", "class Foo {}");
        }
    }
}
