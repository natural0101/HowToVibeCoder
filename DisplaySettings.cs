using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

namespace HowToVibeCoder;

[JsonConverter(typeof(JsonStringEnumConverter<DisplayMode>))]
internal enum DisplayMode { Fullscreen, Windowed }

internal sealed record DisplaySettings(DisplayMode DisplayMode=DisplayMode.Fullscreen,int ResolutionIndex=0,string Language="ru")
{
    internal static readonly (int Width,int Height)[] Sizes=[(0,0),(2560,1440),(1920,1080),(1600,900),(1280,720)];
    internal string ResolutionLabel => ResolutionIndex==0 ? "Авто (экран)" : $"{Sizes[ResolutionIndex].Width}×{Sizes[ResolutionIndex].Height}";

    internal static DisplaySettings Load(string path)
    {
        try
        {
            var value=JsonSerializer.Deserialize<DisplaySettings>(File.ReadAllText(path));
            if(value!=null && Enum.IsDefined(value.DisplayMode) && (uint)value.ResolutionIndex<Sizes.Length) return value;
        }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) { }
        return new();
    }

    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temp,JsonSerializer.Serialize(this));
            File.Move(temp,path,true);
        }
        finally { File.Delete(temp); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
    [DllImport("user32")] private static extern nint MonitorFromWindow(nint window,uint flags);
    [DllImport("user32")] private static extern bool GetMonitorInfoW(nint monitor,ref MonitorInfo info);
    [DllImport("user32")] private static extern bool GetWindowRect(nint handle,out Rect rect);
    [DllImport("user32")] private static extern bool GetClientRect(nint handle,out Rect rect);
    [DllImport("user32")] private static extern bool ClientToScreen(nint handle,ref System.Drawing.Point point);

    internal unsafe void Apply()
    {
        int m=Raylib.GetCurrentMonitor();
        var origin=Raylib.GetMonitorPosition(m);
        int left=(int)origin.X,top=(int)origin.Y,width=Raylib.GetMonitorWidth(m),height=Raylib.GetMonitorHeight(m);
        if(DisplayMode==DisplayMode.Fullscreen)
        {
            Raylib.SetWindowState(ConfigFlags.UndecoratedWindow);
            Raylib.SetWindowPosition(left,top);
            Raylib.SetWindowSize(width,height);
            return;
        }

        Raylib.ClearWindowState(ConfigFlags.UndecoratedWindow);
        if(OperatingSystem.IsWindows())
        {
            nint handle=(nint)Raylib.GetWindowHandle();
            var info=new MonitorInfo { Size=Marshal.SizeOf<MonitorInfo>() };
            if(GetMonitorInfoW(MonitorFromWindow(handle,2),ref info))
            {
                left=info.Work.Left; top=info.Work.Top;
                width=info.Work.Right-left; height=info.Work.Bottom-top;
                System.Drawing.Point clientOrigin=new();
                if(GetWindowRect(handle,out var frame) && GetClientRect(handle,out var client) && ClientToScreen(handle,ref clientOrigin))
                {
                    left+=clientOrigin.X-frame.Left; top+=clientOrigin.Y-frame.Top;
                    width-=frame.Right-frame.Left-client.Right; height-=frame.Bottom-frame.Top-client.Bottom;
                }
            }
        }
        var requested=Sizes[ResolutionIndex];
        float scale=ResolutionIndex==0 ? 1 : Math.Min(1,Math.Min((float)width/requested.Width,(float)height/requested.Height));
        int w=ResolutionIndex==0 ? width : (int)(requested.Width*scale);
        int h=ResolutionIndex==0 ? height : (int)(requested.Height*scale);
        Raylib.SetWindowSize(w,h);
        Raylib.SetWindowPosition(left+(width-w)/2,top+(height-h)/2);
    }
}

internal static class UiViewport
{
    internal static float Scale => Math.Min(Raylib.GetScreenWidth()/1280f,Raylib.GetScreenHeight()/720f);
    internal static int Width => (int)MathF.Round(Raylib.GetScreenWidth()/Scale);
    internal static int Height => (int)MathF.Round(Raylib.GetScreenHeight()/Scale);
    internal static Vector2 Pointer => Raylib.GetMousePosition()/Scale;
    internal static Vector2 Project(Vector3 point,Camera3D camera) => Raylib.GetWorldToScreenEx(point,camera,Width,Height);
    internal static void Begin() => Raylib.BeginMode2D(new Camera2D { Zoom=Scale });
}
