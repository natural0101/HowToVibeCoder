using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HowToVibeCoderSetup;

internal static class SetupProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
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

        if (args.Length >= 2 && (string.Equals(args[0], "--verify-ui", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "--verify-ui-progress", StringComparison.OrdinalIgnoreCase)))
        {
            bool showProgress = string.Equals(args[0], "--verify-ui-progress", StringComparison.OrdinalIgnoreCase);
            using var preview = new SetupMainForm();
            preview.ShowInTaskbar = false;
            preview.StartPosition = FormStartPosition.Manual;
            preview.Location = new Point(-20000, -20000);
            if (showProgress)
            {
                preview.ApplyPreviewProgressState(64, "Распаковка файлов игры…");
            }
            preview.Show();
            Application.DoEvents();
            using var image = new Bitmap(preview.Width, preview.Height, PixelFormat.Format32bppArgb);
            preview.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
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
            $"Удалить «How to Vibe Coder» из папки:\n{appDir}\nи убрать ярлыки?",
            "Удаление игры",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

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
                "Игра «How to Vibe Coder» удалена.",
                "Удаление завершено",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось удалить игру: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    public static Bitmap? LoadInstallerCoverBitmap()
    {
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using Stream? s = asm.GetManifestResourceStream("HowToVibeCoder.InstallerCover.png");
            if (s != null)
            {
                using Image img = Image.FromStream(s);
                return new Bitmap(img);
            }

            string[] candidatePaths =
            {
                Path.Combine(AppContext.BaseDirectory, "installer_cover_editorial.png"),
                @"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\Installer\installer_cover_editorial.png"
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
        bw.Write((ushort)0);
        bw.Write((ushort)1);
        bw.Write((ushort)count);

        uint offset = (uint)(6 + 16 * count);
        for (int i = 0; i < count; i++)
        {
            int sz = IcoFrameSizes[i];
            byte dimByte = sz >= 256 ? (byte)0 : (byte)sz;
            byte[] bytes = pngFrames[i];

            bw.Write(dimByte);
            bw.Write(dimByte);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write((uint)bytes.Length);
            bw.Write(offset);
            offset += (uint)bytes.Length;
        }

        for (int i = 0; i < count; i++)
        {
            bw.Write(pngFrames[i]);
        }

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
        using SolidBrush bg = new(Color.FromArgb(18, 20, 24));
        g.FillRectangle(bg, 0, 0, size, size);
        using SolidBrush accent = new(Color.FromArgb(168, 30, 50));
        g.FillRectangle(accent, size / 6, size / 6, size * 2 / 3, size * 2 / 3);
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

        progressCallback?.Invoke(6, "Подготовка папки…");
        string icoPath = Path.Combine(installDir, "HowToVibeCoder.ico");
        using (Stream icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("HowToVibeCoder.Icon.ico")
            ?? throw new InvalidDataException("Installer icon resource is missing."))
        using (FileStream output = File.Create(icoPath)) icon.CopyTo(output);

        Assembly asm = Assembly.GetExecutingAssembly();
        using Stream? payloadStream = asm.GetManifestResourceStream("HowToVibeCoder.Payload.zip");

        if (payloadStream != null)
        {
            using ZipArchive archive = new(payloadStream, ZipArchiveMode.Read);
            int total = Math.Max(1, archive.Entries.Count);
            int current = 0;
            int lastPct = 6;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                current++;
                int pct = 10 + (int)((current / (float)total) * 76f);
                string destPath = Path.GetFullPath(Path.Combine(installDir, entry.FullName));

                if (!destPath.StartsWith(Path.TrimEndingDirectorySeparator(installDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Invalid payload entry: " + entry.FullName);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                if (pct != lastPct)
                {
                    lastPct = pct;
                    progressCallback?.Invoke(pct, "Распаковка файлов игры…");
                }
                entry.ExtractToFile(destPath, overwrite: true);
            }
        }
        else
        {
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
                    int pct = 10 + (int)(((i + 1) / (float)Math.Max(1, files.Length)) * 76f);
                    progressCallback?.Invoke(pct, "Копирование файлов игры…");
                    File.Copy(files[i], dest, overwrite: true);
                }
            }
        }

        progressCallback?.Invoke(90, "Настройка удаления…");
        CreateUninstallerFiles(installDir);

        string exePath = Path.Combine(installDir, "HowToVibeCoder.exe");

        if (createDesktopShortcut)
        {
            progressCallback?.Invoke(94, "Создание ярлыка на рабочем столе…");
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string lnkPath = Path.Combine(desktopDir, "How to Vibe Coder — Этаж 1.lnk");
            CreateWindowsShortcut(lnkPath, exePath, installDir, icoPath, "How to Vibe Coder — Этаж 1");
        }

        if (createStartMenuFolder)
        {
            progressCallback?.Invoke(98, "Добавление в меню «Пуск»…");
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
                "Удалить How to Vibe Coder");
        }

        progressCallback?.Invoke(100, "Установка завершена");

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
            "if exist \"%~dp0Uninstall.exe\" (\r\n" +
            "    start \"\" \"%~dp0Uninstall.exe\" --uninstall\r\n" +
            ") else (\r\n" +
            "    del /q \"%USERPROFILE%\\Desktop\\How to Vibe Coder — Этаж 1.lnk\" 2>nul\r\n" +
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

internal static class UiDraw
{
    public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        GraphicsPath path = new();
        float d = Math.Max(0.5f, radius * 2f);
        if (d > bounds.Width) d = bounds.Width;
        if (d > bounds.Height) d = bounds.Height;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class CleanButton : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = Color.FromArgb(26, 30, 37);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = Color.FromArgb(37, 42, 52);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color PressedColor { get; set; } = Color.FromArgb(21, 24, 30);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DisabledFillColor { get; set; } = Color.FromArgb(24, 27, 33);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.FromArgb(46, 52, 64);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DisabledForeColor { get; set; } = Color.FromArgb(100, 108, 122);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float CornerRadius { get; set; } = 5f;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool DrawBorder { get; set; } = true;

    private bool _hovered;
    private bool _pressed;

    public CleanButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
        ForeColor = Color.FromArgb(230, 234, 240);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && Enabled)
        {
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_pressed)
        {
            _pressed = false;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (SolidBrush parentBg = new(Parent?.BackColor ?? Color.FromArgb(18, 20, 24)))
        {
            g.FillRectangle(parentBg, ClientRectangle);
        }

        Color bg = !Enabled ? DisabledFillColor : _pressed ? PressedColor : _hovered ? HoverColor : FillColor;
        RectangleF r = new(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

        using GraphicsPath path = UiDraw.RoundedRect(r, CornerRadius);
        using (SolidBrush fill = new(bg))
        {
            g.FillPath(fill, path);
        }

        if (DrawBorder)
        {
            Color bCol = Enabled ? BorderColor : Color.FromArgb(34, 38, 46);
            using Pen pen = new(bCol, 1f);
            g.DrawPath(pen, path);
        }

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            ClientRectangle,
            Enabled ? ForeColor : DisabledForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

internal sealed class CleanCheckBox : Control
{
    private bool _checked = true;
    private bool _hovered;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                Invalidate();
            }
        }
    }

    public CleanCheckBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
        ForeColor = Color.FromArgb(212, 218, 228);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        Size = new Size(420, 26);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (Enabled)
        {
            Checked = !Checked;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
        {
            Checked = !Checked;
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (SolidBrush parentBg = new(Parent?.BackColor ?? Color.FromArgb(18, 20, 24)))
        {
            g.FillRectangle(parentBg, ClientRectangle);
        }

        const int boxSize = 17;
        int boxY = (Height - boxSize) / 2;
        RectangleF boxRect = new(0.5f, boxY + 0.5f, boxSize - 1f, boxSize - 1f);

        using GraphicsPath boxPath = UiDraw.RoundedRect(boxRect, 4f);
        if (_checked)
        {
            Color fillCol = !Enabled
                ? Color.FromArgb(88, 38, 46)
                : _hovered ? Color.FromArgb(190, 38, 60) : Color.FromArgb(168, 30, 50);
            using SolidBrush fill = new(fillCol);
            g.FillPath(fill, boxPath);

            using Pen tick = new(Enabled ? Color.White : Color.FromArgb(190, 195, 205), 1.85f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            PointF[] pts =
            {
                new(4.2f, boxY + 8.9f),
                new(7.4f, boxY + 12.1f),
                new(12.9f, boxY + 5.6f)
            };
            g.DrawLines(tick, pts);
        }
        else
        {
            using SolidBrush fill = new(Color.FromArgb(24, 27, 34));
            g.FillPath(fill, boxPath);
            Color borderCol = !Enabled
                ? Color.FromArgb(40, 45, 54)
                : _hovered ? Color.FromArgb(105, 114, 130) : Color.FromArgb(62, 69, 82);
            using Pen pen = new(borderCol, 1.1f);
            g.DrawPath(pen, boxPath);
        }

        Rectangle textRect = new(boxSize + 11, 0, Width - (boxSize + 11), Height);
        Color textCol = Enabled ? (_hovered ? Color.FromArgb(240, 243, 248) : ForeColor) : Color.FromArgb(105, 112, 124);
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            textRect,
            textCol,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

internal sealed class WindowChromeButton : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsClose { get; init; }
    private bool _hovered;

    public WindowChromeButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Size = new Size(40, 32);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color bg = _hovered
            ? (IsClose ? Color.FromArgb(184, 38, 52) : Color.FromArgb(32, 36, 44))
            : (Parent?.BackColor ?? Color.FromArgb(18, 20, 24));

        using (SolidBrush fill = new(bg))
        {
            g.FillRectangle(fill, ClientRectangle);
        }

        Color fg = _hovered ? Color.White : Color.FromArgb(132, 141, 156);
        using Pen pen = new(fg, 1.35f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        float cx = Width / 2f;
        float cy = Height / 2f;
        if (IsClose)
        {
            const float r = 4.6f;
            g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
            g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
        }
        else
        {
            g.DrawLine(pen, cx - 5f, cy + 0.5f, cx + 5f, cy + 0.5f);
        }
    }
}

public sealed class SetupMainForm : Form
{
    private const int LeftPanelWidth = 268;
    private const int ContentX = 304;
    private const int ContentW = 420;
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly Panel _pathFrame;
    private readonly TextBox _pathTextBox;
    private readonly CleanButton _browseButton;
    private readonly Label _diskSpaceLabel;
    private readonly CleanCheckBox _chkDesktopShortcut;
    private readonly CleanCheckBox _chkStartMenu;
    private readonly CleanCheckBox _chkLaunchGame;
    private readonly CleanButton _installButton;
    private readonly CleanButton _cancelButton;
    private readonly WindowChromeButton _minButton;
    private readonly WindowChromeButton _closeButton;
    private readonly Bitmap? _coverBitmap;

    private bool _pathFocused;
    private bool _isInstalling;
    private bool _installFinished;
    private bool _showProgressArea;
    private int _progressPercent;
    private string _statusMessage = "";
    private string _installedExePath = "";

    private static readonly Color BgRight = Color.FromArgb(20, 18, 17);
    private static readonly Color SurfaceInput = Color.FromArgb(29, 26, 24);
    private static readonly Color BorderSubtle = Color.FromArgb(52, 47, 43);
    private static readonly Color BorderFocus = Color.FromArgb(110, 100, 92);
    private static readonly Color AccentCrimson = Color.FromArgb(168, 30, 50);
    private static readonly Color AccentCrimsonHover = Color.FromArgb(192, 37, 60);
    private static readonly Color AccentCrimsonPressed = Color.FromArgb(142, 24, 41);
    private static readonly Color AccentSuccess = Color.FromArgb(40, 128, 80);
    private static readonly Color AccentSuccessHover = Color.FromArgb(48, 148, 93);

    public SetupMainForm()
    {
        Text = "Установка — How to Vibe Coder";
        ClientSize = new Size(760, 450);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgRight;
        ForeColor = Color.FromArgb(242, 244, 247);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        DoubleBuffered = true;

        try
        {
            Icon = IconGenerator.CreateMemoryIcon();
            _coverBitmap = IconGenerator.LoadInstallerCoverBitmap();
        }
        catch
        {
        }

        // Window controls (top right)
        _closeButton = new WindowChromeButton
        {
            IsClose = true,
            Location = new Point(ClientSize.Width - 41, 1)
        };
        _closeButton.Click += (_, _) => TryCloseForm();
        Controls.Add(_closeButton);

        _minButton = new WindowChromeButton
        {
            IsClose = false,
            Location = new Point(ClientSize.Width - 81, 1)
        };
        _minButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        Controls.Add(_minButton);

        // Input box container
        _pathFrame = new Panel
        {
            Location = new Point(ContentX, 134),
            Size = new Size(326, 36),
            BackColor = SurfaceInput
        };
        _pathFrame.Paint += OnPathFramePaint;
        Controls.Add(_pathFrame);

        _pathTextBox = new TextBox
        {
            Text = @"C:\Games\HowToVibeCoder",
            BorderStyle = BorderStyle.None,
            Location = new Point(11, 9),
            Size = new Size(304, 19),
            Font = new Font("Segoe UI", 9.75f, FontStyle.Regular),
            BackColor = SurfaceInput,
            ForeColor = Color.FromArgb(234, 238, 244)
        };
        _pathTextBox.GotFocus += (_, _) => { _pathFocused = true; _pathFrame.Invalidate(); };
        _pathTextBox.LostFocus += (_, _) => { _pathFocused = false; _pathFrame.Invalidate(); };
        _pathTextBox.TextChanged += (_, _) => UpdateDiskSpaceInfo();
        _pathFrame.Controls.Add(_pathTextBox);

        _browseButton = new CleanButton
        {
            Text = "Обзор",
            Location = new Point(ContentX + 334, 134),
            Size = new Size(86, 36),
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular),
            FillColor = Color.FromArgb(32, 29, 27),
            HoverColor = Color.FromArgb(44, 40, 37),
            PressedColor = Color.FromArgb(25, 22, 21),
            BorderColor = Color.FromArgb(62, 56, 51),
            ForeColor = Color.FromArgb(232, 227, 218)
        };
        _browseButton.Click += OnBrowseClicked;
        Controls.Add(_browseButton);

        _diskSpaceLabel = new Label
        {
            Location = new Point(ContentX, 176),
            Size = new Size(ContentW, 18),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(132, 124, 116),
            BackColor = BgRight
        };
        Controls.Add(_diskSpaceLabel);

        // Options
        _chkDesktopShortcut = new CleanCheckBox
        {
            Text = "Создать ярлык на рабочем столе",
            Location = new Point(ContentX, 212),
            Checked = true
        };
        Controls.Add(_chkDesktopShortcut);

        _chkStartMenu = new CleanCheckBox
        {
            Text = "Добавить ярлык в меню «Пуск»",
            Location = new Point(ContentX, 244),
            Checked = true
        };
        Controls.Add(_chkStartMenu);

        _chkLaunchGame = new CleanCheckBox
        {
            Text = "Запустить игру после установки",
            Location = new Point(ContentX, 276),
            Checked = true
        };
        Controls.Add(_chkLaunchGame);

        // Footer action buttons
        _cancelButton = new CleanButton
        {
            Text = "Отмена",
            Location = new Point(ContentX + ContentW - 104 - 172 - 10, 384),
            Size = new Size(104, 38),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            FillColor = Color.FromArgb(29, 26, 24),
            HoverColor = Color.FromArgb(41, 37, 34),
            PressedColor = Color.FromArgb(23, 21, 19),
            BorderColor = Color.FromArgb(56, 50, 46),
            ForeColor = Color.FromArgb(206, 200, 192)
        };
        _cancelButton.Click += (_, _) => TryCloseForm();
        Controls.Add(_cancelButton);

        _installButton = new CleanButton
        {
            Text = "Установить",
            Location = new Point(ContentX + ContentW - 172, 384),
            Size = new Size(172, 38),
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            FillColor = AccentCrimson,
            HoverColor = AccentCrimsonHover,
            PressedColor = AccentCrimsonPressed,
            DrawBorder = false,
            ForeColor = Color.White
        };
        _installButton.Click += OnInstallClicked;
        Controls.Add(_installButton);

        UpdateDiskSpaceInfo();
    }

    public void ApplyPreviewProgressState(int percent, string message)
    {
        _showProgressArea = true;
        _isInstalling = true;
        _progressPercent = Math.Clamp(percent, 0, 100);
        _statusMessage = message;
        _installButton.Enabled = false;
        _installButton.Text = "Установка…";
        _browseButton.Enabled = false;
        _pathTextBox.ReadOnly = true;
        _chkDesktopShortcut.Enabled = false;
        _chkStartMenu.Enabled = false;
        _chkLaunchGame.Enabled = false;
        Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int pref = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        }
        catch
        {
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
        }
    }

    private void TryCloseForm()
    {
        if (_isInstalling && !_installFinished)
            return;
        Close();
    }

    private void UpdateDiskSpaceInfo()
    {
        string raw = _pathTextBox.Text.Trim();
        try
        {
            string? root = Path.GetPathRoot(raw);
            if (!string.IsNullOrEmpty(root))
            {
                DriveInfo drive = new(root);
                if (drive.IsReady)
                {
                    double freeGb = drive.AvailableFreeSpace / (1024d * 1024d * 1024d);
                    string driveName = root.TrimEnd('\\', ':');
                    _diskSpaceLabel.ForeColor = freeGb < 1.0 ? Color.FromArgb(214, 148, 72) : Color.FromArgb(132, 124, 116);
                    _diskSpaceLabel.Text = $"Свободно на диске {driveName}: {freeGb:0.0} ГБ";
                    return;
                }
            }
        }
        catch
        {
        }

        _diskSpaceLabel.ForeColor = Color.FromArgb(110, 118, 132);
        _diskSpaceLabel.Text = "Выберите существующий диск и папку";
    }

    private void OnPathFramePaint(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        RectangleF r = new(0.5f, 0.5f, _pathFrame.Width - 1.5f, _pathFrame.Height - 1.5f);
        using GraphicsPath path = UiDraw.RoundedRect(r, 5f);
        using Pen pen = new(_pathFocused ? BorderFocus : BorderSubtle, 1f);
        g.DrawPath(pen, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // 1. Left visual key-art panel
        Rectangle leftRect = new(0, 0, LeftPanelWidth, ClientSize.Height);
        if (_coverBitmap != null)
        {
            float scale = Math.Max(
                leftRect.Width / (float)_coverBitmap.Width,
                leftRect.Height / (float)_coverBitmap.Height);
            int srcW = Math.Min(_coverBitmap.Width, (int)Math.Round(leftRect.Width / scale));
            int srcH = Math.Min(_coverBitmap.Height, (int)Math.Round(leftRect.Height / scale));
            int srcX = Math.Max(0, (_coverBitmap.Width - srcW) / 2);
            int srcY = Math.Max(0, (_coverBitmap.Height - srcH) / 2);
            g.DrawImage(_coverBitmap, leftRect, new Rectangle(srcX, srcY, srcW, srcH), GraphicsUnit.Pixel);
        }
        else
        {
            using LinearGradientBrush fallback = new(
                leftRect,
                Color.FromArgb(46, 35, 32),
                Color.FromArgb(14, 16, 20),
                LinearGradientMode.Vertical);
            g.FillRectangle(fallback, leftRect);
        }

        // Clean bottom scrim for title contrast
        Rectangle scrimRect = new(0, ClientSize.Height - 190, LeftPanelWidth, 190);
        using (LinearGradientBrush scrim = new(
            scrimRect,
            Color.FromArgb(0, 12, 14, 18),
            Color.FromArgb(245, 12, 14, 18),
            LinearGradientMode.Vertical))
        {
            g.FillRectangle(scrim, scrimRect);
        }

        // Left cover typography (anti-aliased cleanly over image scrim)
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using (Font titleFont = new("Segoe UI", 18.5f, FontStyle.Bold))
        using (Font subFont = new("Segoe UI", 9.25f, FontStyle.Regular))
        using (SolidBrush titleBrush = new(Color.FromArgb(245, 247, 250)))
        using (SolidBrush subBrush = new(Color.FromArgb(174, 182, 194)))
        using (SolidBrush barBrush = new(Color.FromArgb(212, 175, 99)))
        {
            g.DrawString("HOW TO", titleFont, titleBrush, 22f, ClientSize.Height - 118f);
            g.DrawString("VIBE CODER", titleFont, titleBrush, 22f, ClientSize.Height - 90f);
            g.FillRectangle(barBrush, 24f, ClientSize.Height - 52f, 26f, 2f);
            g.DrawString("Этаж 1 · Белая Акула", subFont, subBrush, 23f, ClientSize.Height - 40f);
        }

        // Vertical hairline divider between art and workspace
        using (Pen divPen = new(Color.FromArgb(34, 38, 46), 1f))
        {
            g.DrawLine(divPen, LeftPanelWidth, 0, LeftPanelWidth, ClientSize.Height);
        }

        // 2. Right panel typography
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (Font headingFont = new("Segoe UI Semibold", 16f, FontStyle.Bold))
        using (Font metaFont = new("Segoe UI", 9f, FontStyle.Regular))
        using (Font labelFont = new("Segoe UI", 9f, FontStyle.Regular))
        using (SolidBrush headingBrush = new(Color.FromArgb(243, 245, 248)))
        using (SolidBrush metaBrush = new(Color.FromArgb(124, 132, 146)))
        using (SolidBrush labelBrush = new(Color.FromArgb(156, 165, 180)))
        {
            g.DrawString("Установка игры", headingFont, headingBrush, ContentX - 2f, 38f);
            g.DrawString("Версия 1.0  ·  Требуется ~420 МБ", metaFont, metaBrush, ContentX, 72f);
            g.DrawString("Папка установки", labelFont, labelBrush, ContentX, 111f);
        }

        // 3. Progress area (visible only while installing or completed)
        if (_showProgressArea)
        {
            using Font statusFont = new("Segoe UI", 9f, FontStyle.Regular);
            using Font pctFont = new("Segoe UI Semibold", 9f, FontStyle.Bold);
            using SolidBrush statusBrush = new(_installFinished ? Color.FromArgb(116, 204, 152) : Color.FromArgb(196, 203, 214));
            using SolidBrush pctBrush = new(Color.FromArgb(230, 234, 240));

            g.DrawString(_statusMessage, statusFont, statusBrush, ContentX, 322f);

            string pctStr = $"{_progressPercent}%";
            SizeF pctSize = g.MeasureString(pctStr, pctFont);
            g.DrawString(pctStr, pctFont, pctBrush, ContentX + ContentW - pctSize.Width + 2f, 322f);

            RectangleF track = new(ContentX, 346f, ContentW, 4f);
            using (GraphicsPath trackPath = UiDraw.RoundedRect(track, 2f))
            using (SolidBrush trackFill = new(Color.FromArgb(34, 38, 47)))
            {
                g.FillPath(trackFill, trackPath);
            }

            float fillW = ContentW * (_progressPercent / 100f);
            if (fillW > 2f)
            {
                RectangleF fillRect = new(ContentX, 346f, fillW, 4f);
                using GraphicsPath fillPath = UiDraw.RoundedRect(fillRect, 2f);
                using SolidBrush fillBrush = new(_installFinished ? AccentSuccess : AccentCrimson);
                g.FillPath(fillBrush, fillPath);
            }
        }

        // Subtle footer divider
        using (Pen footerDiv = new(Color.FromArgb(30, 34, 42), 1f))
        {
            g.DrawLine(footerDiv, ContentX, 366, ContentX + ContentW, 366);
        }

        // Clean outer 1px window border
        using (Pen outerBorder = new(Color.FromArgb(40, 45, 55), 1f))
        {
            g.DrawRectangle(outerBorder, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
    }

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        if (_isInstalling) return;
        using FolderBrowserDialog dlg = new()
        {
            Description = "Выберите папку для установки",
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
            if (!_chkLaunchGame.Checked && !string.IsNullOrEmpty(_installedExePath) && File.Exists(_installedExePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _installedExePath,
                        WorkingDirectory = Path.GetDirectoryName(_installedExePath)!,
                        UseShellExecute = true
                    });
                }
                catch
                {
                }
            }
            Close();
            return;
        }

        if (_isInstalling)
            return;

        string targetDir = _pathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show("Укажите папку для установки.", "Установка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isInstalling = true;
        _showProgressArea = true;
        _progressPercent = 2;
        _statusMessage = "Подготовка…";
        _installButton.Enabled = false;
        _installButton.Text = "Установка…";
        _cancelButton.Enabled = false;
        _browseButton.Enabled = false;
        _pathTextBox.ReadOnly = true;
        _chkDesktopShortcut.Enabled = false;
        _chkStartMenu.Enabled = false;
        _chkLaunchGame.Enabled = false;
        Invalidate();

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
                                _statusMessage = msg;
                                Invalidate(new Rectangle(ContentX - 4, 316, ContentW + 8, 44));
                            }));
                        }
                    });
            });

            _progressPercent = 100;
            _statusMessage = "Установка завершена";
            _installFinished = true;
            _installedExePath = Path.Combine(Path.GetFullPath(targetDir), "HowToVibeCoder.exe");
            _installButton.Text = launchAfter ? "Готово" : "Запустить игру";
            _installButton.FillColor = AccentSuccess;
            _installButton.HoverColor = AccentSuccessHover;
            _installButton.PressedColor = Color.FromArgb(32, 106, 66);
            _installButton.Enabled = true;
            _cancelButton.Text = "Закрыть";
            _cancelButton.Enabled = true;
            Invalidate();
        }
        catch (Exception ex)
        {
            _statusMessage = "Ошибка установки";
            _isInstalling = false;
            _installButton.Enabled = true;
            _installButton.Text = "Установить";
            _cancelButton.Enabled = true;
            _browseButton.Enabled = true;
            _pathTextBox.ReadOnly = false;
            _chkDesktopShortcut.Enabled = true;
            _chkStartMenu.Enabled = true;
            _chkLaunchGame.Enabled = true;
            Invalidate();
            MessageBox.Show($"Не удалось установить игру:\n{ex.Message}", "Ошибка установки", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _coverBitmap?.Dispose();
        }
        base.Dispose(disposing);
    }
}
