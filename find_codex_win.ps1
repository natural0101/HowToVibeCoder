Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinUIA {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$out = @()
$cb = [WinUIA+EnumWindowsProc]{
    param($hWnd, $lParam)
    if ([WinUIA]::IsWindowVisible($hWnd)) {
        $sb = New-Object System.Text.StringBuilder 256
        [WinUIA]::GetWindowText($hWnd, $sb, 256) | Out-Null
        $cls = New-Object System.Text.StringBuilder 256
        [WinUIA]::GetClassName($hWnd, $cls, 256) | Out-Null
        $pidOut = [uint32]0
        [WinUIA]::GetWindowThreadProcessId($hWnd, [ref]$pidOut) | Out-Null
        $p = Get-Process -Id $pidOut -ErrorAction SilentlyContinue
        if ($p.ProcessName -match "ChatGPT|Codex" -or $sb.ToString() -match "Codex") {
            $r = New-Object WinUIA+RECT
            [WinUIA]::GetWindowRect($hWnd, [ref]$r) | Out-Null
            $script:out += "HWND=$hWnd PID=$pidOut NAME=$($p.ProcessName) TITLE='$($sb.ToString())' CLASS='$($cls.ToString())' RECT=$($r.Left),$($r.Top),$($r.Right),$($r.Bottom)"
        }
    }
    return $true
}
[WinUIA]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$out | Set-Content "C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\codex_win_rect.txt" -Encoding UTF8