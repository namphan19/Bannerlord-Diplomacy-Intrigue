using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;

namespace LoadProbe
{
    /// <summary>
    /// Pre-flight check for the module assembly, run before launching the game.
    ///
    /// Why this tool exists: when a module assembly fails to load, none of the module's
    /// own code runs, so it cannot log anything. All the player sees is the game's
    /// message "&lt;module&gt; submodule could not be loaded correctly due to a dependency
    /// conflict", which names neither the cause nor the culprit. Everything this tool
    /// checks was learned by hitting exactly that wall.
    ///
    ///     dotnet run --project tools/LoadProbe [-- path-to-dll [game-folder]]
    ///
    /// Exit code 0 = would load, 1 = would fail, 2 = could not run the check.
    /// Analysis is pure metadata (Mono.Cecil): nothing is executed, and the tool is not
    /// affected by the runtime it happens to be running on.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var repoRoot = FindRepoRoot();
            var moduleName = "DiplomacyIntrigue";

            // Drop anything that looks like a flag: a misplaced dotnet switch would
            // otherwise be read as the assembly path and reported as a missing file.
            var positional = args.Where(a => !a.StartsWith("-", StringComparison.Ordinal)).ToArray();

            var dllPath = positional.Length > 0
                ? positional[0]
                : Path.Combine(repoRoot, "module", moduleName, "bin", "Win64_Shipping_Client", moduleName + ".dll");

            var gameFolder = positional.Length > 1
                ? positional[1]
                : Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR")
                  ?? @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord";

            var gameBin = Path.Combine(gameFolder, "bin", "Win64_Shipping_Client");

            if (!File.Exists(dllPath)) return Fatal("module assembly not found: " + dllPath);
            if (!Directory.Exists(gameBin)) return Fatal("game bin folder not found: " + gameBin);

            Console.WriteLine("module : " + dllPath);
            Console.WriteLine("game   : " + gameFolder);
            Console.WriteLine();

            var searchDirs = BuildSearchDirs(gameFolder, gameBin, Path.GetDirectoryName(dllPath));
            var resolver = new DefaultAssemblyResolver();
            foreach (var dir in searchDirs) resolver.AddSearchDirectory(dir);

            AssemblyDefinition module;
            try
            {
                module = AssemblyDefinition.ReadAssembly(dllPath,
                    new ReaderParameters { AssemblyResolver = resolver });
            }
            catch (Exception ex)
            {
                return Fatal("cannot read the module assembly: " + ex.GetType().Name + ": " + ex.Message);
            }

            var failed = false;
            failed |= !CheckTargetFramework(module, gameBin);
            failed |= !CheckReferences(module, searchDirs, gameBin, gameFolder);
            failed |= !CheckSubModuleXml(module, dllPath, resolver);

