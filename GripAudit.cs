using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private static void RunGripAudit()
    {
        string output=Path.Combine(Environment.CurrentDirectory,"audit","native-grips");
        Directory.CreateDirectory(output);
        for(int slot=1;slot<=4;slot++)
        {
            ResetNewGameFloor();
            MainMenuAndPauseUI.IsInMainMenu=false;
            _weapons.SelectSlot(slot);
            _weapons.UpdateWeapons(1,BuildPlayerCamera(),false,false);
            foreach(string angle in new[]{"overview","right","palm","left"})
            {
                if(slot==1 && angle=="left") continue;
                Camera3D? camera=null;
                if(angle!="overview")
                {
                    Vector3 target=angle=="left"
                        ? slot==2 ? new(.16f,-.13f,.70f) : new(-.18f,-.10f,.83f)
                        : new(-.18f,-.13f,.62f);
                    Vector3 offset=angle switch {
                        "palm" => new(.32f,.06f,.16f),
                        "left" => new(.40f,.20f,-.22f),
                        _ => new(-.30f,.13f,-.25f)};
                    camera=new Camera3D {Position=target+offset,Target=target,Up=Vector3.UnitY,FovY=40,Projection=CameraProjection.Perspective};
                }
                for(int frame=0;frame<3;frame++)
                {
                    Raylib.BeginDrawing();
                    Raylib.ClearBackground(new Color(38,48,62,255));
                    _weapons.DrawFirstPersonHandsAndWeapon(BuildPlayerCamera(),0,false,_uiFont,camera);
                    Raylib.EndDrawing();
                }
                Image screenshot=Raylib.LoadImageFromScreen();
                string path=Path.Combine(output,$"slot_{slot}_{angle}.png");
                if(!Raylib.ExportImage(screenshot,path)) throw new IOException(path);
                Raylib.UnloadImage(screenshot);
                Console.WriteLine("[GRIP TEST] "+path);
            }
        }
    }
}
