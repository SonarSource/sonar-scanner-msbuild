using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TestAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IssueWithoutLocationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "S1235";
        private const string Category = "Test";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerS1235Title), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerS1235MessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerS1235Description), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            Action<SyntaxTreeAnalysisContext> a = delegate (SyntaxTreeAnalysisContext c)
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, null));
            };
            context.RegisterSyntaxTreeAction(a);
        }
    }
}
