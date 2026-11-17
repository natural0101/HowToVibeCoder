Add-Type @"
using System;
using System.Runtime.InteropServices;
public class CodexAppWin {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@
Add-Type -AssemblyName System.Windows.Forms
$p = Get-Process -Id 14680 -ErrorAction SilentlyContinue
if (-not $p) {
    $p = Get-Process -Name "ChatGPT" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
}
if ($p -and $p.MainWindowHandle -ne 0) {
    [CodexAppWin]::ShowWindow($p.MainWindowHandle, 9) | Out-Null
    [CodexAppWin]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 400
    # Refresh Codex Desktop webview so the 7 promoted Workshop threads in state_5.sqlite + HowToVibeCoder project show up immediately in the left sidebar
    [System.Windows.Forms.SendKeys]::SendWait("^r")
}