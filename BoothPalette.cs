using System.Security.Cryptography;
using Raylib_cs;

namespace HowToVibeCoder;

internal static unsafe partial class VisualEngine3D
{
    // The first connected-arm export collapsed the Blender material slots on join.
    // Runs follow the source object's material assignments, matched by vertex position.
    // Only these exact exports require repair. Later revisions use their own materials.
    private enum Swatch : uint
    {
        skin=0xF2C2A2,
        lip=0x98203A,
        ivory=0xFAF7F4,
        wax=0xEDE0C6,
        iris=0x9E44FF,
        onyx=0x08050A,
        catchlight=0xFFFFFF,
        eyeshadow=0xB46272,
        hair=0x261412,
        hairlight=0x522E22,
        gold=0xD4A242,
        teal=0x20C494,
        silk=0x4E1432,
        plum=0x380E25,
        trouser=0x1C1520,
        coal=0x141018,
        sole=0x0A080C,
        nail=0x5E0E26,
        orb=0xC888FF,
        flame=0xFFE898,
        card=0xEFE0B8
    }

    private static readonly Dictionary<string,(string Hash,(Swatch Color,int Count)[] Runs)> boothPalette=new()
    {
        ["char_hr_veronika_head"]=("E4B5901BCE5F0086F100F81F551B0A00FEDE1C3D7B366FDFA9BF044AD6254162",[
            (Swatch.skin,17563), (Swatch.lip,628), (Swatch.ivory,830), (Swatch.iris,614),
            (Swatch.onyx,366), (Swatch.catchlight,368), (Swatch.eyeshadow,218), (Swatch.onyx,266),
            (Swatch.skin,64), (Swatch.onyx,42), (Swatch.hair,218), (Swatch.ivory,830),
            (Swatch.iris,614), (Swatch.onyx,366), (Swatch.catchlight,368), (Swatch.eyeshadow,218),
            (Swatch.onyx,266), (Swatch.skin,64), (Swatch.onyx,42), (Swatch.hair,1940),
            (Swatch.hairlight,218), (Swatch.hair,750), (Swatch.hairlight,218), (Swatch.hair,1266),
            (Swatch.hairlight,448), (Swatch.gold,400), (Swatch.teal,288), (Swatch.gold,112)]),
        ["char_hr_veronika_head_blink"]=("4E9BEC9CEE0EDF1DC158231A75FB23F6AE166AD6824617933DF02678DFACCF18",[
            (Swatch.skin,17563), (Swatch.lip,628), (Swatch.eyeshadow,758), (Swatch.onyx,266),
            (Swatch.skin,42), (Swatch.hair,218), (Swatch.eyeshadow,758), (Swatch.onyx,266),
            (Swatch.skin,42), (Swatch.hair,1940), (Swatch.hairlight,218), (Swatch.hair,750),
            (Swatch.hairlight,218), (Swatch.hair,1266), (Swatch.hairlight,448), (Swatch.gold,400),
            (Swatch.teal,288), (Swatch.gold,112)]),
        ["char_hr_veronika_body"]=("6CB577DD3D2DD374056AE49744155E0B720E1E4B57673CFC4E536C4798A68D3C",[
            (Swatch.skin,616), (Swatch.gold,760), (Swatch.silk,1792), (Swatch.plum,580),
            (Swatch.silk,956), (Swatch.hair,410), (Swatch.hairlight,410), (Swatch.hair,820),
            (Swatch.hairlight,410), (Swatch.hair,820), (Swatch.trouser,504), (Swatch.coal,96),
            (Swatch.sole,56), (Swatch.trouser,504), (Swatch.coal,96), (Swatch.sole,56)]),
        ["char_hr_veronika_arm_l"]=("5892B1952CF1BE2DEE87461948EA50E16F2761D126F966D52A44FBAB22F5A529",[
            (Swatch.silk,1666), (Swatch.skin,336), (Swatch.gold,1024), (Swatch.skin,544),
            (Swatch.nail,122), (Swatch.skin,256), (Swatch.nail,122), (Swatch.skin,256),
            (Swatch.nail,122), (Swatch.skin,256), (Swatch.nail,122), (Swatch.skin,224),
            (Swatch.nail,122)]),
        ["char_hr_veronika_arm_r"]=("2498C1C2FFCAB38B6280A2B31258DA67A6D147B5E5B6BF60B17A5EBEC1A3C461",[
            (Swatch.silk,1666), (Swatch.skin,336), (Swatch.gold,1024), (Swatch.skin,544),
            (Swatch.nail,122), (Swatch.skin,256), (Swatch.nail,122), (Swatch.skin,256),
            (Swatch.nail,122), (Swatch.gold,336), (Swatch.skin,256), (Swatch.nail,122),
            (Swatch.skin,224), (Swatch.nail,122)]),
        ["prop_veronika_tarot_table"]=("F8F9EF5A586000689C64E8B39469297FE745F8B8B7EBCBB80E1D20F1D7CFCC77",[
            (Swatch.plum,1152), (Swatch.silk,1090), (Swatch.gold,2056), (Swatch.orb,1490),
            (Swatch.iris,610), (Swatch.flame,362), (Swatch.gold,112), (Swatch.wax,112),
            (Swatch.flame,262), (Swatch.gold,112), (Swatch.wax,112), (Swatch.flame,262),
            (Swatch.gold,112), (Swatch.wax,112), (Swatch.flame,262), (Swatch.gold,112),
            (Swatch.wax,112), (Swatch.flame,262), (Swatch.card,224)]),
    };
    private static readonly HashSet<string> paintedBoothMeshes=new();
    private static int boothColorLoc;

    private static void RestoreBoothPalette(string path,string name,Model model)
    {
        if(model.MeshCount!=1 || !boothPalette.TryGetValue(name,out var palette)) return;
        using var file=File.OpenRead(path);
        if(Convert.ToHexString(SHA256.HashData(file))!=palette.Hash) return;
        Mesh mesh=model.Meshes[0];
        if(mesh.VertexCount!=palette.Runs.Sum(run=>run.Count)) throw new InvalidDataException(name);
        var colors=new Color[mesh.VertexCount];
        // Material base colours in this shader are linear glTF factors.
        static byte Linear(uint channel)
        {
            float value=(channel & 255)/255f;
            return (byte)MathF.Round(255*(value<=.04045f ? value/12.92f : MathF.Pow((value+.055f)/1.055f,2.4f)));
        }
        int start=0;
        foreach(var (swatch,count) in palette.Runs)
        {
            uint rgb=(uint)swatch;
            var color=new Color(Linear(rgb>>16),Linear(rgb>>8),Linear(rgb),(byte)255);
            Array.Fill(colors,color,start,count);
            start+=count;
        }
        Raylib.UpdateMeshBuffer<Color>(mesh,3,colors,0);
        paintedBoothMeshes.Add(name);
    }
}
