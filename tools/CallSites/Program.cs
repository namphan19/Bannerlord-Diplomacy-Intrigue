using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CallSites
{
    /// <summary>
    /// Finds who calls a given game method, and prints the IL of any method by name.
    ///
    ///     dotnet run --project tools/CallSites -- --il "Clan::IsAtWarWith"
    ///     dotnet run --project tools/CallSites -- --callers "FactionManager::IsAtWarAgainstFaction"
    ///     dotnet run --project tools/CallSites -- --members "Clan"
    ///
    /// ApiDump answers "what is the public surface" and, with --calls, "what does this method
    /// call". This tool answers the reverse question - "who calls this, and what do they push
    /// onto the stack first" - which is the only way to find out whether the engine resolves
    /// hostility through a Clan or through its MapFaction. Phase 2's internal-politics work
    /// (option C) has to patch that resolution, and a patch aimed at the wrong method degrades
    /// to vanilla silently and looks like a design failure instead of a missed call site.
    ///
    /// Metadata only: nothing here loads or runs game code.
    /// </summary>
    internal static class Program
    {
        /// <summary>How many instructions before a call site to print. Enough to see the
        /// receiver and the arguments being pushed, which is the whole point.</summary>
        private const int ContextInstructions = 8;

        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("usage: CallSites --il|--callers|--members <Type::Method or Type>");
                return 2;
            }

            var gameFolder = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR")
                             ?? @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord";
            var gameBin = Path.Combine(gameFolder, "bin", "Win64_Shipping_Client");
            if (!Directory.Exists(gameBin))
            {
                Console.WriteLine("FATAL: game bin not found: " + gameBin);
                return 2;
            }

            var mode = args[0];
            var target = args[1];
            ParseTarget(target, out var typeFilter, out var memberFilter);

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(gameBin);
            var parameters = new ReaderParameters { AssemblyResolver = resolver };

            var modules = new List<ModuleDefinition>();
            foreach (var dll in Directory.GetFiles(gameBin, "TaleWorlds*.dll").OrderBy(p => p))
            {
                try { modules.Add(ModuleDefinition.ReadModule(dll, parameters)); }
                catch { /* native or unreadable: not every dll in bin is managed */ }
            }

            Console.WriteLine("indexed " + modules.Count + " managed assemblies from " + gameBin);
            var sb = new StringBuilder();

            switch (mode)
            {
                case "--il":
                    DumpIl(modules, typeFilter, memberFilter, sb);
                    break;
                case "--callers":
                    FindCallers(modules, typeFilter, memberFilter, sb);
                    break;
                case "--members":
                    ListMembers(modules, typeFilter, sb);
                    break;
                default:
                    Console.WriteLine("unknown mode: " + mode);
                    return 2;
            }

            var repoRoot = FindRepoRoot();
            var outDir = Path.Combine(repoRoot, "artifacts", "callsites");
            Directory.CreateDirectory(outDir);
            var safe = Sanitize(target) + mode.Replace("--", "-") + ".txt";
            var outPath = Path.Combine(outDir, safe);
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));

            Console.WriteLine(sb.ToString());
            Console.WriteLine("-> " + outPath);
            return 0;
        }

        private static void ParseTarget(string target, out string typeFilter, out string memberFilter)
        {
            var idx = target.IndexOf("::", StringComparison.Ordinal);
            if (idx < 0) { typeFilter = target; memberFilter = null; return; }
            typeFilter = target.Substring(0, idx);
            memberFilter = target.Substring(idx + 2);
        }

        private static bool TypeMatches(TypeDefinition t, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            return t.Name == filter || t.FullName == filter || t.FullName.EndsWith("." + filter, StringComparison.Ordinal);
        }

        private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<ModuleDefinition> modules)
        {
            foreach (var m in modules)
            {
                IEnumerable<TypeDefinition> types;
                try { types = m.GetTypes(); } catch { continue; }
                foreach (var t in types) yield return t;
            }
        }

        private static void ListMembers(List<ModuleDefinition> modules, string typeFilter, StringBuilder sb)
        {
            foreach (var t in AllTypes(modules).Where(t => TypeMatches(t, typeFilter)))
            {
                sb.AppendLine("// " + t.Module.Assembly.Name.Name + " :: " + t.FullName);
                foreach (var m in t.Methods.OrderBy(m => m.Name))
                {
                    var vis = m.IsPublic ? "public" : m.IsPrivate ? "private" : m.IsFamily ? "protected" : "internal";
                    var stat = m.IsStatic ? " static" : "";
                    sb.AppendLine("  " + vis + stat + " " + m.ReturnType.Name + " " + m.Name
                                  + "(" + string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
                }
                sb.AppendLine();
            }
        }

        private static void DumpIl(List<ModuleDefinition> modules, string typeFilter, string memberFilter, StringBuilder sb)
        {
            foreach (var t in AllTypes(modules).Where(t => TypeMatches(t, typeFilter)))
            {
                foreach (var m in t.Methods)
                {
                    if (memberFilter != null && m.Name != memberFilter) continue;
                    sb.AppendLine("=== " + t.FullName + "::" + m.Name
                                  + "(" + string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name)) + ")");
                    if (!m.HasBody) { sb.AppendLine("  (no body)"); sb.AppendLine(); continue; }
                    foreach (var ins in m.Body.Instructions)
                        sb.AppendLine("  " + Format(ins));
                    sb.AppendLine();
                }
            }
        }

        private static void FindCallers(List<ModuleDefinition> modules, string typeFilter, string memberFilter, StringBuilder sb)
        {
            var hits = 0;
            foreach (var t in AllTypes(modules))
            {
                foreach (var caller in t.Methods)
                {
                    if (!caller.HasBody) continue;
                    IList<Instruction> body;
                    try { body = caller.Body.Instructions; } catch { continue; }

                    for (var i = 0; i < body.Count; i++)
                    {
                        if (!(body[i].Operand is MethodReference callee)) continue;
                        if (memberFilter != null && callee.Name != memberFilter) continue;
                        if (!string.IsNullOrEmpty(typeFilter))
                        {
                            var dt = callee.DeclaringType;
                            if (dt == null) continue;
                            if (dt.Name != typeFilter && dt.FullName != typeFilter
                                && !dt.FullName.EndsWith("." + typeFilter, StringComparison.Ordinal)) continue;
                        }

                        hits++;
                        sb.AppendLine("=== " + t.FullName + "::" + caller.Name
                                      + "   [" + t.Module.Assembly.Name.Name + "]");
                        var from = Math.Max(0, i - ContextInstructions);
                        for (var j = from; j <= i; j++)
                            sb.AppendLine("  " + (j == i ? ">> " : "   ") + Format(body[j]));
                        sb.AppendLine();
                    }
                }
            }
            sb.AppendLine("// " + hits + " call site(s).");
        }

        private static string Format(Instruction ins)
        {
            var operand = ins.Operand switch
            {
                MethodReference mr => mr.DeclaringType?.Name + "::" + mr.Name,
                FieldReference fr => fr.DeclaringType?.Name + "::" + fr.Name,
                TypeReference tr => tr.Name,
                null => "",
                _ => ins.Operand.ToString()
            };
            return "IL_" + ins.Offset.ToString("x4") + "  " + ins.OpCode.Name
                   + (operand.Length > 0 ? "  " + operand : "");
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git"))) dir = dir.Parent;
            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }
    }
}
