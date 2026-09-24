using System;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

// Prints the managed exception behind a game crash, from the minidump Windows writes.
//
// Why this exists: an exception thrown on the game's main thread during a campaign tick is
// caught by the engine's native crash handler before AppDomain.UnhandledException sees it, so
// the mod's own crash logger records nothing, and the rgl error log has a native stack only.
// The dump still holds the managed exception object. Written after a crash on 2026-09-23 that
// looked like a background stall for seventeen minutes (CLAUDE.md §1).
//
// Usage: dotnet run --project tools/DumpProbe -- [path-to.dmp]
//        with no path, reads the newest Bannerlord dump in %LOCALAPPDATA%\CrashDumps.
//        dotnet run --project tools/DumpProbe -- --pid <game pid>
//        reads a live game instead. One sitting on its crash-report dialog still holds the
//        exception on the faulting thread, and no dump exists until that dialog is closed.
//        Read-only: the process is snapshotted, not debugged, and carries on as it was.

var livePid = args.Length > 1 && args[0] == "--pid" ? int.Parse(args[1]) : 0;
var path = livePid != 0 ? null : (args.Length > 0 ? args[0] : NewestDump());
if (livePid == 0 && (path == null || !File.Exists(path)))
{
    Console.WriteLine("No dump found. Pass a path, or check %LOCALAPPDATA%\\CrashDumps.");
    return 1;
}
Console.WriteLine(livePid != 0 ? "Live process: " + livePid : "Dump: " + path + "  (" + File.GetLastWriteTime(path) + ")");

// The game runs on .NET Framework 4.x; its DAC ships with Windows.
const string Dac = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscordacwks.dll";

using var target = livePid != 0 ? DataTarget.AttachToProcess(livePid, suspend: false) : DataTarget.LoadDump(path);
if (target.ClrVersions.Length == 0)
{
    Console.WriteLine("No CLR in this dump - a purely native crash. Read rgl_log_errors_<pid>.txt instead.");
    return 1;
}

var clr = target.ClrVersions[0];
Console.WriteLine("CLR: " + clr.Flavor + " " + clr.Version);
var runtime = clr.CreateRuntime(Dac, ignoreMismatch: true);

var found = 0;
foreach (var thread in runtime.Threads)
{
    var ex = thread.CurrentException;
    if (ex == null) continue;
    found++;

    Console.WriteLine();
    Console.WriteLine("=== managed thread " + thread.ManagedThreadId + " (OS " + thread.OSThreadId + ")");
    for (var e = ex; e != null; e = e.Inner)
    {
        Console.WriteLine(e.Type?.Name + ": " + e.Message);
        foreach (var frame in e.StackTrace) Console.WriteLine("   at " + frame);
    }
}

if (found == 0)
    Console.WriteLine("No thread was holding a managed exception. The fault may be native, or the dump was taken without heap memory.");
return 0;

static string NewestDump()
{
    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
    if (!Directory.Exists(dir)) return null;
    return new DirectoryInfo(dir).GetFiles("Bannerlord*.dmp")
        .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()?.FullName;
}
