Add-Type @"
using System;
using System.Runtime.InteropServices;
public class NativeCodexInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_V = 0x56;
    public const byte VK_RETURN = 0x0D;

    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(100);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(150);
    }

    public static void PasteAndSend() {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(40);
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(40);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        System.Threading.Thread.Sleep(40);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        System.Threading.Thread.Sleep(350);

        // Press Enter AND click the black send button circle at (2506, 1348)
        keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(40);
        keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        System.Threading.Thread.Sleep(250);
        Click(2506, 1348);
    }
}
"@
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$hwnd = [IntPtr]1312404
[NativeCodexInput]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 300

$chats = @(
    "ЦЕХ 1 (Левел-дизайн этажа Белая Акула 76x76м): Открой проект C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder и доведи LevelAndEntities.cs до идеала в стиле How to Fish (без проволочных рамок DrawCubeWires, мягкий гранёный flat-shaded 3D свет, центральная шахта лифта 9x9м, развилки влево и вправо, 85+ офисных 3D-предметов).",
    "ЦЕХ 2 (Оружие 90-х и 3D-руки в стиле How to Fish): В C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder обнови WeaponAndCombatView.cs так, чтобы две крупные гранёные low-poly руки снизу экрана выглядели 1-в-1 как в How to Fish, а Ремень с пряжкой, Паяльник с дымом канифоли, Micro-Uzi и Обрез имели сочные 3D-анимации без проволочных линий.",
    "ЦЕХ 3 (Звуки ремня и паяльника, музыка 90-х и озвучка): В C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder проверь и улучши AudioMusicSystem.cs — реалистичный свист и кожаный шлепок ремня, шипение канифоли и треск паяльника, выстрелы УЗИ и обреза, синтвейв и голоса.",
    "ЦЕХ 4 (Окна [F8] Звуковая рубка, [K] Контрабандный кейс и [N] Досье и слив чатов): В C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder обнови LoreAndMenusUI.cs точно под визуальный стиль наших модов How to Fish (C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\ui_f8_audio_settings.png, ui_k_knife_cases.png, ui_n_lore_codex.png).",
    "ЦЕХ 5 (Главное Меню и Настройки 1-в-1 как в How to Fish): В C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder переделай MainMenuAndPauseUI.cs точно по эталону C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\ui_native_options_audio.png — крупный логотип How to Vibe слева сверху и колонка из 7 скруглённых кнопок слева снизу (Создать игру, Войти в игру, Персонаж, Настройки, Настройки модов, Создатели, Выход).",
    "ЦЕХ 6 (Боевой HUD в стиле How to Fish, сборка Program.cs и 8 скриншотов --autotest): В C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder переделай HUD в Program.cs точно под эталон C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\.user_uploaded\media_1794908059787_a2e5632e.jpg ($450 и кружки сердечка/сытости слева внизу, 4 квадратных слота 1..4 с иконками оружия внизу по центру, уровень 1 + XP справа вверху, чистый белый текст по центру без рамок), собери Release и сними 8 скриншотов --autotest.",
    "ЦЕХ 7 (Установщик HowToVibeCoder_Setup.exe с AAA-иконкой и релиз в GitHub): В C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\Installer перепакуй финальный релиз с новой иконкой HowToVibeCoder.ico на Рабочий стол и обнови GitHub-репозиторий natural0101/HowToVibeCoder."
)

foreach ($msg in $chats) {
    [System.Windows.Forms.Clipboard]::SetText($msg)
    Start-Sleep -Milliseconds 100
    # 1. Click "Новый чат" at (1567, 78)
    [NativeCodexInput]::Click(1567, 78)
    Start-Sleep -Milliseconds 450
    # 2. Click "Поручите что угодно" input field at (1950, 1305)
    [NativeCodexInput]::Click(1950, 1305)
    Start-Sleep -Milliseconds 200
    # 3. Paste via keybd_event Ctrl+V and click Send button at (2506, 1348)
    [NativeCodexInput]::PasteAndSend()
    Start-Sleep -Milliseconds 1600
}

# Capture verification screenshot
$bmp = New-Object System.Drawing.Bitmap(2560, 1440)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
$g.Dispose()
$bmp.Save("C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\desktop_codex_7_running.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()