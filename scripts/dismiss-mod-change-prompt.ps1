# Watches for the "Mod change detected" prompt and clears it. Bannerlord shows it
# whenever a module DLL differs from the last run, and it blocks startup entirely -
# no log, no GABP bridge - so an automated deploy-and-test loop has to handle it.
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public class W { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
                 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n); }
"@
$deadline = (Get-Date).AddSeconds(150)
while ((Get-Date) -lt $deadline) {
    $p = Get-Process -Name "Bannerlord*" -ErrorAction SilentlyContinue |
         Where-Object { $_.MainWindowTitle -like "*Mod change*" } | Select-Object -First 1
    if ($p) {
        [W]::ShowWindow($p.MainWindowHandle, 9) | Out-Null
        [W]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
        Start-Sleep -Milliseconds 500
        [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
        Write-Output "dismissed"
        exit 0
    }
    Start-Sleep -Milliseconds 400
}
Write-Output "prompt never appeared"
