using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Localize
{
    /// <summary>One string expression found in the source, and what the tool decided about it.</summary>
    internal sealed class Candidate
    {
        public string File;
        public int Line;
        public int Column;
        public string Area = "";
        public string Verdict = VerdictManual;
        public string Reason = "";
        public string Key = "";
        public string English = "";
        public string Variables = "";
        public string Code = ";";
        public string Parent = ";";

        public const string VerdictConvert = "convert";   // auto-rewritable into DiText.T / DiText.O
        public const string VerdictManual = "manual";     // player-facing, but a shape the rewriter leaves alone
        public const string VerdictSkip = "skip";         // not player-facing: log, id, format, asset
    }

    /// <summary>
    /// The boundary rule of story 4.1 ST-2, made executable: a literal is either a string a
    /// player reads - and gets a key - or it is not one and stays English. Every decision carries
    /// its reason, so "why was this one left alone" is answerable from the report alone.
    ///
    /// A literal is player-facing when it reaches a screen. Which is decided from the sink it
    /// flows into, not from the file it sits in, because one file holds both: DiplomacyMenu
    /// writes menu labels and ids a few characters apart, and <c>switch (selected) { case "wars":
    /// ... }</c> is an id, not a word.
    ///
    /// What it cannot do is know intent. A literal that is an id but does not look like one - a
    /// saved key, a sprite name - is reported as <c>manual</c> rather than rewritten, and the
    /// dry-run report is read by hand. That review is why <c>rewrite</c> needs <c>--apply</c>.
    /// </summary>
    internal sealed class Classifier
    {
        private readonly string _file;
        private readonly SyntaxTree _tree;
        private readonly Microsoft.CodeAnalysis.Text.TextLine[] _lines;
        private readonly HashSet<string> _caseLabels;

        private readonly List<Candidate> _candidates = new List<Candidate>();

        public Classifier(string file, SyntaxTree tree, string repo)
        {
            _file = file;
            _tree = tree;
            Repo = repo;
            Area = Paths.AreaOf(file, repo);
            Exceptions.SetRepo(repo);
            _lines = tree.GetText()?.Lines.ToArray();
            _caseLabels = CaseLabelValues(tree.GetRoot());
            foreach (var expr in StringExpressions(tree.GetRoot()))
            {
                var literal = FirstLiteral(expr);
                if (literal == null) continue;
                var candidate = Describe(expr, literal);
                if (candidate != null) _candidates.Add(candidate);
            }
        }

        public string Area { get; }
        public IReadOnlyList<Candidate> Candidates => _candidates;

        private string Repo { get; }

        /// <summary>
        /// The <c>DiText</c> call for one expression, or null when the tool will not touch it.
        /// The rewriter calls this per node and leaves everything else exactly as written.
        ///
        /// A conditional that chooses between two sentences becomes <em>two</em> calls, joined by
        /// the same <c>? :</c> with the condition left alone. <c>cond ? "of 100 - no ceiling" :
        /// "of {N} - the most this handler can build"</c> has two sentences in it, and one key
        /// would force a language to choose a wording for the case the player is not in.
        /// </summary>
        public ExpressionSyntax Replacement(ExpressionSyntax expr)
        {
            var literal = FirstLiteral(expr);
            var c = Describe(expr, literal);
            if (c == null || c.Verdict != Candidate.VerdictConvert) return null;

            // Always T, never O: the rewriter only ever replaces a literal, a chain or a
            // conditional, so the value always lands where a string is wanted - an argument of a
            // TextObject construction, a view-model property, an InquiryData field. The engine
            // builds the TextObject from that string itself.
            const string method = "T";
            var parts = Flatten(expr).ToList();

            var choices = parts.Select(FindTextChoice).Where(c => c != null).ToList();
            if (choices.Count == 0)
            {
                var template = Template(expr, null, true, out var vars);
                return Build(method, UniqueKey(template, vars.Select(v => v.Name).ToArray()), template, vars, expr);
            }
            if (choices.Count > 1)
            {
                // Two conditionals in one sentence: three or four wordings, and which pair goes
                // with which is a judgement about the sentence rather than about the syntax.
                return null;
            }

            var choice = choices[0];
            var whenTrue = Template(expr, choice, true, out var varsTrue);
            var whenFalse = Template(expr, choice, false, out var varsFalse);

            // A branch with no words at all stays as it is: `cond ? "your side" : string.Empty`
            // is one key and an empty string, not two keys one of which says nothing.
            var trueCall = string.IsNullOrEmpty(whenTrue)
                ? (ExpressionSyntax)SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(""))
                : Build(method, UniqueKey(whenTrue, varsTrue.Select(v => v.Name).ToArray()), whenTrue, varsTrue, expr);
            var falseCall = string.IsNullOrEmpty(whenFalse)
                ? (ExpressionSyntax)SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(""))
                : Build(method, UniqueKey(whenFalse, varsFalse.Select(v => v.Name).ToArray()), whenFalse, varsFalse, expr);

            // The condition keeps its own text; the punctuation around it is rebuilt so the two
            // calls read as `cond ? T(...) : T(...)` and not as `cond?T(...):T(...)`.
            var conditional = SyntaxFactory.ConditionalExpression(choice.Condition.WithoutTrivia(), trueCall, falseCall);
            conditional = conditional.ReplaceToken(conditional.QuestionToken,
                conditional.QuestionToken.WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space))
                    .WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space)));
            conditional = conditional.ReplaceToken(conditional.ColonToken,
                conditional.ColonToken.WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space))
                    .WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space)));
            return conditional.WithTriviaFrom(expr);
        }

        /// <summary>
        /// The key for one English, and unique across the whole run.
        ///
        /// Two sentences can open the same way - "Peace is negotiated at the diplomacy table" and
        /// "Peace is negotiated at the diplomacy table (Ctrl+D)" share their first six words, and
        /// two files can each hold one. A shared key would give one language one string for two
        /// different sentences, which is the one mistake a translation file cannot be checked for,
        /// so the second takes <c>_2</c>.
        ///
        /// The same English in two places, on the other hand, keeps one key: two rows that read
        /// "Refuse" should be translated once.
        /// </summary>
        private string UniqueKey(string template, string[] variables)
        {
            var key = Keys.For(Area, template, variables);
            if (Keys.TemplateOf(key) == template) return key;
            if (!Keys.WasMinted(template)) Keys.Mint(key, template);

            for (var n = 2; ; n++)
            {
                var candidate = key + "_" + n;
                if (Keys.TemplateOf(candidate) == null || Keys.TemplateOf(candidate) == template)
                {
                    Keys.Mint(candidate, template);
                    return candidate;
                }
            }
        }

        // ----- the decision ------------------------------------------------------

        private Candidate Describe(ExpressionSyntax expr, LiteralExpressionSyntax literal)
        {
            if (literal == null) return null;
            var value = literal.Token.ValueText;
            if (value.Length == 0) return null;

            var span = literal.GetLocation().GetLineSpan().StartLinePosition;
            var c = new Candidate
            {
                File = Paths.Relative(Repo, _file),
                Line = span.Line + 1,
                Column = span.Character + 1,
                Area = Area,
                English = value,
                Code = expr.ToString(),
                Parent = expr.Parent?.Kind().ToString() ?? "none",
            };

            var root = _tree.GetRoot();
            var skip = ReasonNotPlayerFacing(c, expr, root);
            if (skip != null)
            {
                c.Verdict = Candidate.VerdictSkip;
                c.Reason = skip;
                return c;
            }

            var relative = c.File.Replace('\\', '/');
            var method = EnclosingMethod(expr);
            var spread = AppendSpread(method);

            // The template, not the first literal: "Vassal of " + name is a whole label, and only
            // the completed sentence can tell a label from a piece of one.
            var choice = Flatten(expr).Select(FindTextChoice).FirstOrDefault(x => x != null);
            var template = Template(expr, choice, true, out var vars);
            c.English = template;
            c.Variables = string.Join(",", vars.Select(v => v.Name));
            c.Key = Keys.For(Area, template, vars.Select(v => v.Name).ToArray());

            // An exception is a judgement about a whole sentence, so it is read here, where the
            // whole sentence exists. The skip-reason chain above runs before the template is built
            // and sees only the first literal, which is how a rule naming the wording found nothing
            // to match: `c.English` was "Refused: " rather than "Refused: {REASON}".
            var exempt = Exceptions.IsExempt(_file, method, c.Line, c.English);
            if (exempt != null)
            {
                c.Verdict = Candidate.VerdictSkip;
                c.Reason = exempt;
                return c;
            }

            if (relative.Contains("/UI/"))
            {
                c.Verdict = Candidate.VerdictConvert;
                c.Reason = "under UI/: drawn on a screen";
            }
            else if (relative.EndsWith("ModSettings.cs") || IsMcmAttribute(expr))
            {
                if (!IsMcmDisplayText(expr))
                {
                    c.Verdict = Candidate.VerdictSkip;
                    c.Reason = "an MCM setting's other arguments: a value format and a group name, not sentences";
                    return c;
                }
                c.Verdict = Candidate.VerdictConvert;
                c.Reason = "MCM setting name, hint or group";
            }
            else if (relative.EndsWith("SubModule.cs") && ReachesLogNotify(expr))
            {
                c.Verdict = Candidate.VerdictConvert;
                c.Reason = "startup notice the player reads";
            }
            else
            {
                var sink = PlayerFacingSink(expr);
                if (sink != null)
                {
                    c.Verdict = Candidate.VerdictConvert;
                    c.Reason = "player-facing sink: " + sink;
                }
                else if (TextProducers.IsTextProducer(_file, EnclosingMethod(expr)))
                {
                    // A band label, a chip name, a peace term's meaning: these live outside UI/
                    // because they belong to the system that computes them, and they are read on
                    // a screen like any other. Naming the method is the only honest signal - the
                    // value never reaches a sink of its own.
                    c.Verdict = Candidate.VerdictConvert;
                    c.Reason = "returned by " + EnclosingMethod(expr)?.Identifier.ValueText + "(), which exists to produce text for a screen";
                }
                else
                {
                    c.Verdict = Candidate.VerdictSkip;
                    c.Reason = "not a screen: outside UI/, no sink, and no text-producing method";
                }
            }

            // The shape rules come last, and only for something a player actually reads. Run first,
            // they relabelled a log line or a telemetry row as "a human decides", which overstates
            // the work: the word-producer rule did exactly that to 34 rows on 2026-10-04, none of
            // which is a screen.
            if (c.Verdict != Candidate.VerdictConvert) return c;

            if (IsFragment(template))
            {
                c.Verdict = Candidate.VerdictManual;
                c.Reason = "a fragment of a longer sentence (" + template.Trim()
                           + "): it was one piece of a sentence built elsewhere, so it is keyed with that sentence by hand";
                return c;
            }

            // A conditional with another conditional inside it, or a sentence assembled somewhere
            // else, is untranslatable wherever it sits - and converting it anyway is worse than
            // leaving it English, because the English ends up inside a variable: half a sentence in
            // one language and half in another.
            var shape = ShapeVerdict(expr, spread, choice);
            if (shape != null)
            {
                c.Verdict = Candidate.VerdictManual;
                c.Reason = shape;
            }

return c;
        }

        /// <summary>Why this literal is not something a player reads, or null when it is.</summary>
