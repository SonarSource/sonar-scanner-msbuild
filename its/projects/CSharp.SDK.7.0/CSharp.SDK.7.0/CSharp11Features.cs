using System.Diagnostics.CodeAnalysis;

namespace CSharp.SDK
{
    class CSharp11Features
    {
        // Raw string literals https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#raw-string-literals
        readonly string rawStringLiteral1 = """test"""; // S2479, S1144
        readonly string rawStringLiteral2 = $$""" Here are two ints: {}"""; // S1144

        // Required modifier https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required
        public required string FooProperty { get; init; }

        [SetsRequiredMembers]
        public CSharp11Features(string fooProperty)
        {
            FooProperty = fooProperty;
        }
    }

    public class FooBar
    {
        // Extended nameof scope: https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#extended-nameof-scope
        [Obsolete(nameof(argument.Trim))]
        public void Bar(string argument)
        {
            // List Patterns https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#list-patterns
            int[] numbers = { 1, 2, 3 };
            Console.WriteLine(numbers is [0 or 1, <= 2, >= 3]);
        }
    }

    // File scoped types https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#file-scoped-types
    file class FileScopedType { }

    // Generic attributes https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#generic-attributes
    public class GenericAttribute<T> : Attribute { } // S2326

}
