using Microsoft.CodeAnalysis;

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

        public void Execute(GeneratorExecutionContext context) =>
            context.AddSource($"Foo.cs", "class Foo {}");
    }
}
