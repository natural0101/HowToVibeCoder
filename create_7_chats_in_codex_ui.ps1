Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinInput {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;

    public static void ClickAt(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(120);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(180);
    }
}
"@
Add-Type -AssemblyName System.Windows.Forms

$hwnd = [IntPtr]1312404
[WinInput]::ShowWindow($hwnd, 9) | Out-Null
[WinInput]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 400

# Window rect is Left=1452, Top=0, Right=2548, Bottom=1392
# "Новый чат" button in left sidebar: X = 1452 + 115 = 1567, Y = 110
# Composer input box "Работайте с Codex": X = 1452 + 580 = 2032, Y = 1295

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
    [WinInput]::SetForegroundWindow($hwnd) | Out-Null
    # 1. Click "Новый чат" button at (1567, 110)
    [WinInput]::ClickAt(1567, 110)
    Start-Sleep -Milliseconds 650

    # 2. Click inside "Работайте с Codex" input box at (2032, 1295)
    [WinInput]::ClickAt(2032, 1295)
    Start-Sleep -Milliseconds 250

    # 3. Copy message to Clipboard and paste via Ctrl+V, then press Enter
    [System.Windows.Forms.Clipboard]::SetText($msg)
    Start-Sleep -Milliseconds 120
    [System.Windows.Forms.SendKeys]::SendWait("^v")
    Start-Sleep -Milliseconds 350
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
    Start-Sleep -Milliseconds 1400
}