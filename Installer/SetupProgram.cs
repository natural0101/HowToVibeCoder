using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HowToVibeCoderSetup;

internal static class SetupProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length >= 2 && string.Equals(args[0], "--generate-icon", StringComparison.OrdinalIgnoreCase))
        {
            IconGenerator.CreateIconFile(args[1]);
            return 0;
        }

        if (args.Length >= 1 && string.Equals(args[0], "--uninstall", StringComparison.OrdinalIgnoreCase))
        {
            RunUninstaller();
            return 0;
        }

        if (args.Length >= 1 && (string.Equals(args[0], "--silent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "--verify-install", StringComparison.OrdinalIgnoreCase)))
        {
            bool verifyOnly = string.Equals(args[0], "--verify-install", StringComparison.OrdinalIgnoreCase);
            if (verifyOnly && args.Length < 2) return 2;
            string targetDir = args.Length >= 2 ? args[1] : @"C:\Games\HowToVibeCoder";
            InstallerEngine.InstallAll(
                targetDir,
                createDesktopShortcut: !verifyOnly,
                createStartMenuFolder: !verifyOnly,
                launchAfterInstall: false,
                progressCallback: null);
            return 0;
        }

        if (args.Length == 2 && string.Equals(args[0], "--verify-ui", StringComparison.OrdinalIgnoreCase))
        {
            using var preview = new SetupMainForm();
            preview.ShowInTaskbar = false;
            preview.StartPosition = FormStartPosition.Manual;
            preview.Location = new Point(-20000, -20000);
            preview.Show();
            Application.DoEvents();
            using var image = new Bitmap(preview.Width, preview.Height);
            preview.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(args[1], ImageFormat.Png);
            preview.Close();
            return 0;
        }

        Application.Run(new SetupMainForm());
        return 0;
    }

    private static void RunUninstaller()
    {
        string appDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        DialogResult res = MessageBox.Show(
            $"Удалить игру «How to Vibe Coder — Этаж 1 (Cloud Entropic Tower)» из папки:\n{appDir}\nи очистить ярлыки?",
            "Деинсталляция How to Vibe Coder",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (res != DialogResult.Yes)
            return;

        try
        {
            string desktopShortcut = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "How to Vibe Coder — Этаж 1.lnk");
            if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

            string startMenuFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "How to Vibe Coder");
            if (Directory.Exists(startMenuFolder)) Directory.Delete(startMenuFolder, true);

            string batPath = Path.Combine(Path.GetTempPath(), "htvc_cleanup.bat");
            File.WriteAllText(batPath,
                "@echo off\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                $"rd /s /q \"{appDir}\"\r\n" +
                "del \"%~f0\"\r\n");

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            MessageBox.Show(
                "Игра «How to Vibe Coder» успешно удалена с компьютера.",
                "Удаление завершено",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

public static class IconGenerator
{
    private static readonly int[] IcoFrameSizes = { 256, 64, 48, 32, 16 };

    public static Bitmap? LoadSourceAppIconBitmap()
    {
        try
        {
            string[] candidatePaths =
            {
                Path.Combine(AppContext.BaseDirectory, "assets", "app_icon.png"),
                @"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\app_icon.png",
                Path.Combine(AppContext.BaseDirectory, "..", "assets", "app_icon.png")
            };

            foreach (string path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using Image img = Image.FromStream(fs);
                    return new Bitmap(img);
                }
            }

            Assembly asm = Assembly.GetExecutingAssembly();
            using Stream? s = asm.GetManifestResourceStream("HowToVibeCoder.AppIcon.png");
            if (s != null)
            {
                using Image img = Image.FromStream(s);
                return new Bitmap(img);
            }
        }
        catch
        {
        }
        return null;
    }

    public static Bitmap? LoadMenuBannerBitmap()
    {
        try
        {
            string[] candidatePaths =
            {
                Path.Combine(AppContext.BaseDirectory, "assets", "menu_banner.png"),
                @"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\menu_banner.png",
                Path.Combine(AppContext.BaseDirectory, "menu_banner.png")
            };

            foreach (string path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using Image img = Image.FromStream(fs);
                    return new Bitmap(img);
                }
            }

            Assembly asm = Assembly.GetExecutingAssembly();
            using Stream? s = asm.GetManifestResourceStream("HowToVibeCoder.MenuBanner.png");
            if (s != null)
            {
                using Image img = Image.FromStream(s);
                return new Bitmap(img);
            }
        }
        catch
        {
        }
        return null;
    }

    public static Icon CreateMemoryIcon()
    {
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using Stream? icoStream = asm.GetManifestResourceStream("HowToVibeCoder.Icon.ico");
            if (icoStream != null)
            {
                return new Icon(icoStream, new Size(64, 64));
            }
        }
        catch
        {
        }

        using Bitmap bmp = RenderIconBitmap(64);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public static void CreateIconFile(string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        using Bitmap? srcIcon = LoadSourceAppIconBitmap();
        System.Collections.Generic.List<byte[]> pngFrames = new(IcoFrameSizes.Length);

        foreach (int sz in IcoFrameSizes)
        {
            using Bitmap frameBmp = srcIcon != null
                ? ResizeHighQuality(srcIcon, sz, sz)
                : RenderIconBitmap(sz);

            using MemoryStream ms = new();
            frameBmp.Save(ms, ImageFormat.Png);
            pngFrames.Add(ms.ToArray());
        }

        using FileStream fs = new(outputPath, FileMode.Create, FileAccess.Write);
        using BinaryWriter bw = new(fs);

        int count = IcoFrameSizes.Length;
        bw.Write((ushort)0); // Reserved
        bw.Write((ushort)1); // Type = 1 (ICO)
        bw.Write((ushort)count);

        uint offset = (uint)(6 + 16 * count);
        for (int i = 0; i < count; i++)
        {
            int sz = IcoFrameSizes[i];
            byte dimByte = sz >= 256 ? (byte)0 : (byte)sz;
            byte[] bytes = pngFrames[i];

            bw.Write(dimByte);   // Width (0 = 256)
            bw.Write(dimByte);   // Height (0 = 256)
            bw.Write((byte)0);   // ColorCount
            bw.Write((byte)0);   // Reserved
            bw.Write((ushort)1); // Planes
            bw.Write((ushort)32);// BitCount
            bw.Write((uint)bytes.Length);
            bw.Write(offset);
            offset += (uint)bytes.Length;
        }

        for (int i = 0; i < count; i++)
        {
            bw.Write(pngFrames[i]);
        }

        // Also mirror to assets/HowToVibeCoder.ico if in repo
        try
        {
            string assetsIco = @"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\HowToVibeCoder.ico";
            string fullOut = Path.GetFullPath(outputPath);
            if (!string.Equals(fullOut, Path.GetFullPath(assetsIco), StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(Path.GetDirectoryName(assetsIco)!))
            {
                File.Copy(fullOut, assetsIco, overwrite: true);
            }
        }
        catch
        {
        }
    }

    private static Bitmap ResizeHighQuality(Bitmap source, int width, int height)
    {
        Bitmap bmp = new(width, height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.DrawImage(source, new Rectangle(0, 0, width, height));
        return bmp;
    }

    public static Bitmap RenderIconBitmap(int size)
    {
        using Bitmap? real = LoadSourceAppIconBitmap();
        if (real != null)
        {
            return ResizeHighQuality(real, size, size);
        }

        Bitmap bmp = new(size, size, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // 1. Фон: тёмный киберпанк с малиновым щитом 90-х
        using (LinearGradientBrush bgBrush = new(
            new Rectangle(0, 0, size, size),
            Color.FromArgb(165, 16, 62),
            Color.FromArgb(24, 14, 38),
            45f))
        {
            g.FillRectangle(bgBrush, 4, 4, size - 8, size - 8);
        }

        // Золотая рамка авторитета
        using (Pen goldPen = new(Color.FromArgb(255, 215, 64), Math.Max(2f, size * 0.055f)))
        {
            g.DrawRectangle(goldPen, 5, 5, size - 10, size - 10);
        }

        // 2. Светящийся неоновый Макбук-приманка внизу справа
        int mbX = (int)(size * 0.42f);
        int mbY = (int)(size * 0.50f);
        int mbW = (int)(size * 0.44f);
        int mbH = (int)(size * 0.30f);
        using (SolidBrush screenGlow = new(Color.FromArgb(38, 235, 155)))
        using (Pen screenBorder = new(Color.FromArgb(190, 255, 235), Math.Max(1.5f, size * 0.025f)))
        {
            g.FillRectangle(screenGlow, mbX, mbY, mbW, mbH);
            g.DrawRectangle(screenBorder, mbX, mbY, mbW, mbH);
            g.FillRectangle(Brushes.Silver, mbX - 4, mbY + mbH, mbW + 8, Math.Max(3, (int)(size * 0.05f)));
        }

        // 3. Золотой бандитский Micro-Uzi поверх малинового пиджака
        using (SolidBrush goldFill = new(Color.FromArgb(255, 212, 55)))
        using (Pen darkOutline = new(Color.FromArgb(20, 12, 28), Math.Max(1.5f, size * 0.025f)))
        {
            Rectangle uziBody = new((int)(size * 0.16f), (int)(size * 0.24f), (int)(size * 0.56f), (int)(size * 0.19f));
            Rectangle uziBarrel = new((int)(size * 0.68f), (int)(size * 0.28f), (int)(size * 0.17f), (int)(size * 0.09f));
            Rectangle uziGrip = new((int)(size * 0.34f), (int)(size * 0.41f), (int)(size * 0.14f), (int)(size * 0.32f));

            g.FillRectangle(goldFill, uziBarrel);
            g.DrawRectangle(darkOutline, uziBarrel);
            g.FillRectangle(goldFill, uziGrip);
            g.DrawRectangle(darkOutline, uziGrip);
            g.FillRectangle(goldFill, uziBody);
            g.DrawRectangle(darkOutline, uziBody);
        }

        return bmp;
    }
}

public static class InstallerEngine
{
    public static void InstallAll(
        string installDir,
        bool createDesktopShortcut,
        bool createStartMenuFolder,
        bool launchAfterInstall,
        Action<int, string>? progressCallback)
    {
        installDir = Path.GetFullPath(installDir);
        Directory.CreateDirectory(installDir);
        Directory.CreateDirectory(Path.Combine(installDir, "assets", "sounds"));
        Directory.CreateDirectory(Path.Combine(installDir, "saves"));
        Directory.CreateDirectory(Path.Combine(installDir, "mods"));
        Directory.CreateDirectory(Path.Combine(installDir, "screenshots"));

        progressCallback?.Invoke(8, "Генерация фирменной иконки HowToVibeCoder.ico...");
        string icoPath = Path.Combine(installDir, "HowToVibeCoder.ico");
        using (Stream icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("HowToVibeCoder.Icon.ico")
            ?? throw new InvalidDataException("Installer icon resource is missing."))
        using (FileStream output = File.Create(icoPath)) icon.CopyTo(output);

        // Проверяем вшитый ресурс HowToVibeCoder.Payload.zip
        Assembly asm = Assembly.GetExecutingAssembly();
        using Stream? payloadStream = asm.GetManifestResourceStream("HowToVibeCoder.Payload.zip");

        if (payloadStream != null)
        {
            using ZipArchive archive = new(payloadStream, ZipArchiveMode.Read);
            int total = Math.Max(1, archive.Entries.Count);
            int current = 0;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                current++;
                int pct = 12 + (int)((current / (float)total) * 72f);
                string destPath = Path.GetFullPath(Path.Combine(installDir, entry.FullName));

                if (!destPath.StartsWith(Path.TrimEndingDirectorySeparator(installDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Invalid payload entry: " + entry.FullName);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                progressCallback?.Invoke(pct, $"Распаковка: {entry.FullName}");
                entry.ExtractToFile(destPath, overwrite: true);
            }
        }
        else
        {
            // Fallback: если запущен из папки рядом со сборкой игры
            string fallbackSrc = @"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\bin\Release\net10.0";
            if (!Directory.Exists(fallbackSrc))
            {
                fallbackSrc = @"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\bin\Debug\net10.0";
            }

            if (Directory.Exists(fallbackSrc))
            {
                string[] files = Directory.GetFiles(fallbackSrc, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string rel = Path.GetRelativePath(fallbackSrc, files[i]);
                    string dest = Path.Combine(installDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    int pct = 12 + (int)(((i + 1) / (float)Math.Max(1, files.Length)) * 72f);
                    progressCallback?.Invoke(pct, $"Копирование: {rel}");
                    File.Copy(files[i], dest, overwrite: true);
                }
            }
        }

        // Создаём Uninstall.bat и Uninstall.exe в папке установки
        progressCallback?.Invoke(88, "Создание деинсталлятора Uninstall.exe и Uninstall.bat...");
        CreateUninstallerFiles(installDir);

        string exePath = Path.Combine(installDir, "HowToVibeCoder.exe");

        if (createDesktopShortcut)
        {
            progressCallback?.Invoke(93, "Создание ярлыка на Рабочем столе...");
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string lnkPath = Path.Combine(desktopDir, "How to Vibe Coder — Этаж 1.lnk");
            CreateWindowsShortcut(lnkPath, exePath, installDir, icoPath, "How to Vibe Coder — Этаж 1 (Cloud Entropic Tower)");
        }

        if (createStartMenuFolder)
        {
            progressCallback?.Invoke(97, "Создание папки в меню Пуск...");
            string programsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "How to Vibe Coder");
            Directory.CreateDirectory(programsDir);

            CreateWindowsShortcut(
                Path.Combine(programsDir, "How to Vibe Coder — Этаж 1.lnk"),
                exePath,
                installDir,
                icoPath,
                "Запустить How to Vibe Coder — Этаж 1");

            CreateWindowsShortcut(
                Path.Combine(programsDir, "Удалить How to Vibe Coder.lnk"),
                Path.Combine(installDir, "Uninstall.exe"),
                installDir,
                icoPath,
                "Удалить игру How to Vibe Coder");
        }

        progressCallback?.Invoke(100, "Установка успешно завершена! Этаж 1 готов к штурму.");

        if (launchAfterInstall && File.Exists(exePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = installDir,
                UseShellExecute = true
            });
        }
    }

    private static void CreateUninstallerFiles(string installDir)
    {
        try
        {
            string currentSetupExe = Environment.ProcessPath ?? "";
            string uninstallExe = Path.Combine(installDir, "Uninstall.exe");
            if (!string.IsNullOrEmpty(currentSetupExe) && File.Exists(currentSetupExe))
            {
                File.Copy(currentSetupExe, uninstallExe, overwrite: true);
            }
        }
        catch
        {
        }

        string uninstallBat = Path.Combine(installDir, "Uninstall.bat");
        File.WriteAllText(uninstallBat,
            "@echo off\r\n" +
            "chcp 65001 >nul\r\n" +
            "echo Удаление How to Vibe Coder — Этаж 1 (Cloud Entropic Tower)...\r\n" +
            "if exist \"%~dp0Uninstall.exe\" (\r\n" +
            "    start \"\" \"%~dp0Uninstall.exe\" --uninstall\r\n" +
            ") else (\r\n" +
            "    del /q \"%USERPROFILE%\\Desktop\\How to Vibe Coder — Этаж 1.lnk\" 2>nul\r\n" +
            "    echo Папка игры: %~dp0\r\n" +
            ")\r\n");
    }

    public static void CreateWindowsShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDir;
            if (File.Exists(iconPath))
            {
                shortcut.IconLocation = $"{iconPath},0";
            }
            shortcut.Description = description;
            shortcut.Save();
        }
        catch
        {
        }
    }
}

public sealed class SetupMainForm : Form
{
    private readonly TextBox _pathTextBox;
    private readonly Button _browseButton;
    private readonly CheckBox _chkDesktopShortcut;
    private readonly CheckBox _chkStartMenu;
    private readonly CheckBox _chkLaunchGame;
    private readonly Button _installButton;
    private readonly Button _exitButton;
    private readonly Label _statusLabel;
    private readonly Panel _progressPanel;
    private readonly Bitmap? _bannerBitmap;
    private readonly Bitmap? _headerIconBitmap;
    private int _progressPercent = 0;
    private bool _isInstalling = false;
    private bool _installFinished = false;

    public SetupMainForm()
    {
        Text = "Установка: How to Vibe Coder — Этаж 1 (Cloud Entropic Tower)";
        ClientSize = new Size(760, 510);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(12, 10, 20);
        ForeColor = Color.White;
        DoubleBuffered = true;

        try
        {
            Icon = IconGenerator.CreateMemoryIcon();
            _bannerBitmap = IconGenerator.LoadMenuBannerBitmap();
            _headerIconBitmap = IconGenerator.RenderIconBitmap(96);
        }
        catch
        {
        }

        // Поле выбора папки установки
        Label pathLabel = new()
        {
            Text = "ПАПКА УСТАНОВКИ ИГРЫ (RAYLIB-CS OPENGL 3D, АУДИО WAV, СОХРАНЕНИЯ И МОДЫ):",
            Location = new Point(28, 186),
            Size = new Size(700, 20),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 218, 85),
            BackColor = Color.Transparent
        };
        Controls.Add(pathLabel);

        _pathTextBox = new TextBox
        {
            Text = @"C:\Games\HowToVibeCoder",
            Location = new Point(28, 210),
            Size = new Size(576, 28),
            Font = new Font("Consolas", 11f, FontStyle.Bold),
            BackColor = Color.FromArgb(22, 18, 36),
            ForeColor = Color.FromArgb(135, 240, 255),
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_pathTextBox);

        _browseButton = new Button
        {
            Text = "ОБЗОР...",
            Location = new Point(614, 208),
            Size = new Size(118, 30),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(148, 20, 62),
            ForeColor = Color.FromArgb(255, 238, 155),
            Cursor = Cursors.Hand
        };
        _browseButton.FlatAppearance.BorderColor = Color.FromArgb(255, 212, 72);
        _browseButton.Click += OnBrowseClicked;
        Controls.Add(_browseButton);

        // 3 чекбокса опций установки
        _chkDesktopShortcut = CreateStyledCheckBox(
            "Создать ярлык на Рабочем столе («How to Vibe Coder — Этаж 1»)",
            new Point(32, 254),
            isChecked: true);
        Controls.Add(_chkDesktopShortcut);

        _chkStartMenu = CreateStyledCheckBox(
            "Создать папку в меню Пуск (включая ярлык быстрого запуска и Uninstall)",
            new Point(32, 284),
            isChecked: true);
        Controls.Add(_chkStartMenu);

        _chkLaunchGame = CreateStyledCheckBox(
            "Запустить игру сразу после завершения установки",
            new Point(32, 314),
            isChecked: true);
        Controls.Add(_chkLaunchGame);

        // Статус и прогресс-бар
        _statusLabel = new Label
        {
            Text = "Готов к установке: HowToVibeCoder.exe + Raylib-cs OpenGL 3D + 19 аудиофайлов WAV + менеджер модов",
            Location = new Point(28, 356),
            Size = new Size(704, 22),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(215, 228, 250),
            BackColor = Color.Transparent
        };
        Controls.Add(_statusLabel);

        _progressPanel = new Panel
        {
            Location = new Point(28, 382),
            Size = new Size(704, 36),
            BackColor = Color.FromArgb(20, 16, 32)
        };
        _progressPanel.Paint += OnProgressPanelPaint;
        Controls.Add(_progressPanel);

        // Кнопки Установить и Выход
        _installButton = new Button
        {
            Text = "▶ УСТАНОВИТЬ ИГРУ НА ПК",
            Location = new Point(28, 436),
            Size = new Size(534, 50),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(205, 22, 78),
            ForeColor = Color.FromArgb(255, 242, 165),
            Cursor = Cursors.Hand
        };
        _installButton.FlatAppearance.BorderColor = Color.FromArgb(255, 215, 68);
        _installButton.FlatAppearance.BorderSize = 2;
        _installButton.Click += OnInstallClicked;
        Controls.Add(_installButton);

        _exitButton = new Button
        {
            Text = "ВЫХОД",
            Location = new Point(576, 436),
            Size = new Size(156, 50),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(36, 28, 54),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        _exitButton.FlatAppearance.BorderColor = Color.FromArgb(135, 115, 175);
        _exitButton.Click += (_, _) => Close();
        Controls.Add(_exitButton);
    }

    private static CheckBox CreateStyledCheckBox(string text, Point location, bool isChecked)
    {
        return new CheckBox
        {
            Text = text,
            Location = location,
            Size = new Size(690, 24),
            Checked = isChecked,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = Color.FromArgb(245, 240, 255),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        const int headerH = 168;
        Rectangle headerRect = new(0, 0, ClientSize.Width, headerH);

        if (_bannerBitmap != null)
        {
            // Cover crop from menu_banner.png (1376x768)
            float scale = Math.Max(
                headerRect.Width / (float)_bannerBitmap.Width,
                headerRect.Height / (float)_bannerBitmap.Height);
            int srcW = Math.Min(_bannerBitmap.Width, (int)(headerRect.Width / scale));
            int srcH = Math.Min(_bannerBitmap.Height, (int)(headerRect.Height / scale));
            int srcX = (_bannerBitmap.Width - srcW) / 2;
            int srcY = Math.Max(0, (_bannerBitmap.Height - srcH) / 3);

            g.DrawImage(
                _bannerBitmap,
                headerRect,
                new Rectangle(srcX, srcY, srcW, srcH),
                GraphicsUnit.Pixel);

            // Тёмно-малиновая тактическая виньетка слева и снизу для контраста типографики
            using (LinearGradientBrush leftVignette = new(
                headerRect,
                Color.FromArgb(238, 12, 8, 22),
                Color.FromArgb(95, 24, 10, 32),
                LinearGradientMode.Horizontal))
            {
                g.FillRectangle(leftVignette, headerRect);
            }

            Rectangle bottomFade = new(0, headerH - 54, ClientSize.Width, 54);
            using (LinearGradientBrush bottomBrush = new(
                bottomFade,
                Color.FromArgb(0, 12, 10, 20),
                Color.FromArgb(245, 12, 10, 20),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bottomBrush, bottomFade);
            }
        }
        else
        {
            using LinearGradientBrush headerBrush = new(
                headerRect,
                Color.FromArgb(152, 16, 58),
                Color.FromArgb(28, 16, 42),
                LinearGradientMode.Vertical);
            g.FillRectangle(headerBrush, headerRect);
        }

        // Тонкие CRT-сканлайны поверх баннера
        using (Pen scanPen = new(Color.FromArgb(26, 0, 0, 0), 1f))
        {
            for (int sy = 0; sy < headerH; sy += 3)
            {
                g.DrawLine(scanPen, 0, sy, ClientSize.Width, sy);
            }
        }

        // Рамка шапки и внешняя тактическая окантовка окна
        using (Pen crimsonLine = new(Color.FromArgb(235, 34, 92), 3f))
        using (Pen goldLine = new(Color.FromArgb(255, 212, 64), 2f))
        {
            g.DrawLine(crimsonLine, 0, headerH - 2, ClientSize.Width, headerH - 2);
            g.DrawLine(goldLine, 0, headerH, ClientSize.Width, headerH);
            g.DrawRectangle(goldLine, 3, 3, ClientSize.Width - 6, ClientSize.Height - 6);
        }

        // Иконка герба слева в шапке с золотым щитом
        Rectangle iconBox = new(24, 24, 112, 112);
        using (SolidBrush iconShadow = new(Color.FromArgb(180, 0, 0, 0)))
        using (Pen iconBorder = new(Color.FromArgb(255, 215, 68), 2.5f))
        {
            g.FillRectangle(iconShadow, iconBox.X + 4, iconBox.Y + 5, iconBox.Width, iconBox.Height);
            if (_headerIconBitmap != null)
            {
                g.DrawImage(_headerIconBitmap, iconBox);
            }
            else
            {
                using Bitmap fallbackBmp = IconGenerator.RenderIconBitmap(112);
                g.DrawImage(fallbackBmp, iconBox);
            }
            g.DrawRectangle(iconBorder, iconBox);
        }

        // Бейдж релиза в правом верхнем углу
        Rectangle badgeRect = new(ClientSize.Width - 192, 18, 168, 26);
        using (SolidBrush badgeBg = new(Color.FromArgb(225, 185, 20, 68)))
        using (Pen badgeBorder = new(Color.FromArgb(255, 215, 72), 1.5f))
        using (Font badgeFont = new("Consolas", 9f, FontStyle.Bold))
        {
            g.FillRectangle(badgeBg, badgeRect);
            g.DrawRectangle(badgeBorder, badgeRect);
            g.DrawString("BUILD v1.0 // WIN-X64", badgeFont, Brushes.White, badgeRect.X + 10, badgeRect.Y + 5);
        }

        using Font tagFont = new("Consolas", 9.5f, FontStyle.Bold);
        using Font titleFont = new("Segoe UI", 21f, FontStyle.Bold);
        using Font subTitleFont = new("Segoe UI", 11f, FontStyle.Bold);
        using Font quoteFont = new("Segoe UI", 10f, FontStyle.Italic);
        using SolidBrush shadowBrush = new(Color.FromArgb(220, 0, 0, 0));
        using SolidBrush goldBrush = new(Color.FromArgb(255, 222, 82));
        using SolidBrush crimsonGlow = new(Color.FromArgb(235, 38, 96));
        using SolidBrush cyanBrush = new(Color.FromArgb(115, 238, 255));

        g.DrawString("CLOUD ENTROPIC TOWER // ОФИЦИАЛЬНЫЙ МАСТЕР УСТАНОВКИ", tagFont, shadowBrush, 153, 25);
        g.DrawString("CLOUD ENTROPIC TOWER // ОФИЦИАЛЬНЫЙ МАСТЕР УСТАНОВКИ", tagFont, goldBrush, 152, 24);

        g.DrawString("HOW TO VIBE CODER", titleFont, shadowBrush, 152, 47);
        g.DrawString("HOW TO VIBE CODER", titleFont, crimsonGlow, 151, 46);
        g.DrawString("HOW TO VIBE CODER", titleFont, Brushes.White, 150, 44);

        Rectangle floorBadge = new(152, 86, 380, 24);
        using (SolidBrush floorBg = new(Color.FromArgb(210, 148, 18, 56)))
        using (Pen floorPen = new(Color.FromArgb(255, 208, 68), 1f))
        {
            g.FillRectangle(floorBg, floorBadge);
            g.DrawRectangle(floorPen, floorBadge);
        }
        g.DrawString("ЭТАЖ 1: ОПЕНСПЕЙС «БЕЛАЯ АКУЛА» & ЛИФТ ИИ-АГЕНТА", subTitleFont, goldBrush, 160, 88);

        g.DrawString("«Ты решал вопросы паяльником в 94-м. Теперь у тебя IT-галера в 2026-м.»", quoteFont, shadowBrush, 153, 121);
        g.DrawString("«Ты решал вопросы паяльником в 94-м. Теперь у тебя IT-галера в 2026-м.»", quoteFont, cyanBrush, 152, 120);
    }

    private void OnProgressPanelPaint(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle r = _progressPanel.ClientRectangle;

        int fillW = (int)((r.Width - 4) * (_progressPercent / 100f));
        if (fillW > 0)
        {
            Rectangle fillRect = new(2, 2, fillW, r.Height - 4);
            using LinearGradientBrush barBrush = new(
                fillRect,
                Color.FromArgb(215, 28, 82),
                Color.FromArgb(255, 205, 58),
                LinearGradientMode.Horizontal);
            g.FillRectangle(barBrush, fillRect);
        }

        using Pen borderPen = new(Color.FromArgb(255, 212, 72), 2f);
        g.DrawRectangle(borderPen, 1, 1, r.Width - 2, r.Height - 2);

        string pctText = $"{_progressPercent}%";
        using Font f = new("Segoe UI", 10f, FontStyle.Bold);
        SizeF sz = g.MeasureString(pctText, f);
        g.DrawString(pctText, f, Brushes.White, (r.Width - sz.Width) / 2f, (r.Height - sz.Height) / 2f);
    }

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        if (_isInstalling) return;
        using FolderBrowserDialog dlg = new()
        {
            Description = "Выберите папку для установки How to Vibe Coder — Этаж 1",
            SelectedPath = _pathTextBox.Text,
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            _pathTextBox.Text = dlg.SelectedPath;
        }
    }

    private async void OnInstallClicked(object? sender, EventArgs e)
    {
        if (_installFinished)
        {
            Close();
            return;
        }

        if (_isInstalling)
            return;

        string targetDir = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show("Укажите корректный путь установки!", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isInstalling = true;
        _installButton.Enabled = false;
        _browseButton.Enabled = false;
        _pathTextBox.ReadOnly = true;

        bool desktopLnk = _chkDesktopShortcut.Checked;
        bool startMenu = _chkStartMenu.Checked;
        bool launchAfter = _chkLaunchGame.Checked;

        try
        {
            await Task.Run(() =>
            {
                InstallerEngine.InstallAll(
                    targetDir,
                    desktopLnk,
                    startMenu,
                    launchAfter,
                    (pct, msg) =>
                    {
                        if (IsHandleCreated)
                        {
                            BeginInvoke(new Action(() =>
                            {
                                _progressPercent = Math.Clamp(pct, 0, 100);
                                _statusLabel.Text = msg;
                                _progressPanel.Invalidate();
                            }));
                        }
                    });
            });

            _progressPercent = 100;
            _progressPanel.Invalidate();
            _statusLabel.Text = $"УСТАНОВКА ЗАВЕРШЕНА В {targetDir}! Создан Uninstall.exe и ярлыки.";
            _installButton.Text = "✔ ГОТОВО (ЗАКРЫТЬ УСТАНОВЩИК)";
            _installButton.BackColor = Color.FromArgb(28, 148, 86);
            _installButton.Enabled = true;
            _installFinished = true;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Ошибка установки: {ex.Message}";
            MessageBox.Show($"Ошибка во время установки:\n{ex}", "Ошибка установки", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _installButton.Enabled = true;
            _browseButton.Enabled = true;
            _pathTextBox.ReadOnly = false;
            _isInstalling = false;
        }
    }
}
