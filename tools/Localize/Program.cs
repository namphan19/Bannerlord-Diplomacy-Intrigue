using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Localize
{
    /// <summary>
    /// Story 4.1 ST-3. Reads the module's source and keeps the English strings file a function of
    /// it, so a converted string and its entry cannot drift apart.
    ///
    ///   report    the boundary rule applied to every literal: artifacts/localization/inventory.csv
    ///   rewrite   turn the approved literals into DiText calls (dry run unless --apply)
    ///   emit      write Languages/<lang>/di_strings.xml from the DiText calls in the source
    ///   golden    key -> English, for the AC1 comparison after a balance pass
    ///   prefabs   the prefab text that cannot be keyed in place, with file and line
    ///
    /// <c>--apply</c> writes. Every other invocation reports, because ST-3's dry-run report is
    /// meant to be read by a human before 1,300 edits land: the classifier decides by shape, and
    /// the shape that fools it is a value that is really an id.
    /// </summary>
    internal static class Program
    {
        private static string _repo;
        private static string _src;
        private static string _module;
        private static string _artifacts;
        private static bool _apply;
        private static string _language = "EN";

        /// <summary>
        /// Files that are not a screen at all: the console commands' output, the telemetry lines
        /// and the log writer itself. Story 4.1 §3 Out - they stay English on purpose.
        /// </summary>
        private static readonly string[] ExcludedFiles =
        {
            "Core/DebugCommands.cs",
            "Core/StatecraftCommands.cs",
            "Core/Telemetry.cs",
            "Core/Log.cs",
            "Core/TickBudget.cs",
            "Core/DiText.cs",
        };

        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Usage();
                return 2;
            }

            var command = args[0];
            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--apply": _apply = true; break;
                    case "--language": _language = args[++i]; break;
                    case "--repo": _repo = args[++i]; break;
                    default:
                        Console.Error.WriteLine("unknown option " + args[i]);
                        return 2;
                }
            }

            _repo ??= FindRepo();
            if (_repo == null || !Directory.Exists(_repo))
            {
                Console.Error.WriteLine("Cannot find the repository root (no DiplomacyIntrigue.sln above me, and no --repo).");
                return 2;
            }
            _src = Path.Combine(_repo, "src", "DiplomacyIntrigue");
            _module = Path.Combine(_repo, "module", "DiplomacyIntrigue");
            _artifacts = Path.Combine(_repo, "artifacts", "localization");
            Directory.CreateDirectory(_artifacts);

            try
            {
                switch (command)
                {
                    case "report": return Report();
                    case "rewrite": return Rewrite();
                    case "emit": return Emit();
                    case "golden": return Golden();
                    case "prefabs": return Prefabs();
                    default:
                        Usage();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void Usage()
        {
            Console.Error.WriteLine("usage: Localize <report|rewrite|emit|golden|prefabs> [--apply] [--language XX] [--repo path]");
        }

        private static string FindRepo()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "DiplomacyIntrigue.sln"))) return dir.FullName;
                dir = dir.Parent;
            }
            return Environment.GetEnvironmentVariable("DI_REPO");
        }

        // ---------------------------------------------------------------- report

        private static int Report()
        {
            Keys.Reset();
            var candidates = new List<Candidate>();
            foreach (var file in SourceFiles())
            {
                if (IsExcluded(file)) continue;
                var tree = Parse(file);
                var classifier = new Classifier(file, tree, _repo);
                candidates.AddRange(classifier.Candidates);
            }

            var csv = new StringBuilder();
            csv.AppendLine("file,line,area,verdict,reason,parent,key,variables,english,code");
            foreach (var c in candidates.OrderBy(x => x.File, StringComparer.Ordinal).ThenBy(x => x.Line))
            {
                csv.AppendLine(string.Join(",",
                    Csv(c.File), c.Line, c.Area, c.Verdict, Csv(c.Reason), c.Parent, c.Key,
                    Csv(c.Variables), Csv(OneLine(c.English)), Csv(OneLine(c.Code))));
            }
            File.WriteAllText(Path.Combine(_artifacts, "inventory.csv"), csv.ToString());

            var convert = candidates.Count(x => x.Verdict == Candidate.VerdictConvert);
            var manual = candidates.Count(x => x.Verdict == Candidate.VerdictManual);
            var skip = candidates.Count(x => x.Verdict == Candidate.VerdictSkip);

            var md = new StringBuilder();
            md.AppendLine("# Localization inventory");
            md.AppendLine();
            md.AppendLine("`tools/Localize report`, one row per string expression in `inventory.csv`.");
            md.AppendLine();
            md.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "| verdict | what it means | count |\n|---|---|---|\n| `convert` | a player reads it; the rewriter can key it | {0} |\n| `manual` | a player reads it; a human decides (a conditional choosing between two texts, a value built in pieces) | {1} |\n| `skip` | not a screen: log, telemetry, `diplomacy.*` output, an id, a format string, an asset name | {2} |",
                convert, manual, skip));
            md.AppendLine();
            md.AppendLine("## The boundary rule");
            md.AppendLine();
            md.AppendLine("A literal is player-facing when it reaches a screen:");
            md.AppendLine();
            md.AppendLine("1. it is under `src/DiplomacyIntrigue/UI/`, which exists to be drawn; or");
            md.AppendLine("2. it reaches a sink the engine shows - `Log.Notify`, `ShowNotification`,");
            md.AppendLine("   `ShowInquiry`, `ShowMultiSelectionInquiry`, `new TextObject(...)`; or");
            md.AppendLine("3. it is an MCM setting's name, hint or group, or the module's own startup");
            md.AppendLine("   notice.");
            md.AppendLine();
            md.AppendLine("And it is **not** when it is an argument to `Log.*`, a telemetry field, a");
            md.AppendLine("`case` label or a value used as a switch id, a format string (`.ToString(\"0.0\")`),");
            md.AppendLine("a colour, a brush or sprite name, or a run of punctuation. Those stay English");
            md.AppendLine("by decision (story 4.1 §3 Out), so a translated log cannot be grepped and a");
            md.AppendLine("format string cannot be translated at all.");
            md.AppendLine();
            md.AppendLine("The classifier reads shapes, not intent: a value that is an id but looks like a");
            md.AppendLine("sentence is listed as `manual` for a human to judge. That is why `rewrite` is a");
            md.AppendLine("dry run without `--apply`.");
            md.AppendLine();
            md.AppendLine("## Per file");
            md.AppendLine();
            md.AppendLine("| file | convert | manual | skip |");
            md.AppendLine("|---|---|---|---|");
            foreach (var g in candidates.GroupBy(x => x.File).OrderByDescending(g => g.Count()))
            {
                md.AppendLine(string.Format(CultureInfo.InvariantCulture, "| `{0}` | {1} | {2} | {3} |",
                    g.Key,
                    g.Count(x => x.Verdict == Candidate.VerdictConvert),
                    g.Count(x => x.Verdict == Candidate.VerdictManual),
                    g.Count(x => x.Verdict == Candidate.VerdictSkip)));
            }
            File.WriteAllText(Path.Combine(_artifacts, "report.md"), md.ToString());

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "report: {0} convert, {1} manual, {2} skip -> artifacts/localization/report.md", convert, manual, skip));
            return 0;
        }

        // --------------------------------------------------------------- rewrite

        private static int Rewrite()
        {
            Keys.Reset();
            var files = SourceFiles().Where(f => !IsExcluded(f)).ToList();
            var totalStrings = 0;
            var changedFiles = 0;

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(text, path: file);
                var classifier = new Classifier(file, tree, _repo);
                var rewriter = new TextRewriter(classifier);
                var root = rewriter.Visit(tree.GetRoot());

                if (rewriter.Count == 0) continue;

                var newText = root.ToFullString();
                newText = EnsureUsing(root, newText, "DiplomacyIntrigue.Core");
                var relative = Paths.Relative(_repo, file);
                if (_apply)
                {
                    File.WriteAllText(file, newText);
                    changedFiles++;
                }
                else
                {
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}: {1} string(s) [{2}]", relative, rewriter.Count,
                        string.Join(", ", rewriter.Keys.Take(4))));
                }
                totalStrings += rewriter.Count;
            }

            Console.WriteLine(_apply
                ? string.Format(CultureInfo.InvariantCulture,
                    "rewrite: {0} strings in {1} files (applied)", totalStrings, changedFiles)
                : string.Format(CultureInfo.InvariantCulture,
                    "rewrite: {0} strings in {1} files would change (dry run - --apply to write)", totalStrings, changedFiles));
            return 0;
        }

        /// <summary>
        /// Adds <c>using DiplomacyIntrigue.Core;</c> to a file the rewrite gave a DiText call, in
        /// the position the rest of the file's usings imply. Only when the rewrite introduced one,
        /// so a file the tool left alone is untouched to the byte.
        /// </summary>
        private static string EnsureUsing(SyntaxNode root, string text, string namespaceName)
        {
            if (!text.Contains("DiText.", StringComparison.Ordinal)) return text;
            if (root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Any(u => u.Name?.ToString() == namespaceName)) return text;

            var directive = "using " + namespaceName + ";";
            var existing = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Where(u => u.Name != null)
                .ToList();
            if (existing.Count == 0)
                return directive + Environment.NewLine + text;

            // Alphabetical, like every other file in the project: System first, then ours.
            var after = existing.LastOrDefault(u => string.CompareOrdinal(u.Name.ToString(), namespaceName) < 0);
            var line = after != null
                ? after.GetLocation().GetLineSpan().EndLinePosition.Line
                : existing[0].GetLocation().GetLineSpan().StartLinePosition.Line - 1;

            var lines = new List<string>(text.Split('\n'));
            lines.Insert(line + 1, directive);
            return string.Join("\n", lines);
        }

        // ------------------------------------------------------------------ emit

        private sealed class KeyEntry
        {
            public string Key;
            public string English;
            public readonly List<string> Variables = new List<string>();
            public readonly List<string> Sites = new List<string>();
        }

        /// <summary>
        /// Writes the English strings file from the code, not the other way round: every
        /// <c>DiText.T("KEY", "English")</c> in the source is one entry. That direction matters
        /// because the English has to stay in the source (it is the fallback the engine shows when
        /// a language has no entry), so the file is derived from it and
        /// <c>scripts/check-localization.ps1</c> holds both sides still afterwards.
        /// </summary>
        private static int Emit()
        {
            var entries = ScanKeys(out var problems);
            var path = Path.Combine(_module, "ModuleData", "Languages", _language, "di_strings.xml");

            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xml.AppendLine("<!-- Generated by `tools/Localize emit`. The English lives in the DiText calls;");
            xml.AppendLine("     edit those, not this file, and run the tool again. -->");
            xml.AppendLine("<base xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" type=\"string\">");
            xml.AppendLine("  <tags>");
            xml.AppendLine("    <tag language=\"" + LanguageId(_language) + "\"/>");
            xml.AppendLine("  </tags>");
            xml.AppendLine("  <strings>");
            foreach (var e in entries.OrderBy(x => x.Key, StringComparer.Ordinal))
                xml.AppendLine("    <string id=\"" + Esc(e.Key) + "\" text=\"" + Esc(e.English) + "\"/>");
            xml.AppendLine("  </strings>");
            xml.AppendLine("</base>");

            var previous = ReadStringsFile(path);
            foreach (var e in entries)
            {
                if (previous.TryGetValue(e.Key, out var old) && old != e.English && problems == 0)
                {
                    Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}: the English for this key changed. A wording fix keeps its key (story 4.1 R2); only a change of meaning takes a new one.", e.Key));
                    problems++;
                }
            }

            if (_apply)
            {
                File.WriteAllText(path, xml.ToString());
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "emit: {0} strings -> module/DiplomacyIntrigue/ModuleData/Languages/{1}/di_strings.xml", entries.Count, _language));
            }
            else
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "emit: {0} strings (dry run - --apply to write)", entries.Count));
            }

            var live = new HashSet<string>(entries.Select(x => x.Key), StringComparer.Ordinal);
            foreach (var stale in previous.Keys.Where(k => !live.Contains(k)).OrderBy(x => x, StringComparer.Ordinal))
                Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}: in the strings file, used nowhere in the source", stale));

            return problems == 0 ? 0 : 1;
        }

        private static List<KeyEntry> ScanKeys(out int problems)
        {
            problems = 0;
            var byKey = new Dictionary<string, KeyEntry>(StringComparer.Ordinal);
            var order = new List<string>();

            foreach (var file in SourceFiles())
            {
                var tree = Parse(file);
                foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var callee = Calls.Name(call);
                    if (callee != "DiText.T" && callee != "DiText.O") continue;

                    var args = call.ArgumentList.Arguments;
                    if (args.Count < 2) continue;
                    var key = Classifier.LiteralOf(args[0].Expression);
                    var english = Classifier.LiteralOf(args[1].Expression);
                    if (key == null || english == null)
                    {
                        Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "{0}:{1}: a DiText call whose key or English is not a literal - the check cannot read it",
                            Paths.Relative(_repo, file), call.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
                        problems++;
                        continue;
                    }

                    if (!byKey.TryGetValue(key, out var entry))
                    {
                        entry = new KeyEntry { Key = key, English = english };
                        byKey[key] = entry;
                        order.Add(key);
                    }
                    else if (entry.English != english)
                    {
                        Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "duplicate key {0} with two different English texts:\n  {1}\n  {2}", key, entry.English, english));
                        problems++;
                    }

                    foreach (var arg in args.Skip(2))
                    {
                        if (arg.Expression is TupleExpressionSyntax tuple && tuple.Arguments.Count == 2)
                        {
                            var name = Classifier.LiteralOf(tuple.Arguments[0].Expression);
                            if (name != null && !entry.Variables.Contains(name)) entry.Variables.Add(name);
                            if (!entry.English.Contains("{" + name + "}"))
                            {
                                Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                    "key {0}: the variable {1} is passed but the English never uses it", key, name));
                                problems++;
                            }
                        }
                    }
                    var where = Paths.Relative(_repo, file) + ":"
                                + (call.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
                    if (!entry.Sites.Contains(where)) entry.Sites.Add(where);
                }
            }
            return order.Select(k => byKey[k]).ToList();
        }

        // ---------------------------------------------------------------- golden

        /// <summary>
        /// The English per key, for AC1: with the game in English, every converted surface must
        /// read exactly as it did before. Written as JSON so the next balance pass can diff it and
        /// see a wording change - which is a decision to make on purpose (R2), not to discover in
        /// a screenshot.
        /// </summary>
        private static int Golden()
        {
            var entries = ScanKeys(out _);
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"note\": \"English source text per key. Story 4.1 AC1 / R2: a wording fix keeps its key; only a change of meaning takes a new one.\",");
            json.AppendLine("  \"keys\": {");
            var ordered = entries.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                json.Append("    \"").Append(ordered[i].Key).Append("\": { \"text\": \"")
                    .Append(Json(ordered[i].English)).Append("\", \"variables\": [");
                for (var v = 0; v < ordered[i].Variables.Count; v++)
                {
                    if (v > 0) json.Append(", ");
                    json.Append('"').Append(ordered[i].Variables[v]).Append('"');
                }
                json.Append("], \"sites\": [\"").Append(string.Join("\", \"", ordered[i].Sites)).Append("\"] }");
                json.AppendLine(i == ordered.Count - 1 ? "" : ",");
            }
            json.AppendLine("  }");
            json.AppendLine("}");

            var path = Path.Combine(_artifacts, "golden-" + _language + ".json");
            if (File.Exists(path) && _apply)
                File.Copy(path, Path.Combine(_artifacts, "golden-previous.json"), true);
            if (_apply)
            {
                File.WriteAllText(path, json.ToString());
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "golden: {0} keys -> artifacts/localization/golden-{1}.json", entries.Count, _language));
            }
            else
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "golden: {0} keys (dry run)", entries.Count));
            }
            return 0;
        }

        // --------------------------------------------------------------- prefabs

        /// <summary>
        /// The prefab worklist. A widget's <c>Text</c> is a plain string: <c>TextWidget.SetText</c>
        /// hands it to <c>TwoDimension.Text.Value</c> and <c>TextParser.Parse</c> never reads a
        /// key, so a literal in prefab XML cannot be translated where it stands. Each one has to
        /// move into a view-model property returning <c>DiText.T(...)</c>, with the binding
        /// becoming <c>Text="@Property"</c>. Verified by IL, not by a session - story §9.
        ///
        /// The DataSource chain is in the report because it decides where the property goes: a
        /// widget under <c>DataSource="{DiIntelligence}"</c> reads <c>@TabText</c> off that view
        /// model, not off the screen's, and a property added to the wrong one binds to nothing and
        /// draws an empty label - which no test in English would catch.
        /// </summary>
        private static int Prefabs()
        {
            var root = Path.Combine(_module, "GUI", "Prefabs");
            var rows = new StringBuilder();
            rows.AppendLine("file,line,attribute,datasource,value,property");
            var total = 0;

            foreach (var file in Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories)
                         .OrderBy(x => x, StringComparer.Ordinal))
            {
                var document = XDocument.Parse(File.ReadAllText(file));
                var chain = new List<(int Line, string Chain, string Value)>();
                var index = 0;

                // Walked in document order, so the Nth literal in the text is the Nth attribute
                // the document reports. Asking the document where it is does not work - XDocument
                // keeps no positions - and searching for the value finds the line of a *comment*
                // that merely mentions it, which is how "What their court would sign" was reported
                // as line 3 of DiPactChooser: it is the prefab's prose, not its markup.
                foreach (var line in File.ReadLines(file))
                {
                    index++;
                    foreach (Match m in Regex.Matches(line, "(?<attr>[A-Za-z_]*Text)\\s*=\\s*\"(?<value>[^\"]*)\""))
                    {
                        if (!IsTextAttribute(m.Groups["attr"].Value)) continue;
                        var value = m.Groups["value"].Value;
                        if (value.StartsWith("@", StringComparison.Ordinal) || value.Length == 0) continue;
                        chain.Add((index, "", value));
                    }
                }

                var values = new List<string>();
                var chains = new List<string>();
                foreach (var element in document.Descendants())
                {
                    var elementChain = SourceChain(element);
                    foreach (var attribute in element.Attributes())
                    {
                        if (!IsTextAttribute(attribute.Name.LocalName)) continue;
                        var value = attribute.Value;
                        if (value.StartsWith("@", StringComparison.Ordinal) || value.Length == 0) continue;
                        values.Add(value);
                        chains.Add(elementChain);
                    }
                }

                for (var i = 0; i < values.Count; i++)
                {
                    var line = i < chain.Count ? chain[i].Line : 0;
                    total++;
                    rows.AppendLine(string.Join(",",
                        Csv(Paths.Relative(_repo, file)), line, Csv("Text"),
                        Csv(chains[i]), Csv(values[i]), Csv(PropertyNameFor(values[i]))));
                }
            }

            File.WriteAllText(Path.Combine(_artifacts, "prefabs.csv"), rows.ToString());
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "prefabs: {0} literal text attribute(s) cannot be keyed in place -> artifacts/localization/prefabs.csv", total));
            return 0;
        }

        private static readonly string[] TextAttributes =
        {
            "Text", "TooltipText", "HeaderText", "ButtonText",
        };

        private static bool IsTextAttribute(string name)
        {
            if (Array.IndexOf(TextAttributes, name) >= 0) return true;
            // Any attribute whose name ends in Text is one, which is how the checker reads it too:
            // a new "CaptionText" should not slip past by being spelled differently.
            return name.EndsWith("Text", StringComparison.Ordinal);
        }

        /// <summary>The DataSource bindings from the root down to this widget, innermost last.</summary>
        private static string SourceChain(XElement element)
        {
            var chain = new List<string>();
            for (var node = element; node != null; node = node.Parent)
            {
                var source = node.Attribute("DataSource")?.Value;
                if (!string.IsNullOrEmpty(source)) chain.Insert(0, source);
            }
            return string.Join(" > ", chain);
        }

        /// <summary>A property name a translator-facing label can carry, from the words it holds.</summary>
        private static string PropertyNameFor(string value)
        {
            var words = Regex.Matches(value, "[A-Za-z]+")
                .Cast<Match>().Select(m => m.Value)
                .Where(w => w.Length > 2)
                .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant())
                .Take(4);
            var name = string.Join("", words);
            return name.Length == 0 ? "LiteralText" : name + "Text";
        }

        private static int LineOf(string[] lines, string value, int skip)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("\"" + value + "\"", StringComparison.Ordinal)) continue;
                if (skip-- > 0) continue;
                return i + 1;
            }
            return 0;
        }

        // ----------------------------------------------------------------- shared

        private static IEnumerable<string> SourceFiles() =>
            Directory.GetFiles(_src, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/obj/"))
                .OrderBy(x => x, StringComparer.Ordinal);

        private static SyntaxTree Parse(string file) =>
            CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);

        private static bool IsExcluded(string file)
        {
            var relative = Paths.Relative(_repo, file).Replace('\\', '/');
            return ExcludedFiles.Any(x => relative.EndsWith(x, StringComparison.Ordinal));
        }

        /// <summary>
        /// The game's own id for a language, which is what the game asks for: "English",
        /// "Deutsch", "Türkçe". Read from the installed Native module rather than guessed, so a
        /// folder we add is picked up by the same selector as vanilla's.
        /// </summary>
        private static string LanguageId(string folder)
        {
            if (folder == "EN") return "English";
            foreach (var game in GameFolders())
            {
                var path = Path.Combine(game, "Modules", "Native", "ModuleData", "Languages", folder, "language_data.xml");
                if (!File.Exists(path)) continue;
                var m = Regex.Match(File.ReadAllText(path), "id=\"(?<id>[^\"]+)\"");
                if (m.Success) return m.Groups["id"].Value;
            }
            return folder;
        }

        private static IEnumerable<string> GameFolders()
        {
            var fromEnv = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
            if (!string.IsNullOrEmpty(fromEnv)) yield return fromEnv;
            foreach (var candidate in new[]
            {
                @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord",
                @"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord",
                @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord",
            })
                yield return candidate;
        }

        private static Dictionary<string, string> ReadStringsFile(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(path)) return map;
            foreach (Match m in Regex.Matches(File.ReadAllText(path),
                         "<string\\s+id=\"(?<id>[^\"]*)\"\\s+text=\"(?<text>[^\"]*)\""))
                map[Unescape(m.Groups["id"].Value)] = Unescape(m.Groups["text"].Value);
            return map;
        }

        private static string Csv(string value)
        {
            if (value == null) return "";
            return value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\""
                : value;
        }

        private static string OneLine(string value) => (value ?? "").Replace("\r", " ").Replace("\n", " ");

        private static string Json(string value)
        {
            var sb = new StringBuilder();
            foreach (var ch in value ?? "")
            {
                if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
                else if (ch == '\n') sb.Append("\\n");
                else if (ch == '\r') sb.Append("\\r");
                else if (ch == '\t') sb.Append("\\t");
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private static string Esc(string value) =>
            (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("\r", "&#13;").Replace("\n", "&#10;");

        private static string Unescape(string value) =>
            value.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"")
                .Replace("&#13;", "\r").Replace("&#10;", "\n").Replace("&amp;", "&");
    }
}