            Console.WriteLine();
            Console.WriteLine(failed
                ? "RESULT: this module would FAIL to load. Fix the issues above before launching."
                : "RESULT: no load blockers found.");
            return failed ? 1 : 0;
        }

        /// <summary>
        /// The check that matters most, and the one that is easiest to get wrong.
        ///
        /// The Win64 shipping client is a .NET Framework 4.7.2 host - the give-away is
        /// Bannerlord.BLSE.Standalone.exe.config and the absence of coreclr/hostfxr next to
        /// it. (The Microsoft.NETCore.App folder inside bin belongs to the Gaming.Desktop
        /// build, not this one.) A net6.0 module reads fine as metadata, so the build and
        /// even a naive load test succeed, and then the game fails to load its types and
        /// blames a "dependency conflict".
        /// </summary>
        private static bool CheckTargetFramework(AssemblyDefinition module, string gameBin)
        {
            Console.WriteLine("=== 1. Target framework ===");

            var tfm = module.CustomAttributes
                .FirstOrDefault(a => a.AttributeType.Name == "TargetFrameworkAttribute")
                ?.ConstructorArguments[0].Value as string;

            var coreRef = module.MainModule.AssemblyReferences
                .FirstOrDefault(r => r.Name == "System.Runtime" || r.Name == "mscorlib" || r.Name == "netstandard");

            Console.WriteLine("  module targets : " + (tfm ?? "(not declared)"));
            Console.WriteLine("  core reference : " + (coreRef == null ? "(none)" : coreRef.Name + " " + coreRef.Version));

            var hostIsNetFramework = File.Exists(Path.Combine(gameBin, "Bannerlord.BLSE.Standalone.exe.config"))
                                     || !File.Exists(Path.Combine(gameBin, "coreclr.dll"));
            Console.WriteLine("  game host      : " + (hostIsNetFramework ? ".NET Framework" : ".NET (Core)"));

            // A .NET 5+ module is identified by a System.Runtime reference of version 5 or above.
            var isNetCoreModule = coreRef != null && coreRef.Name == "System.Runtime" && coreRef.Version.Major >= 5;

            if (hostIsNetFramework && isNetCoreModule)
            {
                Console.WriteLine();
                Console.WriteLine("  FAIL: the module targets .NET 5+ but the game host is .NET Framework.");
                Console.WriteLine("        The game will report a \"dependency conflict\" and load nothing.");
                Console.WriteLine("        Fix: set <TargetFramework>net472</TargetFramework>.");
                return false;
            }

            Console.WriteLine("  OK");
            return true;
        }

        /// <summary>
        /// Vanilla walks the module's references and loads every one whose file name does
        /// not start with System, mscorlib or netstandard - by bare file name, resolved
        /// against the game bin folder. Anything living only in another module's folder
        /// relies on BLSE having already loaded it, so it is worth seeing the list.
        /// </summary>
        private static bool CheckReferences(AssemblyDefinition module, List<string> searchDirs,
            string gameBin, string gameFolder)
        {
            Console.WriteLine();
            Console.WriteLine("=== 2. Assembly references ===");

            var missing = new List<string>();
            foreach (var reference in module.MainModule.AssemblyReferences
                         .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                var file = FindFile(searchDirs, reference.Name);
                var note = "";

                if (file == null)
                {
                    missing.Add(reference.Name);
                    note = "*** NOT FOUND ***";
                }
                else
                {
                    note = Shorten(file, gameFolder);
                    var skippedByLoader = reference.Name.StartsWith("System", StringComparison.Ordinal)
                                          || reference.Name.StartsWith("mscorlib", StringComparison.Ordinal)
                                          || reference.Name.StartsWith("netstandard", StringComparison.Ordinal);
                    var inGameBin = File.Exists(Path.Combine(gameBin, reference.Name + ".dll"));
                    if (!skippedByLoader && !inGameBin)
                        note += "   (cross-module: needs BLSE to have loaded it first)";
                }

                Console.WriteLine(string.Format("  {0,-40} {1,-14} {2}", reference.Name, reference.Version, note));
            }

            if (missing.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  FAIL: not present anywhere the game can see: " + string.Join(", ", missing));
                return false;
            }

            Console.WriteLine("  OK");
            return true;
        }

        /// <summary>
        /// The game reads SubModuleClassType from SubModule.xml and does
        /// assembly.GetType(name).GetConstructor(...). A typo there is an instant failure
        /// with no useful message, so check the manifest against the real metadata.
        /// </summary>
        private static bool CheckSubModuleXml(AssemblyDefinition module, string dllPath, IAssemblyResolver resolver)
        {
            Console.WriteLine();
            Console.WriteLine("=== 3. SubModule.xml entry point ===");

            // module/<Name>/bin/Win64_Shipping_Client/x.dll  ->  module/<Name>/SubModule.xml
            var moduleRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dllPath), "..", ".."));
            var xmlPath = Path.Combine(moduleRoot, "SubModule.xml");

            var declared = module.MainModule.Types
                .Where(t => t.IsPublic && !t.IsAbstract && InheritsFrom(t, "TaleWorlds.MountAndBlade.MBSubModuleBase"))
                .ToList();

            foreach (var t in declared)
            {
                var hasCtor = t.Methods.Any(m => m.IsConstructor && m.IsPublic && m.Parameters.Count == 0);
                Console.WriteLine("  found entry point: " + t.FullName + (hasCtor ? "" : "   *** no public parameterless ctor ***"));
            }
            if (declared.Count == 0)
                Console.WriteLine("  WARNING: no public MBSubModuleBase subclass in this assembly.");

            if (!File.Exists(xmlPath))
            {
                Console.WriteLine("  SubModule.xml not found at " + xmlPath + " - skipping cross-check.");
                return true;
            }

            var ok = true;
            var xml = XDocument.Load(xmlPath);
            var dllFileName = Path.GetFileName(dllPath);

            foreach (var sub in xml.Descendants("SubModule"))
            {
                var typeName = sub.Element("SubModuleClassType")?.Attribute("value")?.Value;
                var dllName = sub.Element("DLLName")?.Attribute("value")?.Value;

                if (!string.Equals(dllName, dllFileName, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("  FAIL: SubModule.xml DLLName is \"" + dllName + "\" but the built assembly is \"" + dllFileName + "\".");
                    ok = false;
                    continue;
                }

                var match = declared.FirstOrDefault(t => t.FullName == typeName);
                if (match == null)
                {
                    Console.WriteLine("  FAIL: SubModuleClassType \"" + typeName + "\" is not a loadable entry point in " + dllFileName + ".");
                    if (declared.Count > 0)
                        Console.WriteLine("        Did you mean: " + string.Join(", ", declared.Select(t => t.FullName)) + " ?");
                    ok = false;
                }
                else
                {
                    Console.WriteLine("  OK: " + typeName + " matches " + dllName + ".");
                }
            }

            return ok;
        }

        private static bool InheritsFrom(TypeDefinition type, string baseFullName)
        {
            var current = type;
            while (current?.BaseType != null)
            {
                if (current.BaseType.FullName == baseFullName) return true;
                try { current = current.BaseType.Resolve(); }
                catch { return false; }
            }
            return false;
        }

        private static List<string> BuildSearchDirs(string gameFolder, string gameBin, string moduleDir)
        {
            var dirs = new List<string>();
            if (!string.IsNullOrEmpty(moduleDir)) dirs.Add(moduleDir);
            dirs.Add(gameBin);

            var modulesRoot = Path.Combine(gameFolder, "Modules");
            if (Directory.Exists(modulesRoot))
            {
                foreach (var dir in Directory.EnumerateDirectories(modulesRoot))
                {
                    var bin = Path.Combine(dir, "bin", "Win64_Shipping_Client");
                    if (Directory.Exists(bin)) dirs.Add(bin);
                }
            }

            // Reference assemblies for the BCL, so mscorlib/netstandard resolve for base-type walks.
            var refPacks = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages", "microsoft.netframework.referenceassemblies.net472", "1.0.3",
                "build", ".NETFramework", "v4.7.2");
            if (Directory.Exists(refPacks)) dirs.Add(refPacks);

            return dirs;
        }

        private static string FindFile(List<string> dirs, string simpleName)
        {
            foreach (var dir in dirs)
            {
                var candidate = Path.Combine(dir, simpleName + ".dll");
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static string Shorten(string path, string gameFolder)
            => path.StartsWith(gameFolder, StringComparison.OrdinalIgnoreCase)
                ? "<game>" + path.Substring(gameFolder.Length)
                : path;

        private static int Fatal(string message)
        {
            Console.WriteLine("FATAL: " + message);
            return 2;
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
