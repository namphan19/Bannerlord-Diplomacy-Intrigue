using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace ApiDump
{
    /// <summary>
    /// Dumps the public surface of selected game types so we can write against the real
    /// v1.4.8 API instead of guessing names and compiling repeatedly. There is no public
    /// API documentation for Bannerlord, and names move between game versions.
    ///
    ///     dotnet run --project tools/ApiDump -- [type-or-namespace-filter ...]
    ///     dotnet run --project tools/ApiDump -- --from Bannerlord.UIExtenderEx BaseViewModelMixin
    ///
    /// With no arguments it dumps the set the mod currently needs. Output goes to
    /// artifacts/api/&lt;Type&gt;.txt so it can be grepped while writing code.
    ///
    /// **--from &lt;module-or-path&gt;** also indexes an assembly outside the game bin: a module
    /// name resolves to Modules/&lt;name&gt;/bin/Win64_Shipping_Client, or pass a folder or a
    /// .dll directly. Added when the Kingdom-screen UI work needed UIExtenderEx's surface,
    /// which lives in a module rather than in TaleWorlds.*.dll and so was invisible here.
    /// </summary>
    internal static class Program
    {
        private static readonly string[] DefaultTypes =
        {
            "TaleWorlds.CampaignSystem.CampaignEvents",
            "TaleWorlds.CampaignSystem.Kingdom",
            "TaleWorlds.CampaignSystem.Clan",
            "TaleWorlds.CampaignSystem.Actions.DeclareWarAction",
            "TaleWorlds.CampaignSystem.Actions.MakePeaceAction",
            "TaleWorlds.CampaignSystem.MapEvents.MapEvent",
            "TaleWorlds.CampaignSystem.Settlements.Settlement",
            "TaleWorlds.CampaignSystem.Settlements.Village",
            "TaleWorlds.CampaignSystem.Siege.SiegeEvent",
            "TaleWorlds.CampaignSystem.Party.PartyBase",
            "TaleWorlds.CampaignSystem.Party.MobileParty",
            "TaleWorlds.CampaignSystem.ComponentInterfaces.DiplomacyModel",
            "TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel",
            "TaleWorlds.CampaignSystem.Election.KingdomDecision",
        };

        private static int Main(string[] args)
        {
            var gameFolder = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR")
                             ?? @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord";
            var gameBin = Path.Combine(gameFolder, "bin", "Win64_Shipping_Client");
            if (!Directory.Exists(gameBin))
            {
                Console.WriteLine("FATAL: game bin not found: " + gameBin);
                return 2;
            }

            var repoRoot = FindRepoRoot();
            var outDir = Path.Combine(repoRoot, "artifacts", "api");
            Directory.CreateDirectory(outDir);

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(gameBin);

            // --from pulls in assemblies the game bin does not hold: module DLLs. Parsed out
            // of the argument list before the filters are read.
            var extraDlls = new List<string>();
            var filters = new List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "--from", StringComparison.Ordinal))
                {
                    filters.Add(args[i]);
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    Console.WriteLine("FATAL: --from needs a module name, a folder or a .dll path.");
                    return 2;
                }

                var where = args[++i];
                var resolved = ResolveSource(where, gameFolder);
                if (resolved.Count == 0)
                {
                    Console.WriteLine("FATAL: nothing to read for --from " + where);
                    return 2;
                }
                extraDlls.AddRange(resolved);
            }

            // Index every public type in the game assemblies once.
            var index = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);
            foreach (var dll in Directory.EnumerateFiles(gameBin, "TaleWorlds.*.dll"))
            {
                AssemblyDefinition asm;
                try
                {
                    asm = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { AssemblyResolver = resolver });
                }
                catch { continue; }

                // IsPublic is false for a public type nested in another type, and those are
                // real API - UIExtenderEx's PrefabExtensionTextAttribute is one, and it was
                // invisible here until this line said IsNestedPublic too.
                foreach (var type in asm.MainModule.GetTypes())
                    if ((type.IsPublic || type.IsNestedPublic) && !index.ContainsKey(type.FullName))
                        index[type.FullName] = type;
            }
            Console.WriteLine("indexed " + index.Count + " public types from " + gameBin);

            foreach (var dll in extraDlls)
            {
                AssemblyDefinition asm;
                try
                {
                    resolver.AddSearchDirectory(Path.GetDirectoryName(dll));
                    asm = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { AssemblyResolver = resolver });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  skipped " + dll + ": " + ex.Message);
                    continue;
                }

                var added = 0;
                foreach (var type in asm.MainModule.GetTypes())
                {
                    if ((!type.IsPublic && !type.IsNestedPublic) || index.ContainsKey(type.FullName)) continue;
                    index[type.FullName] = type;
                    added++;
                }
                Console.WriteLine("  +" + added + " public types from " + Path.GetFileName(dll));
            }

            var wanted = filters.Count > 0 ? filters.ToArray() : DefaultTypes;
            var written = 0;

            foreach (var name in wanted)
            {
                var matches = index.TryGetValue(name, out var exact)
                    ? new List<TypeDefinition> { exact }
                    : index.Where(kv => kv.Key.Contains(name, StringComparison.OrdinalIgnoreCase))
                        .Select(kv => kv.Value).Take(25).ToList();

                if (matches.Count == 0)
                {
                    Console.WriteLine("  NOT FOUND: " + name);
                    continue;
                }

                foreach (var type in matches)
                {
                    var file = Path.Combine(outDir, SafeFileName(type.FullName) + ".txt");
                    File.WriteAllText(file, Describe(type), Encoding.UTF8);
                    Console.WriteLine("  " + type.FullName + "  ->  artifacts/api/" + Path.GetFileName(file));
                    written++;
                }
            }

            Console.WriteLine("wrote " + written + " file(s).");
            return 0;
        }

        private static string Describe(TypeDefinition type)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// " + type.Module.Assembly.Name.Name + " :: " + type.FullName);
            sb.AppendLine("// base: " + (type.BaseType?.FullName ?? "-")
                          + (type.IsEnum ? "   [enum]" : "")
                          + (type.IsAbstract && type.IsSealed ? "   [static]" : ""));
            sb.AppendLine();

            if (type.IsEnum)
            {
                foreach (var f in type.Fields.Where(f => f.HasConstant))
                    sb.AppendLine("  " + f.Name + " = " + f.Constant);
                return sb.ToString();
            }

            var fields = type.Fields.Where(f => f.IsPublic).ToList();
            if (fields.Count > 0)
            {
                sb.AppendLine("FIELDS");
                foreach (var f in fields.OrderBy(f => f.Name, StringComparer.Ordinal))
                    sb.AppendLine("  " + (f.IsStatic ? "static " : "") + Short(f.FieldType) + " " + f.Name);
                sb.AppendLine();
            }

            var props = type.Properties.Where(p =>
                (p.GetMethod?.IsPublic ?? false) || (p.SetMethod?.IsPublic ?? false)).ToList();
            if (props.Count > 0)
            {
                sb.AppendLine("PROPERTIES");
                foreach (var p in props.OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    var access = ((p.GetMethod?.IsPublic ?? false) ? "get;" : "")
                                 + ((p.SetMethod?.IsPublic ?? false) ? "set;" : "");
                    var isStatic = p.GetMethod?.IsStatic ?? p.SetMethod?.IsStatic ?? false;
                    sb.AppendLine("  " + (isStatic ? "static " : "") + Short(p.PropertyType) + " " + p.Name + " { " + access + " }");
                }
                sb.AppendLine();
            }

            var methods = type.Methods
                .Where(m => m.IsPublic && !m.IsGetter && !m.IsSetter && !m.IsAddOn && !m.IsRemoveOn)
                .ToList();
            if (methods.Count > 0)
            {
                sb.AppendLine("METHODS");
                foreach (var m in methods.OrderBy(m => m.Name, StringComparer.Ordinal))
                {
                    var ps = string.Join(", ", m.Parameters.Select(p =>
                        (p.ParameterType.IsByReference ? (p.IsOut ? "out " : "ref ") : "") + Short(p.ParameterType) + " " + p.Name));
                    sb.AppendLine("  " + (m.IsStatic ? "static " : "") + Short(m.ReturnType) + " " + m.Name + "(" + ps + ")");
                }
                sb.AppendLine();
            }

            var nested = type.NestedTypes.Where(n => n.IsNestedPublic).ToList();
            if (nested.Count > 0)
            {
                sb.AppendLine("NESTED");
                foreach (var n in nested)
                {
                    sb.AppendLine("  " + n.Name + (n.IsEnum ? " [enum]" : ""));
                    if (n.IsEnum)
                        foreach (var f in n.Fields.Where(f => f.HasConstant))
                            sb.AppendLine("      " + f.Name + " = " + f.Constant);
                }
            }

            return sb.ToString();
        }

        private static string Short(TypeReference t)
        {
            var name = t.Name;
            if (t is GenericInstanceType git)
                name = git.Name.Split('`')[0] + "<" + string.Join(", ", git.GenericArguments.Select(Short)) + ">";
            return name.TrimEnd('&');
        }

        /// <summary>
        /// A module name, a folder or a .dll. A bare name is looked up the way the game
        /// lays modules out, which is the common case and saves typing the whole path.
        /// </summary>
        private static List<string> ResolveSource(string where, string gameFolder)
        {
            var found = new List<string>();

            if (File.Exists(where) && where.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                found.Add(Path.GetFullPath(where));
                return found;
            }

            var dir = Directory.Exists(where)
                ? where
                : Path.Combine(gameFolder, "Modules", where, "bin", "Win64_Shipping_Client");

            if (!Directory.Exists(dir)) return found;

            foreach (var dll in Directory.EnumerateFiles(dir, "*.dll"))
                found.Add(dll);
            return found;
        }

        private static string SafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        private static string FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            for (var i = 0; i < 8 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir, "DiplomacyIntrigue.sln"))) return dir;
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }
            return Directory.GetCurrentDirectory();
        }
    }
}
