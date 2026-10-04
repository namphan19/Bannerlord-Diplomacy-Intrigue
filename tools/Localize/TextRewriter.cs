using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Localize
{
    /// <summary>
    /// Rewrites the string expressions <see cref="Classifier"/> approved into <c>DiText</c> calls.
    ///
    /// A syntax rewriter, not a search and replace: a node it does not recognise comes out exactly
    /// as it went in, so a file with one shape it cannot handle is left whole rather than
    /// half-converted. That is the difference between a tool that can be run and one that has to
    /// be trusted - this conversion changes what a player reads on every screen the mod draws.
    ///
    /// The tree is rewritten top down, and only the outermost node of a string expression is asked
    /// about: an inner node of a chain that has already become a call is never re-examined.
    /// </summary>
    internal sealed class TextRewriter : CSharpSyntaxRewriter
    {
        private readonly Classifier _classifier;
        private readonly List<string> _keys = new List<string>();

        public TextRewriter(Classifier classifier)
        {
            _classifier = classifier;
        }

        /// <summary>Keys written by the last run, for the dry-run report.</summary>
        public IReadOnlyList<string> Keys => _keys;

        public int Count => _keys.Count;

        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            var replacement = TryReplace(node);
            if (replacement != null) return replacement;
            return base.VisitBinaryExpression(node);
        }

        public override SyntaxNode VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            var replacement = TryReplace(node);
            if (replacement != null) return replacement;
            return base.VisitLiteralExpression(node);
        }

        public override SyntaxNode VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            // Only when the conditional is the whole value: inside a chain it is part of one
            // sentence, and the chain above has already been turned into the call that owns it.
            if (Classifier.IsWholeValue(node))
            {
                var replacement = TryReplace(node);
                if (replacement != null) return replacement;
            }
            return base.VisitConditionalExpression(node);
        }

        private SyntaxNode TryReplace(ExpressionSyntax node)
        {
            var replacement = _classifier.Replacement(node);
            if (replacement == null) return null;

            if (replacement is InvocationExpressionSyntax call)
                _keys.Add(((LiteralExpressionSyntax)call.ArgumentList.Arguments[0].Expression).Token.ValueText);
            else if (replacement is ConditionalExpressionSyntax conditional)
            {
                foreach (var branch in new[] { conditional.WhenTrue, conditional.WhenFalse })
                    if (branch is InvocationExpressionSyntax b)
                        _keys.Add(((LiteralExpressionSyntax)b.ArgumentList.Arguments[0].Expression).Token.ValueText);
            }

            // The original node's own trivia carries its indentation, which is why a replaced
            // literal has to take it back: without this every rewritten line jumps to column 0.
            return replacement.WithTriviaFrom(node);
        }
    }
}
