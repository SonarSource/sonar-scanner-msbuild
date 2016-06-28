using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace TestAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OneIssuePerLineAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "S1234";
        private const string Category = "Test";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            Action<SyntaxTreeAnalysisContext> a = delegate (SyntaxTreeAnalysisContext c)
            {
                IEnumerable<SyntaxToken> tokens = c.Tree.GetRoot().DescendantTokens();
                IDictionary<int, TextSpan> lineSpans = getLineSpans(c.Tree, tokens);

                int noLines = c.Tree.GetText().Lines.Count();
                for (int i = 0; i < noLines; i++)
                {
                    if(lineSpans.ContainsKey(i))
                    {
                        var location = Location.Create(c.Tree, lineSpans[i]);
                        c.ReportDiagnostic(Diagnostic.Create(Rule, location));
                    }

                }
            };
            context.RegisterSyntaxTreeAction(a);
        }

        private IDictionary<int, TextSpan> getLineSpans(SyntaxTree tree, IEnumerable<SyntaxToken> tokens)
        {
            Dictionary<int, TextSpan> dict = new Dictionary<int, TextSpan>();

            foreach(SyntaxToken t in tokens)
            {
                TextSpan span = t.Span;
                if(span.Length == 0)
                {
                    continue;
                }
                FileLinePositionSpan linePos = tree.GetLineSpan(span);
                int line = linePos.StartLinePosition.Line;

                if (dict.ContainsKey(line))
                {
                    if (span.Start < dict[line].Start)
                    {
                        dict[line] = TextSpan.FromBounds(span.Start, dict[line].End);
                    }
                    if (span.End > dict[line].End)
                    {
                        dict[line] = TextSpan.FromBounds(dict[line].Start, span.End);
                    }
                }
                else
                {
                    dict[line] = span;
                }
            }

            return dict;
        }
    }
}
