Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(2560, 1440)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
$g.Dispose()
$bmp.Save("C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\desktop_codex_check.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()