private string ReasonNotPlayerFacing(Candidate c, ExpressionSyntax expr, SyntaxNode root)
        {
            // A literal that is an argument of a DiText call is the converted form of itself: either the
            // key or the English a previous pass wrote. And an expression that *contains* one has
            // been converted too - `cond ? T(..) : T(..)` is itself a string expression, and
            // without this every pass would wrap the last one again. Together they are what makes
            // the tool idempotent.
            if (expr.Ancestors().OfType<InvocationExpressionSyntax>().Any(Calls.IsDiText))
                return "already keyed: an argument of DiText";
            if (expr.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(Calls.IsDiText))
                return "already keyed: it holds a DiText call";

if (IsCaseLabel(expr)) return "switch label: an id the code compares, not a word";
            if (_caseLabels.Contains(FirstLiteral(expr).Token.ValueText))
                return "value used as a switch id in this file";
            if (IsCompared(expr)) return "compared with == or !=: an id, not a word";

            // `route: "player_voluntary"` - a telemetry or event id, lowercase with an underscore
            // in it. No English sentence is written that way, and an id that goes through
            // localization is an id the log and `analyse-log.py` can no longer find.
            if (Regex.IsMatch(c.English, @"^[a-z][a-z0-9]*(_[a-z0-9]+)+$"))
                return "an event or route id: lowercase_with_underscores, never a sentence";

            // A literal handed to one of our own methods whose parameter only ever reaches a log
            // line is not player-facing, whichever folder it is written in. `Guard("Raising the
            // budget", …)` in the Intelligence view model looks like a screen label and is not: the
            // string appears in exactly one place inside Guard, and it is Log.Error. Following the
            // parameter is the only way to tell, and the folder cannot.
            var logged = PassedToLogOnly(expr);
            if (logged != null) return logged;

            foreach (var call in expr.Ancestors().OfType<InvocationExpressionSyntax>())
            {
                var callee = Calls.Name(call);
                if (Logger.IsLoggerCall(callee)) return "logger: written to the mod log or the console, English by decision";
                if (FormatCalls.IsFormatCall(callee)) return "format string: shapes a number or a date, never read";
                if (FormatCalls.IsIdentityCall(callee)) return "compared for identity, never shown";
                if (Wiring.IsWiringCall(callee)) return "prefab wiring: names a prefab, a layer or an injected attribute";
            }

            // UIExtenderEx patches prefabs by writing attribute values into an XML document.
            // `new Attribute("Brush", "Header.Tab.Center")` is the game's own markup language, and
            // `LoadXml("<DiTabs>...")` is a prefab fragment: translating either breaks the patch.
            if (expr.Ancestors().OfType<ObjectCreationExpressionSyntax>()
                .Any(o => o.Type.ToString().EndsWith("Attribute", StringComparison.Ordinal)))
                return "injected prefab attribute: markup, not a sentence";
            if (Wiring.IsAssetType(expr))
                return "a layer or movie name: the game resolves it as a prefab, not as text";

            // A prefab is addressed by a query path, and a view-model mixin by the name of the
            // method it wraps. Both are single words or bracketed paths, never sentences.
            if (LooksLikePrefabPath(c.English)) return "prefab query path, not text";
            if (expr.Ancestors().OfType<AttributeSyntax>().Any(a =>
                    a.Name.ToString().StartsWith("ViewModelMixin", StringComparison.Ordinal)
                    || a.Name.ToString().StartsWith("PrefabExtension", StringComparison.Ordinal)
                    || a.Name.ToString().StartsWith("ViewModelCreation", StringComparison.Ordinal)))
                return "attribute argument: a prefab or method name, not text";

            var method = EnclosingMethod(expr);
            if (method != null && method.Identifier.ValueText == "ToString"
                && method.Parent is MethodDeclarationSyntax md && md.Modifiers.Any(SyntaxKind.OverrideKeyword))
                return "ToString(): a dump read from the console, not a screen";

            var constant = ConstantContext(expr);
            if (constant != null) return constant;

            // The exemption is read in Describe, after the whole sentence has been built.
            //
            if (Regex.IsMatch(c.English, "^#[0-9A-Fa-f]{6,8}$")) return "colour";
            if (expr.Ancestors().OfType<ObjectCreationExpressionSyntax>()
                .Any(o => o.Type.ToString().EndsWith("Color", StringComparison.Ordinal))) return "colour";
            if (expr.Ancestors().OfType<MemberAccessExpressionSyntax>()
                .Any(m => m.Name.Identifier.ValueText.EndsWith("Brush", StringComparison.Ordinal))) return "brush or sprite name";
            if (Regex.IsMatch(c.English, @"^[\s\-–—:;,. /|?!*+=<>()^%&…·]+$"))
                return "separator or placeholder glyph, not words: structure a translator cannot improve";
            if (Regex.IsMatch(c.English, @"^-?\d+([.,]\d+)?$"))
                return "a bare number: a value shown before it is filled in, not a sentence";

            return null;
        }

        /// <summary>
        /// Outside <c>UI/</c> a literal only counts when it reaches something the engine shows.
        /// The sink is named so the report says which one, because that list is the rule and it
        /// should be readable in one screen.
        /// </summary>
private string PlayerFacingSink(ExpressionSyntax expr)
        {
            foreach (var node in expr.Ancestors())
            {
                // A lambda is its own context, and the walk stops there. The click handler passed
                // to ShowInquiry sits inside the inquiry's argument list, but what it writes is a
                // trust record or a log line, not something on the inquiry: the sink above it says
                // nothing about it. (Two strings in one file with the same words is what this
                // caught: "refused our peace offer" was being keyed on the strength of an inquiry
                // it is not part of.)
                if (node is AnonymousFunctionExpressionSyntax) break;
                if (!(node is InvocationExpressionSyntax call)) continue;

                var callee = Calls.Name(call);
                if (callee != null && Sink.IsDisplayCall(callee)) return callee;
            }
            if (expr.Ancestors().OfType<ObjectCreationExpressionSyntax>()
                .Any(o => o.Type.ToString() == "TextObject")) return "new TextObject(...)";
            return null;
        }

        private bool ReachesLogNotify(ExpressionSyntax expr)
        {
            return expr.Ancestors().OfType<InvocationExpressionSyntax>()
                .Any(i => (Calls.Name(i) ?? "").StartsWith("Log.Notify", StringComparison.Ordinal));
        }

        private static bool IsMcmAttribute(ExpressionSyntax expr)
        {
            return expr.Ancestors().OfType<AttributeSyntax>()
                .Select(a => a.Name.ToString())
                .Any(n => n.StartsWith("SettingProperty", StringComparison.Ordinal));
        }

        /// <summary>
        /// Which of an MCM attribute's arguments a player reads: the setting's own name (the first
        /// positional one), its <c>HintText</c>, and a group name. The rest are the value's bounds
        /// and its number format - <c>SettingPropertyFloatingInteger("War exhaustion rate",
        /// 0.25f, 4f, "0.00")</c> has three words in it and only the first is text.
        /// </summary>
        private static bool IsMcmDisplayText(ExpressionSyntax expr)
        {
            foreach (var argument in expr.Ancestors().OfType<ArgumentSyntax>())
            {
                var named = argument.NameColon?.Name.Identifier.ValueText;
                if (named != null)
                    return named == "HintText" || named == "DisplayName" || named == "Text";

                var attribute = argument.Parent?.Parent as AttributeSyntax;
                var list = attribute?.ArgumentList;
                if (list == null) return true;                       // not an attribute argument
                return list.Arguments.Count > 0 && list.Arguments[0].Expression == argument.Expression;
            }
            return false;
        }

        private static bool IsCaseLabel(ExpressionSyntax expr)
        {
            return expr.Ancestors().Any(a => a is CaseSwitchLabelSyntax || a is CasePatternSwitchLabelSyntax);
        }

        /// <summary>
        /// <c>if (kind == "war")</c> and <c>x != "Defensive pact"</c> ask a question; they never
        /// show anything. The <c>Equals</c> method is caught by name, these are its operator form.
        /// </summary>
        private static bool IsCompared(ExpressionSyntax expr)
        {
            var child = (ExpressionSyntax)expr;
            foreach (var node in expr.Ancestors())
            {
                if (node is BinaryExpressionSyntax binary
                    && (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression)))
                    return binary.Left == child || binary.Right == child;
                if (node is StatementSyntax) return false;
                child = node as ExpressionSyntax;
                if (child == null) return false;
            }
            return false;
        }

        /// <summary>
        /// A Gauntlet query - <c>descendant::ButtonWidget[@CommandParameter.Click='3']</c> - or a
        /// prefab path. A sentence never looks like one, and a wrong guess here would hand a
        /// translator a query string.
        /// </summary>
        private static bool LooksLikePrefabPath(string value)
        {
            return value.Contains("::") || value.Contains("[@") || value.Contains("]/")
                || (value.StartsWith("/", StringComparison.Ordinal) && value.Contains('/'));
        }

        /// <summary>The method a literal sits in, for the rules that are about a method's role.</summary>
        internal static MethodDeclarationSyntax EnclosingMethod(SyntaxNode node)
        {
            return node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        }

        /// <summary>
        /// Why this shape has to be written by hand, or null when the rewriter can own it.
        ///
        /// The three shapes are the ones a key cannot describe:
        ///  - the string is not one value but part of a larger expression the tool cannot see
        ///    (a fragment assigned to a local and concatenated later);
        ///  - a sentence spread over several <c>sb.Append</c> statements, where a language cannot
        ///    reorder words across statements;
        ///  - a conditional with another conditional inside it, which would put its English inside
        ///    a variable. Two conditionals in one sentence are the same problem: which wording goes
        ///    with which is a judgement about the sentence, not about the syntax.
        /// </summary>
        private static string ShapeVerdict(ExpressionSyntax expr, int spread, TextChoice choice)
        {
            if (!IsInConvertiblePosition(expr))
                return "built in pieces elsewhere, or not a value the rewriter can own";

            // Both branches when it is a choice the rewriter turns into two keys, and the whole
            // expression either way: a variable can hide in a branch as easily as in the line.
            var behind = EnglishBehindVariables(expr, choice);
            if (behind != null) return behind;

            if (spread > 1)
                return "one sentence spread over " + spread + " Append calls: a language cannot reorder words across statements, so it is written by hand (story 4.1 ST-5)";
            if (!SupportsChoices(expr))
                return "a conditional inside a clause: it would end up inside a variable, untranslatable, so it is written by hand";
            return null;
        }

        /// <summary>
        /// A conditional that offers two sentences - <c>cond ? "entitles land" : ""</c> - is a
        /// choice between two wordings, so it wants two keys. One conditional is handled; two in
        /// one sentence is left to a human.
        /// </summary>
        private static bool SupportsChoices(ExpressionSyntax expr)
        {
            var parts = Flatten(expr).ToList();
            if (parts.Select(FindTextChoice).Count(c => c != null) > 1) return false;

            // A conditional buried inside one part is the same problem one level down: the clause
            // would end up inside a {VARIABLE}, untranslatable, and the sentence would read half in
            // one language and half in another.
            foreach (var part in parts)
            {
                if (FindTextChoice(part) != null) continue;

                // A conditional the tool will not rewrite as two whole sentences - a branch that is
                // itself a concatenation, a branch that is punctuation, a branch that is a value -
                // must never be treated as an opaque {VARIABLE}. Its text would leave the English
                // without a trace, and the screen would silently lose a word. This is not
                // hypothetical: on 2026-10-04 the dry run offered
                //     Clan {NAME}, the crown        ->  Clan {NAME}{TOLOWERINVARIANT}
                //     Speaks through nobody         ->  Speaks through {TOSTRING}
                //     The network can grow to 40; it stands at 20.0 now.
                //                                     ->  ... can grow to {CEILINGOF}{TOSTRING}
                // and applying it would have deleted English from three screens. IsTextBranch is
                // what declines these, and the loop below only looked for a conditional nested one
                // level down, so the conditional sitting in the part itself fell through.
                if (part is ConditionalExpressionSyntax) return false;

                if (part.DescendantNodesAndSelf().OfType<ConditionalExpressionSyntax>()
                    .Any(c => IsTextBranch(c.WhenTrue) && IsTextBranch(c.WhenFalse)
                              && !c.DescendantNodes().OfType<ConditionalExpressionSyntax>().Any()))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// A piece of a longer sentence rather than a sentence: <c>" are "</c>, <c>" and you"</c>,
        /// <c>" - " </c>. It has space on both sides and at most two words, which is how C# spells
        /// "this string goes on next to something else".
        ///
        /// Keying it would be worse than leaving it: a translator would be handed <c>" are "</c>
        /// with nothing to reorder it around, and the sentence it belongs to would stay English in
        /// the middle of a translated line. Those sites are written by hand, as one sentence with
        /// its variables (story 4.1 R3, ST-5).
        /// </summary>
        private static bool IsFragment(string template)
        {
            if (template == template.Trim()) return false;

            // A slot makes it a sentence. "Vassal of {NAME}" is a whole label with a name in it;
            // only text with no slot and at most two words is glue that belongs inside a
            // sentence someone else finishes - " are ", " - us", "denars.".
            if (template.Contains("{")) return false;
            var words = Regex.Matches(template.Trim(), "[A-Za-z]+").Count;
            return words <= 2;
        }

        /// <summary>
        /// Why a variable in this expression carries English rather than a value, or null when every
        /// variable holds a value.
        ///
        /// R5 lets a value travel as a <c>{VARIABLE}</c>, and the value is a number or a name. A
        /// *word* or a *sentence* travelling that way is the same hole one level out: the language
        /// is asked to supply English inside its own sentence. Three shapes reach here, all found on
        /// 2026-10-04:
        ///
        ///  - a local assigned a conditional of two words, as in
        ///    <c>var verb = leader == Hero.MainHero ? " are " : " is ";</c>, which would have made
        ///    <c>{WHO}{VERB}held - 3 of 30 days</c> a key;
        ///  - a local assigned by a method this project lists as producing text, as in
        ///    <c>var side = SideChange.SideName(...)</c>, which would have made
        ///    <c>Pay {PRICE} - they come over to {SIDE}</c> a key;
        ///  - an <c>out</c> parameter, which is by definition something the callee computed, and
        ///    for <c>TreatyRegistry.Sign</c> it is a whole refusal sentence - <c>{REASON}</c>.
        ///
        /// One hop through the locals of the enclosing method, which covers the shapes above and
        /// stops there on purpose: a second hop is dataflow analysis, and a wrong answer from one
        /// would put an English word inside a translation nobody can fix. What one hop cannot see
        /// goes in <c>text-producers.txt</c> or <c>exceptions.txt</c>, which a person reads.
        /// </summary>
        private static string EnglishBehindVariables(ExpressionSyntax expr, TextChoice choice)
        {
            var method = EnclosingMethod(expr);
            if (method == null) return null;

            var declared = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
            foreach (var declarator in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (declarator.Initializer?.Value == null) continue;
                if (declared.ContainsKey(declarator.Identifier.ValueText)) continue;
                declared[declarator.Identifier.ValueText] = declarator.Initializer.Value;
            }

            // A name an out argument introduced. It has no declarator initialiser to look through,
            // so without this list "out var reason" reads as a plain identifier and the refusal
            // sentence behind it slips into a key - which is what happened to five sites in
            // DiplomacyMenu on 2026-10-04 ("Refused: {REASON}", "Failed: {FAILED}").
            var outNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var argument in method.DescendantNodes().OfType<ArgumentSyntax>())
            {
                // Either signal: the keyword, or the fact that the argument *is* a declaration,
                // which is what `out var reason` parses to.
                var declares = argument.Expression is DeclarationExpressionSyntax;
                if (!declares && !argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword)) continue;
                foreach (var declarator in argument.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                    outNames.Add(declarator.Identifier.ValueText);
            }

            // The parts of the expression, and of both branches when it is a choice the rewriter
            // turns into two keys - a variable can hide in a branch as easily as in the whole.
            var parts = new List<ExpressionSyntax>(Flatten(expr));
            if (choice != null)
            {
                if (choice.WhenTrue != null) parts.AddRange(Flatten(choice.WhenTrue));
                if (choice.WhenFalse != null) parts.AddRange(Flatten(choice.WhenFalse));
            }

            foreach (var part in parts.Concat(parts.SelectMany(RightHandSide)))
            {
                if (part is IdentifierNameSyntax named && outNames.Contains(named.Identifier.ValueText))
                    return "a variable filled by an out parameter, which is a sentence the callee built, so the line would read half in one language and half in another";

                var throughLocal = part is IdentifierNameSyntax identifier
                                   && declared.TryGetValue(identifier.Identifier.ValueText, out _);
                var resolved = throughLocal
                    ? declared[((IdentifierNameSyntax)part).Identifier.ValueText]
                    : part;

                var isOut = resolved.DescendantNodesAndSelf().OfType<ArgumentSyntax>()
                    .Any(a => a.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword));
                if (!isOut && resolved is DeclarationExpressionSyntax declaration)
                    isOut = declaration.FirstAncestorOrSelf<ArgumentSyntax>()?
                        .RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword) == true;
                if (isOut)
                    return "a variable filled by an out parameter, which is a sentence the callee built, so the line would read half in one language and half in another";

                // Only through a local. A conditional written in the expression is what the rewriter
                // turns into two whole keys - a plural, a two-wording label - and looking inside it
                // finds the next conditional, which is a value. The same conditional reached through
                // a local is the other thing entirely: the local holds a finished English fragment
                // the caller cannot reorder, so there is nothing left to key.
                if (throughLocal)
                {
                    var conditional = resolved as ConditionalExpressionSyntax
                                      ?? resolved.DescendantNodesAndSelf().OfType<ConditionalExpressionSyntax>().FirstOrDefault();
                    if (conditional != null && IsTextBranch(conditional.WhenTrue) && IsTextBranch(conditional.WhenFalse))
                        return "a variable whose value is a piece of English the code picks at run time (\" are \" / \" is \"), so a language would have to supply the English words inside its own sentence";
                }

                var producer = TextProducers.WordProducerIn(resolved);
                if (producer != null)
                    return "a variable filled by " + producer + "(), which returns text rather than a value, so the sentence would read half in one language and half in another";
            }
            return null;
        }

        /// <summary>The expressions an expression depends on: its arguments, and a call's receiver.</summary>
        private static IEnumerable<ExpressionSyntax> RightHandSide(ExpressionSyntax part)
        {
            if (part is InvocationExpressionSyntax call)
            {
                foreach (var a in call.ArgumentList.Arguments)
                {
                    if (a.Expression != null) yield return a.Expression;
                }
            }
            if (part is MemberAccessExpressionSyntax member && member.Expression != null)
                yield return member.Expression;
        }

        /// <summary>
        /// How many <c>sb.Append…</c> calls one method spends on its text.
        ///
        /// A sentence written as <c>Append("Influence ").Append(base).Append(" + ")</c> is one
        /// sentence, and a key per fragment would be a key per word - untranslatable, because no
        /// language puts "x" before the number it multiplies. Past one call the shape is a method
        /// that has to be rewritten by hand into whole sentences (story 4.1 ST-5), so the tool
        /// leaves it and says why.
        /// </summary>
        private static int AppendSpread(MethodDeclarationSyntax method)
        {
            if (method == null) return 0;
            return method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Count(i => i.Expression is MemberAccessExpressionSyntax ma
                            && ma.Name.Identifier.ValueText.StartsWith("Append", StringComparison.Ordinal));
        }

        /// <summary>
        /// A <c>const</c> field, a <c>const</c> local or a default parameter value has to be a
        /// compile-time constant, so a <c>DiText</c> call cannot go there. Those three places are
        /// rewritten by hand instead - either the declaration stops being const, or the label moves
        /// to where it is used.
        /// </summary>
        private static string ConstantContext(ExpressionSyntax expr)
        {
            if (expr.Ancestors().OfType<ParameterSyntax>().Any(p => p.Default?.Value == expr))
                return "a default parameter value: a compile-time constant, so it is keyed by hand";

            foreach (var field in expr.Ancestors().OfType<FieldDeclarationSyntax>())
                if (field.Modifiers.Any(SyntaxKind.ConstKeyword)) return "a const field: a compile-time constant, so it is keyed by hand";

            foreach (var local in expr.Ancestors().OfType<LocalDeclarationStatementSyntax>())
                if (local.IsConst) return "a const local: a compile-time constant, so it is keyed by hand";

            return null;
        }

        /// <summary>
        /// Whether this literal arrives at a parameter that only a log line ever reads.
        ///
        /// One hop, and only into a method declared in the same file: enough for the shape the mod
        /// actually uses (a `Guard(string what, Action action)` wrapper around a view-model command)
        /// without pretending to be a dataflow analysis. Two conditions, both checked in the
        /// callee's body: the parameter is passed to a logger, and it is used nowhere else - one
        /// appearance in the whole body means there is nothing else it could reach.
        /// </summary>
        private string PassedToLogOnly(ExpressionSyntax expr)
        {
            foreach (var argument in expr.Ancestors().OfType<ArgumentSyntax>())
            {
                var call = argument.Parent?.Parent as InvocationExpressionSyntax;
                if (call == null) continue;

                var declaration = LocalMethodOf(call);
                if (declaration == null) continue;

                var index = call.ArgumentList.Arguments.IndexOf(argument);
                if (index < 0 || index >= declaration.ParameterList.Parameters.Count) continue;
                var parameter = declaration.ParameterList.Parameters[index].Identifier.ValueText;

                var uses = declaration.DescendantNodes().OfType<IdentifierNameSyntax>()
                    .Where(i => i.Identifier.ValueText == parameter)
                    .ToList();
                if (uses.Count == 0) continue;

                var anywhereElse = uses.Any(u => !(u.FirstAncestorOrSelf<InvocationExpressionSyntax>() is InvocationExpressionSyntax inner
                                                  && Logger.IsLoggerCall(Calls.Name(inner))));
                var toALogger = uses.Any(u => u.FirstAncestorOrSelf<InvocationExpressionSyntax>() is InvocationExpressionSyntax sink
                                             && Logger.IsLoggerCall(Calls.Name(sink)));
                if (toALogger && !anywhereElse) return "its parameter only reaches a log line ("
                    + declaration.Identifier.ValueText + "), so no player reads it";
            }
            return null;
        }

        /// <summary>The method a call names, when it is declared in this file.</summary>
        private MethodDeclarationSyntax LocalMethodOf(InvocationExpressionSyntax call)
        {
            var name = call.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => null,
            };
            if (name == null) return null;

            return _tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == name
                                     && m.ParameterList.Parameters.Count == call.ArgumentList.Arguments.Count);
        }

        /// <summary>The expression has to *be* the whole value - an argument, an assignment's right side, an
        /// initialiser, a return. A literal buried inside something bigger is that bigger thing's
        /// to describe, and rewriting it would leave a half-converted sentence behind.
        /// </summary>
        internal static bool IsWholeValue(ExpressionSyntax expr) => IsInConvertiblePosition(expr);

        private static bool IsInConvertiblePosition(ExpressionSyntax expr)
        {
            switch (expr.Parent)
            {
                case ArgumentSyntax _:
                case EqualsValueClauseSyntax _:
                case ReturnStatementSyntax _:
                case ArrowExpressionClauseSyntax _:
                    return true;
                case AssignmentExpressionSyntax assignment:
                    return assignment.Right == expr;
                case InitializerExpressionSyntax initializer:
                    // An array or collection element counts when the element *is* the string,
                    // which is what rules out a dictionary entry: there the key sits beside the
                    // value and is an id, not a sentence.
                    return initializer.Expressions.Contains(expr)
                        && initializer.Expressions.All(e => e is LiteralExpressionSyntax
                            || e is BinaryExpressionSyntax
                            || e is ConditionalExpressionSyntax
                            || e is InvocationExpressionSyntax);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the sink wants a <c>TextObject</c> or a string. Only <c>new TextObject(...)</c>
        /// and the notification types do; everything else - a view-model property, an
        /// <c>InquiryData</c> field - takes a string, and the resolution has already happened by
        /// then.
        /// </summary>
        private static bool NeedsTextObject(ExpressionSyntax expr)
        {
            foreach (var creation in expr.Ancestors().OfType<ObjectCreationExpressionSyntax>())
                if (creation.Type.ToString() == "TextObject") return true;

            foreach (var call in expr.Ancestors().OfType<InvocationExpressionSyntax>())
            {
                var callee = Calls.Name(call) ?? "";
                if (Sink.NeedsTextObject(callee)) return true;
            }
            return false;
        }

        // ----- the English template and its variables -----------------------------

        /// <summary>
        /// A conditional inside a chain, when both of its branches are text: the one place where
        /// a sentence has to become two keys.
        /// </summary>
        internal sealed class TextChoice
        {
            internal ExpressionSyntax Condition;
            internal ExpressionSyntax WhenTrue;
            internal ExpressionSyntax WhenFalse;
        }

        private static TextChoice FindTextChoice(ExpressionSyntax part)
        {
            if (!(part is ConditionalExpressionSyntax conditional)) return null;
            if (conditional.DescendantNodes().OfType<ConditionalExpressionSyntax>().Any()) return null;
            if (!IsTextBranch(conditional.WhenTrue) || !IsTextBranch(conditional.WhenFalse)) return null;
            if (LiteralOf(conditional.WhenTrue) != null && LiteralOf(conditional.WhenFalse) != null
                && IsEmpty(conditional.WhenTrue) && IsEmpty(conditional.WhenFalse)) return null;

            return new TextChoice
            {
                Condition = conditional.Condition,
                WhenTrue = IsEmpty(conditional.WhenTrue) ? null : conditional.WhenTrue,
                WhenFalse = IsEmpty(conditional.WhenFalse) ? null : conditional.WhenFalse,
            };
        }

        /// <summary>
        /// A branch that is words. Punctuation is not words: <c>(x >= 0f ? "+" : "-")</c> is a
        /// sign, and giving a language two keys to translate would put a decision about arithmetic
        /// in a translation file. A branch needs two letters to count.
        /// </summary>
        private static bool IsTextBranch(ExpressionSyntax expr)
        {
            if (expr is ConditionalExpressionSyntax) return false;
            if (IsEmpty(expr)) return true;
            return expr.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>()
                .Any(l => l.IsKind(SyntaxKind.StringLiteralExpression) && Regex.IsMatch(l.Token.ValueText, "[A-Za-z]{2}"));
        }

        /// <summary>
        /// Flattens a <c>+</c> chain into the English template and the named variables behind it.
        /// The literals go in exactly as written and every other part is passed whole - including
        /// its <c>.ToString("0.0")</c> - so the English reads character for character what the
        /// concatenation produced before the key was added (AC1). The <c>{"0.0"}</c> stays in the
        /// variable, never in the template: a translated sentence must not be able to change how a
        /// number is formatted (R5).
        ///
        /// A conditional that chooses between two texts contributes one of its branches, chosen by
        /// <paramref name="takeTrue"/>. With <paramref name="choice"/> null it contributes
        /// nothing, which is what the report wants: one row for a conditional, not one per branch.
        /// </summary>
        public static string Template(ExpressionSyntax expr, TextChoice choice, bool takeTrue, out List<(string Name, string Code)> vars)
        {
            vars = new List<(string, string)>();
            var text = new StringBuilder();
            var used = new HashSet<string>(StringComparer.Ordinal);

            foreach (var part in Flatten(expr))
            {
                var found = FindTextChoice(part);
                if (found != null)
                {
                    var branch = choice == null ? null : (takeTrue ? found.WhenTrue : found.WhenFalse);
                    if (branch == null) continue;
                    var inner = Template(branch, null, true, out var innerVars);
                    foreach (var v in innerVars)
                    {
                        var name = UniqueName(v.Name, used);
                        vars.Add((name, v.Code));
                        inner = inner.Replace("{" + v.Name + "}", "{" + name + "}");
                    }
                    text.Append(inner);
                    continue;
                }

                var literal = LiteralOf(part);
                if (literal != null) { text.Append(literal); continue; }
                if (IsNewline(part)) { text.Append('\n'); continue; }
                if (IsEmpty(part)) continue;

                var variable = UniqueName(VarName(part), used);
                vars.Add((variable, part.ToString()));
                text.Append('{').Append(variable).Append('}');
            }
            return text.ToString();
        }

        private static IEnumerable<ExpressionSyntax> Flatten(ExpressionSyntax expr)
        {
            // Parentheses are how C# writes precedence, not part of the sentence:
            // "a " + (x ? "b" : "c") is one string with a choice in it, not a string plus a
            // parenthesised value. Looking through them is what lets a conditional keep its place.
            while (expr is ParenthesizedExpressionSyntax parens) expr = parens.Expression;

            if (expr is BinaryExpressionSyntax bin && bin.IsKind(SyntaxKind.AddExpression))
            {
                foreach (var p in Flatten(bin.Left)) yield return p;
                foreach (var p in Flatten(bin.Right)) yield return p;
                yield break;
            }
            yield return expr;
        }

        private static bool IsNewline(ExpressionSyntax expr) =>
            expr.ToString().Replace(" ", "") == "Environment.NewLine";

        private static bool IsEmpty(ExpressionSyntax expr)
        {
            var literal = LiteralOf(expr);
            if (literal != null && literal.Length == 0) return true;
            return expr.ToString() == "string.Empty";
        }

        /// <summary>
        /// A variable name from the expression it stands for, so a translator sees which figure the
        /// slot holds rather than <c>{VALUE}</c>.
        ///
        /// A call is named after what it computes - <c>Hegemony.HoldOf(link).ToString("0")</c> is
        /// <c>HOLD_OF</c>, <c>TermLeft(link)</c> is <c>TERM_LEFT</c> - because the receiver of a
        /// static call is the type, not the thing being described. Everything else is named after
        /// the last member or local it reads: <c>war.DaysElapsed</c> is <c>DAYS_ELAPSED</c>.
        /// </summary>
        private static string VarName(ExpressionSyntax expr)
        {
            while (expr is ParenthesizedExpressionSyntax parens) expr = parens.Expression;

            string identifier = null;
            if (expr is InvocationExpressionSyntax call)
            {
                // The value, not its formatting: hold.ToString("0") is HOLD, not TO_STRING.
                if (call.Expression is MemberAccessExpressionSyntax toString
                    && toString.Name.Identifier.ValueText == "ToString")
                    return VarName(toString.Expression);

                identifier = call.Expression is MemberAccessExpressionSyntax member
                    ? member.Name.Identifier.ValueText
                    : (call.Expression as IdentifierNameSyntax)?.Identifier.ValueText;
            }
            else
            {
                identifier = expr.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                    .Select(i => i.Identifier.ValueText).LastOrDefault()
                    ?? expr.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()
                        .Select(m => m.Name.Identifier.ValueText).LastOrDefault();
            }

            if (string.IsNullOrEmpty(identifier)) identifier = expr is ConditionalExpressionSyntax ? "CHOICE" : "VALUE";

            var sb = new StringBuilder();
            foreach (var ch in identifier.ToUpperInvariant())
                sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            var name = sb.ToString().Trim('_');
            while (name.Contains("__")) name = name.Replace("__", "_");
            if (name.Length == 0) name = "VALUE";
            if (char.IsDigit(name[0])) name = "V_" + name;
            return name;
        }

        private static string UniqueName(string name, HashSet<string> used)
        {
            var candidate = name;
            var n = 2;
            while (!used.Add(candidate)) candidate = name + "_" + n++;
            return candidate;
        }

        // ----- syntax helpers ----------------------------------------------------

        /// <summary>
        /// Builds the call the way the file it lands in already writes them: one line when it fits
        /// the project's width, one argument per line under it when it does not. The alternative -
        /// a single 300-character line - is what a mechanical rewrite produces, and it makes the
        /// next balance pass a review of unreadable text.
        /// </summary>
        internal InvocationExpressionSyntax Build(string method, string key, string template,
            List<(string Name, string Code)> vars, SyntaxNode context)
        {
            var args = new List<ArgumentSyntax>
            {
                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(key))),
                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(template))),
            };
            foreach (var v in vars)
            {
                var tuple = SyntaxFactory.TupleExpression(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(v.Name))),
                    SyntaxFactory.Argument(SyntaxFactory.ParseExpression(v.Code)),
                }, new[]
                {
                    SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space)),
                }));
                args.Add(SyntaxFactory.Argument(tuple));
            }

            var oneLine = args.Count <= 3 && Width(args, context) <= 110;
            var separators = new List<SyntaxToken>();
            for (var i = 0; i < args.Count - 1; i++)
            {
                separators.Add(oneLine
                    ? Space
                    : SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(
                        SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed,
                            SyntaxFactory.Whitespace(Indent(context) + "    "))));
            }

            // No trailing newline on the closing paren: whatever followed the expression before -
            // a semicolon, a colon of a ternary, a comma - follows it now, on the same line.
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("DiText"),
                    SyntaxFactory.IdentifierName(method)),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
                    args.Cast<ArgumentSyntax>().ToList(), separators)));
        }

        private static SyntaxToken Space => SyntaxFactory.Token(SyntaxKind.CommaToken)
            .WithTrailingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Space));

        private int Width(List<ArgumentSyntax> args, SyntaxNode context)
        {
            var length = 10 + "DiText.T(".Length;                       // the call and the assignment
            foreach (var arg in args) length += arg.ToString().Length + 2;
            return length + Indent(context).Length;
        }

        /// <summary>
        /// The indentation of the line the expression starts on, read from the file rather than
        /// from the node's own trivia: the indentation of a statement is trailing trivia of the
        /// token before it, not leading trivia of its own, so the trivia route returns nothing.
        /// </summary>
        internal string Indent(SyntaxNode node)
        {
            if (node == null || _lines == null) return "";
            var line = _tree.GetLineSpan(node.Span).StartLinePosition.Line;
            if (line < 0 || line >= _lines.Length) return "";

            var text = _lines[line].ToString();
            var indent = new string(text.TakeWhile(c => c == ' ' || c == '\t').ToArray());
            if (indent.Length > 0) return indent;

            // A continuation line can start at column 0 - a wrapped argument, or a call opened at
            // the end of a long line. The indentation the project writes for that block is the
            // nearest enclosing one, so step back until a line has some. Four spaces is the
            // project's unit; a shorter run means the line is not an indented statement.
            for (var up = line - 1; up >= 0 && line - up < 40; up--)
            {
                var candidate = new string(_lines[up].ToString().TakeWhile(c => c == ' ' || c == '\t').ToArray());
                if (candidate.Length >= 4) return candidate;
            }
            return "    ";
        }

        internal static LiteralExpressionSyntax FirstLiteral(ExpressionSyntax expr)
        {
            if (expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression)) return lit;
            return expr.DescendantNodes().OfType<LiteralExpressionSyntax>()
                .FirstOrDefault(l => l.IsKind(SyntaxKind.StringLiteralExpression));
        }

        internal static string LiteralOf(ExpressionSyntax expr) =>
            expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression)
                ? lit.Token.ValueText
                : null;

        /// <summary>Every expression that builds a string: a literal, or a <c>+</c> chain holding one.</summary>
        internal static IEnumerable<ExpressionSyntax> StringExpressions(SyntaxNode root)
        {
            var claimed = new HashSet<ExpressionSyntax>();
            foreach (var node in root.DescendantNodes())
            {
                if (claimed.Contains(node)) continue;

                if (node is BinaryExpressionSyntax bin && bin.IsKind(SyntaxKind.AddExpression) && HoldsString(bin))
                {
                    // The whole chain is one sentence, so its inner parts are claimed with it.
                    // Without that, "a (" + n + ")" would be reported as three candidates and the
                    // rewriter would replace the inner chain first, leaving the outer one holding
                    // a DiText call it cannot describe.
                    claimed.Add(bin);
                    foreach (var inner in bin.DescendantNodes().OfType<ExpressionSyntax>()) claimed.Add(inner);
                    yield return bin;
                    continue;
                }
                if (node is InterpolatedStringExpressionSyntax interp)
                {
                    claimed.Add(interp);
                    yield return interp;
                    continue;
                }
                if (node is ConditionalExpressionSyntax cond && HoldsString(cond))
                {
                    // cond ? "a" : "b" is one value with two wordings. Yielded as a whole so the
                    // branches are not reported as two unrelated sentences.
                    claimed.Add(cond);
                    foreach (var inner in cond.DescendantNodes().OfType<ExpressionSyntax>()) claimed.Add(inner);
                    yield return cond;
                    continue;
                }
                if (node is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    claimed.Add(lit);
                    yield return lit;
                }
            }
        }

        private static bool HoldsString(SyntaxNode node) =>
            node.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>().Any(l => l.IsKind(SyntaxKind.StringLiteralExpression))
            || node.DescendantNodesAndSelf().OfType<InterpolatedStringExpressionSyntax>().Any();

        private static HashSet<string> CaseLabelValues(SyntaxNode root)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var label in root.DescendantNodes().OfType<CaseSwitchLabelSyntax>())
            {
                var value = LiteralOf(label.Value);
                if (value != null) set.Add(value);
            }
            return set;
        }
    }

    /// <summary>
    /// The log side of the boundary rule. Log lines, telemetry and <c>diplomacy.*</c> output stay
    /// English by decision (story 4.1 §3 Out): they are read by the lead and by
    /// <c>analyse-log.py</c>, and a translated log cannot be grepped.
    /// </summary>
    internal static class Logger
    {
        private static readonly string[] Types =
        {
            "Log", "MBDebug", "Debug", "Telemetry", "DebugCommands", "StatecraftCommands", "WriteReport",
        };

        private static readonly HashSet<string> VerboseCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "Notify", "Error", "Warn", "Info", "Debug", "Trace", "Write",
        };

        public static bool IsLoggerCall(string callee)
        {
            if (string.IsNullOrEmpty(callee)) return false;
            var parts = callee.Split('.');
            var bare = parts[parts.Length - 1];
            if (!VerboseCalls.Contains(bare)) return false;
            return parts.Any(p => Array.IndexOf(Types, p) >= 0);
        }
    }

    /// <summary>Calls whose string argument is a shape, not a sentence.</summary>
    internal static class FormatCalls
    {
        private static readonly HashSet<string> Formats = new HashSet<string>(StringComparer.Ordinal)
        {
            "ToString", "Format", "Parse", "TryParse", "AppendFormat", "Concat", "Join",
        };

        private static readonly HashSet<string> Identity = new HashSet<string>(StringComparer.Ordinal)
        {
            "Equals", "Contains", "StartsWith", "EndsWith", "IndexOf", "LastIndexOf", "Replace",
            "GetValueOrDefault", "Compare", "CompareTo",
        };

        public static bool IsFormatCall(string callee)
        {
            var bare = Bare(callee);
            // string.Concat and string.Join build text too, but they are how *our* sentences are
            // assembled in places, so they are judged by the argument instead of skipped.
            if (bare == "Concat" || bare == "Join") return false;
            return Formats.Contains(bare);
        }

        public static bool IsIdentityCall(string callee) => Identity.Contains(Bare(callee));

        private static string Bare(string callee)
        {
            if (string.IsNullOrEmpty(callee)) return "";
            var parts = callee.Split('.');
            return parts[parts.Length - 1];
        }
    }

    /// <summary>
    /// Calls whose string argument is a name the engine resolves, not words: a prefab, a layer,
    /// a vanilla game-text variable. <c>GameTexts.SetVariable("STR", n)</c> is the sharpest case -
    /// "STR" is the placeholder inside the game's own localized string, and ours would shadow it.
    /// </summary>
    internal static class Wiring
    {
        private static readonly HashSet<string> Calls = new HashSet<string>(StringComparer.Ordinal)
        {
            "LoadXml", "CreateLayer", "LoadPrefab", "AddLayer", "SetDefaultSelectedItem",
            "SetVariable", "GetGameText", "SetGameText", "AddAttribute", "RemoveAttribute",
            "Inject", "InjectAt",
            // Reflection by name: `GetMethod("SetDefaultSelectedItem")` looks up a private vanilla
            // method, and a key that resolved to something else would hand back null and take the
            // screen down on the next call.
            "GetMethod", "GetProperty", "GetField", "GetConstructor", "Invoke", "CreateInstance",
            // A movie is a prefab: `LoadMovie("DiPeaceTable", vm)` names an asset, and a wrong name
            // loads nothing at all - a blank layer.
            "LoadMovie",
        };

        public static bool IsWiringCall(string callee)
        {
            if (string.IsNullOrEmpty(callee)) return false;
            var parts = callee.Split('.');
            return Calls.Contains(parts[parts.Length - 1]);
        }

        /// <summary>
        /// Types whose name is an asset: a layer built around a prefab takes the prefab's name, and
        /// a name that resolves through localization is a name the game cannot find.
        /// </summary>
        public static bool IsAssetType(ExpressionSyntax expr)
        {
            return expr.Ancestors().OfType<ObjectCreationExpressionSyntax>()
                .Any(o => o.Type.ToString() == "GauntletLayer"
                          || o.Type.ToString().EndsWith("Layer", StringComparison.Ordinal));
        }
    }

    /// <summary>Which of the engine's calls put their string argument on a screen.</summary>
    internal static class Sink
    {
        private static readonly string[] Display =
        {
            "Log.Notify",
            "InformationManager.ShowNotification",
            "InformationManager.DisplayMessage",
            "InformationManager.ShowInquiry",
            "InformationManager.ShowMultiSelectionInquiry",
            "MBInformationManager.ShowMultiSelectionInquiry",
            "Campaign.ShowNotification",
        };

        public static bool IsDisplayCall(string callee)
        {
            if (string.IsNullOrEmpty(callee)) return false;
            return Display.Any(d => callee.EndsWith(d, StringComparison.Ordinal) || callee.Contains(d));
        }

        /// <summary>
        /// The engine's types that want a <c>TextObject</c>. Everything else - a view-model
        /// property, every <c>InquiryData</c> field - takes a string, because
        /// <c>DiText.T</c> has already resolved it.
        /// </summary>
        private static readonly string[] TextObjectCalls =
        {
            "ShowNotification", "CreateNotification", "ShowMultiLineNotification",
        };

        public static bool NeedsTextObject(string callee)
        {
            if (string.IsNullOrEmpty(callee)) return false;
            return TextObjectCalls.Any(d => callee.Contains(d));
        }
    }

    /// <summary>
    /// The methods whose return value a player reads, wherever they live.
    ///
    /// Most player-facing text sits under <c>UI/</c>, but not all of it: a band's name, a hold
    /// term's chip, the meaning of a peace term - those belong to the system that computes them
    /// (ExhaustionBands, Hegemony, WarScore, Summons) and are drawn by a view model that already
    /// trusts them. A value like that reaches no sink of its own, so the only honest signal is a
    /// reviewed list, and it is written out in <c>tools/Localize/text-producers.txt</c> rather
    /// than guessed from method names.
    ///
    /// The name heuristic that came first swept in AI reasoning strings that only ever reach the
    /// log - <c>Missions.ApplyEffect</c>'s explanation, the <c>out why</c> of
    /// <c>AiEspionage.BestInBand</c> - and a wrong conversion is a wrong English sentence on a
    /// player's screen. The list was built by asking the other way round: every
    /// <c>Type.Method</c> call made from <c>UI/</c> whose return type is <c>string</c> or
    /// <c>TextObject</c>.
    ///
    /// A text producer missing from the list is not lost: it appears in the report as "not a
    /// screen", which is the AC3 worklist (story 4.1 §5).
    /// </summary>
    internal static class TextProducers
    {
        private static HashSet<string> _keys;

        public static void Reset() => _keys = null;

        public static bool IsTextProducer(string file, MethodDeclarationSyntax method)
        {
            if (method == null) return false;
            var name = method.Identifier.ValueText;
            if (name == "ToString") return false;
            if (_keys == null) _keys = Load();
            var relative = Paths.Relative(Exceptions.RepoRoot, file).Replace('\\', '/');
            return _keys.Contains(relative + "|" + name);
        }

        /// <summary>
        /// Whether a call inside an expression is to a method this list says produces text, and the
        /// method's name if so.
        ///
        /// A value is allowed to travel as a <c>{VARIABLE}</c> - R5 is about numbers, so a
        /// translation can never change how one is formatted. A *word* is not a value: passing one
        /// as a variable asks a language to supply English words inside its own sentence, which is
        /// the same hole as <c>{VERB}</c>, one level further out. "Pay {PRICE} - they come over to
        /// {SIDE}" with <c>SideChange.SideName</c> behind the variable is the shape (found
        /// 2026-10-04). The fix is not here: the producer itself has to return a keyed string, and
        /// until it does the site is written by hand.
        /// </summary>
        public static string WordProducerIn(ExpressionSyntax expr)
        {
            foreach (var call in expr.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            {
                var name = Calls.Name(call);
                if (string.IsNullOrEmpty(name)) continue;
                var shortName = name.Substring(name.LastIndexOf('.') + 1);
                if (shortName == "ToString" || shortName == "Format") continue;

                // The list is consulted by method name alone, because the declaring file is not
                // knowable from the call site. It is a short list of deliberately distinctive
                // names, and a false positive only moves a site from "the tool can do it" to "a
                // human decides", which is the safe direction.
                if (_keys == null) _keys = Load();
                foreach (var key in _keys)
                {
                    var at = key.IndexOf('|');
                    if (at < 0) continue;
                    if (key.Substring(at + 1) != shortName) continue;
                    return shortName;
                }
            }
            return null;
        }

        private static HashSet<string> Load()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var path = Path.Combine(Exceptions.FindRepoRoot(), "tools", "Localize", "text-producers.txt");
            if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "text-producers.txt");
            if (!File.Exists(path)) return set;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var parts = line.Split(new[] { '|' }, 3);
                if (parts.Length < 3) continue;
                var file = parts[0].Trim().Replace('\\', '/');
                var at = file.IndexOf("DiplomacyIntrigue/", StringComparison.Ordinal);
                if (at >= 0) file = file.Substring(at);
                set.Add(file + "|" + parts[1].Trim());
            }
            return set;
        }
    }

    /// <summary>
    /// Judgements the shapes cannot make, written down in a file rather than guessed in code.
    ///
    /// One line per case: the file, the method (or <c>*</c>), and the reason, which is written
    /// into the inventory so the report says why. This is where a diagnostic dump that lives in a
    /// view model gets recorded as English - the reader is the lead at a console, not a player.
    /// </summary>
    internal static class Exceptions
    {
        private static List<(string File, string Method, string Reason)> _rules;

        /// <summary>
        /// The judgement <c>exceptions.txt</c> holds for this expression, or null when it holds none.
        ///
        /// Two ways to name one expression, and the choice matters. <c>Name:123</c> is a line
        /// number: precise, but our own rewriter inserts lines above the site, so on 2026-10-04 six
        /// rules written that way stopped firing the moment they were used, and the sites went back
        /// into the convert list with nothing to say why. <c>Name#English</c> anchors on the wording
        /// the tool computed instead, which no edit to the file above it can move - and if the
        /// wording does change, the rule stops matching and the site reappears in the report for a
        /// person, which is the direction that costs nothing. Prefer it; use <c>Name:123</c> only
        /// where there is no stable wording to name.
        ///
        /// Either way the rule is a judgement a person wrote, not something the shapes decided, and
        /// it must never make a site invisible: a rule that stops matching turns the site back into
        /// an ordinary convert candidate, which the dry run prints.
        /// </summary>
        /// <summary>
        /// A sentence with its line breaks written as <c>&lt;br&gt;</c>, so a rule can name it on one
        /// line. A rules file has one rule per line and a sentence does not always fit on one: the
        /// submission offer is two paragraphs. <c>&lt;br&gt;</c> rather than <c>{NL}</c> because a
        /// brace is what a variable looks like in this file's other column, and a rule should not be
        /// able to be mistaken for a placeholder.
        /// </summary>
        private static string FoldBreaks(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            if (english.IndexOf('\n') < 0 && english.IndexOf('\r') < 0) return english;
            return english.Replace("\r\n", "<br>").Replace("\r", "<br>").Replace("\n", "<br>");
        }

        public static string IsExempt(string file, MethodDeclarationSyntax method, int line, string english)
        {
            foreach (var rule in Rules(file))
            {
                if (rule.Method != "*" && rule.Method != "*:*")
                {
                    var hash = rule.Method.LastIndexOf('#');
                    if (hash > 0)
                    {
                        if (method == null || method.Identifier.ValueText != rule.Method.Substring(0, hash)) continue;
                        if (!string.Equals(rule.Method.Substring(hash + 1), english, StringComparison.Ordinal))
                        if (!string.Equals(rule.Method.Substring(hash + 1), FoldBreaks(english), StringComparison.Ordinal)) continue;
                    }
                    else
                    {
                        var at = rule.Method.LastIndexOf(':');
                        if (at > 0 && int.TryParse(rule.Method.Substring(at + 1), out var wanted))
                        {
                            if (method == null || method.Identifier.ValueText != rule.Method.Substring(0, at)) continue;
                            if (line != wanted) continue;
                        }
                        else if (method == null || method.Identifier.ValueText != rule.Method)
                        {
                            continue;
                        }
                    }
                }
                return "exempt by tools/Localize/exceptions.txt: " + rule.Reason;
            }
            return null;
        }

        private static IEnumerable<(string File, string Method, string Reason)> Rules(string file)
        {
            if (_rules == null) _rules = Load();
            var relative = Paths.Relative(RepoRoot, file).Replace('\\', '/');
            var tail = relative.Substring(relative.IndexOf("/src/", StringComparison.Ordinal) + 1);
            return _rules.Where(r => tail.EndsWith(r.File, StringComparison.Ordinal));
        }

        internal static string RepoRoot;

        public static void SetRepo(string repo)
        {
            RepoRoot = repo;
            _rules = null;
            TextProducers.Reset();
        }

        private static List<(string, string, string)> Load()
        {
            var list = new List<(string, string, string)>();
// The repo copy first: the one beside the binary goes stale the moment the file is
            // edited, and a stale rules file means a stale verdict.
            var path = Path.Combine(FindRepoRoot(), "tools", "Localize", "exceptions.txt");
            if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "exceptions.txt");
            if (!File.Exists(path)) return list;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var parts = line.Split(new[] { '|' }, 3);
                if (parts.Length < 3) continue;
                list.Add((parts[0].Trim(), parts[1].Trim(), parts[2].Trim()));
            }
            return list;
        }

        internal static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "DiplomacyIntrigue.sln"))) return dir.FullName;
                dir = dir.Parent;
            }
            return ".";
        }
    }

    /// <summary>How a call is named, so a rule can say <c>Log.Notify</c> and not a local <c>Show</c>.</summary>
    internal static class Calls
    {
        /// <summary>
        /// Whether a named call is <c>DiText.T</c> or <c>DiText.O</c>, however it was written.
        ///
        /// The name comes back fully qualified from a member access, so a file that says
        /// <c>Core.DiText.T</c> - which <c>CasusBelli.cs</c> does, the way it already writes
        /// <c>Core.ModState</c> - reads as "Core.DiText.T" and matched nothing. That is not a
        /// cosmetic miss: <c>emit</c> left the key out of the English file and the check's
        /// idempotence guard would not have recognised the call as already keyed, so the next
        /// <c>rewrite</c> would have keyed it a second time. Two readers of the same call must
        /// agree, so the test is on the tail of the name, not on a using directive.
        /// </summary>
        public static bool IsDiText(string name) =>
            name != null
            && (name.EndsWith("DiText.T", StringComparison.Ordinal)
                || name.EndsWith("DiText.O", StringComparison.Ordinal));

        public static bool IsDiText(InvocationExpressionSyntax call) => IsDiText(Name(call));

        public static string Name(InvocationExpressionSyntax call)
        {
            switch (call.Expression)
            {
                case MemberAccessExpressionSyntax ma:
                    return ma.Expression.ToString() + "." + ma.Name.Identifier.ValueText;
                case IdentifierNameSyntax id:
                    return id.Identifier.ValueText;
                default:
                    return null;
            }
        }
    }

    /// <summary>Key naming, and the one place a key is minted.</summary>
    internal static class Keys
    {
        private static readonly Dictionary<string, string> ByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ByText = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Forget the run's keys. A report and a rewrite must see the same numbers.</summary>
        public static void Reset() { ByKey.Clear(); ByText.Clear(); }

        public static string TemplateOf(string key) => ByKey.TryGetValue(key, out var t) ? t : null;

        public static bool WasMinted(string template) => ByText.ContainsKey(template);

        public static void Mint(string key, string template)
        {
            ByKey[key] = template;
            if (!ByText.ContainsKey(template)) ByText[template] = key;
        }
        /// <summary>
        /// <c>DI_&lt;area&gt;_&lt;slug&gt;</c>, from the words of the English itself so the strings
        /// file reads as a list of sentences and two sites that say the same thing share one key.
        ///
        /// The variables join the name when there are any. That is not decoration: the same six
        /// words appear twice on the Realm tab - "Our wars (3)" as a menu row and "Our wars" as a
        /// screen title - and one key for both would force a language to translate two different
        /// sentences as one. <c>DI_MENU_OUR_WARS_COUNT</c> and <c>DI_MENU_OUR_WARS</c> say which is
        /// which before a translator opens the file.
        ///
        /// The slug is only a seed: once a key is in the code and in the strings file it is
        /// frozen, because a wording fix keeps its key and only a change of meaning takes a new one
        /// (story 4.1 R2).
        /// </summary>
        public static string For(string area, string template, string[] variables)
        {
            var seed = Regex.Replace(template, @"\{[A-Z0-9_]+\}", " ");
            var words = Regex.Matches(seed.ToLowerInvariant(), "[a-z0-9]+")
                .Cast<Match>().Select(m => m.Value)
                .Where(w => w.Length > 1)
                .ToList();
            var slug = string.Join("_", words.Take(6));

            var key = "DI_" + area;
            if (slug.Length > 0) key += "_" + slug;

            // The variables go in when they fit. Past a length they are cut down rather than
            // dropped: a key has to stay readable in a translation file, and DI_MENU_OUR_WARS_COUNT
            // says more than DI_MENU_OUR_WARS_COUNT_A_COUNT_B_COUNT would.
            const int Limit = 72;
            var names = (variables ?? Array.Empty<string>()).Select(v => v.ToUpperInvariant()).ToList();
            while (names.Count > 0 && Sanitize(key + "_" + string.Join("_", names)).Length > Limit)
                names.RemoveAt(names.Count - 1);
            if (names.Count > 0) key += "_" + string.Join("_", names);
            else if (Sanitize(key).Length > Limit) key = Sanitize(key).Substring(0, Limit).TrimEnd('_');

            return Sanitize(key);
        }

        public static string Sanitize(string key)
        {
            var sb = new StringBuilder(key.Length);
            foreach (var ch in key)
                sb.Append(char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_');
            var s = sb.ToString().Trim('_');
            while (s.Contains("__")) s = s.Replace("__", "_");
            return s;
        }
    }

    internal static class Paths
    {
        public static string AreaOf(string file, string repo)
        {
            var r = Relative(repo, file).Replace('\\', '/');
            if (r.EndsWith("ModSettings.cs", StringComparison.Ordinal)) return "MCM";
            if (r.EndsWith("SubModule.cs", StringComparison.Ordinal)) return "STARTUP";
            if (r.EndsWith("RealmVM.cs", StringComparison.Ordinal)) return "REALM";
            if (r.EndsWith("CourtVM.cs", StringComparison.Ordinal)) return "COURT";
            if (r.EndsWith("CivilWarVM.cs", StringComparison.Ordinal)) return "CIVILWAR";
            if (r.EndsWith("CounterIntelVM.cs", StringComparison.Ordinal)) return "COUNTERINTEL";
            if (r.EndsWith("StatecraftVM.cs", StringComparison.Ordinal)) return "STATECRAFT";
            if (r.EndsWith("DiplomacyItemMixin.cs", StringComparison.Ordinal)) return "DIPLOMACY";
            if (r.EndsWith("DiplomacyMenu.cs", StringComparison.Ordinal)) return "MENU";
            if (r.EndsWith("IntelligenceVM.cs", StringComparison.Ordinal)) return "INTEL";
            if (r.EndsWith("EncyclopediaCourtVM.cs", StringComparison.Ordinal)) return "ENCYCLOPEDIA";
            if (r.Contains("/PeaceTable/")) return "PEACE";
            if (r.Contains("/KingdomScreen/")) return "KINGDOMSCREEN";
            if (r.Contains("/ClanScreen/")) return "CLANSCREEN";
            if (r.Contains("/EncyclopediaPages/")) return "ENCYCLOPEDIA";
            if (r.Contains("/GameModels/")) return "DECISIONS";
            if (r.Contains("/Diplomacy/")) return "DIPLOMACY";
            if (r.Contains("/Intrigue/")) return "INTRIGUE";
            if (r.Contains("/Espionage/")) return "ESPIONAGE";
            if (r.Contains("/Behaviors/")) return "BEHAVIORS";
            if (r.Contains("/Statecraft/")) return "STATECRAFT";
            if (r.Contains("/Patches/")) return "PATCHES";
            if (r.Contains("/Core/")) return "CORE";
            if (r.Contains("/Models/")) return "CORE";
            return "MOD";
        }

        public static string Relative(string repo, string path)
        {
            var full = Path.GetFullPath(path);
            if (!string.IsNullOrEmpty(repo) && full.StartsWith(repo, StringComparison.OrdinalIgnoreCase))
                return full.Substring(repo.Length).TrimStart('\\', '/');
            return full;
        }
    }
}
