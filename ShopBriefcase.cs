using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private static Texture2D shopBackdrop;
    private static readonly Rectangle ShopPurchase=new(306,552,262,52);
    private static readonly Color shopInk=new(36,23,19,255), shopGold=new(224,184,96,255),
        shopCream=new(246,239,222,255), shopRed=new(172,32,32,225);
    private static readonly string[] catalogNames=["Сачок-ловушка","Ремень 90-х","Паяльник 90-х","Микро-Узи 90-х","Обрез 12-го калибра","Ключ-карта СБ"];
    private static readonly Rectangle[] iconCrops=[new(13,7,230,242),new(7,72,242,112),
        new(7,10,242,236),new(21,7,214,242),new(7,82,242,92)];

    private static void ShopText(string text,float x,float y,int size,Color ink,bool center=true,bool display=true,bool shadow=false)
    {
        var font=GameFonts.Get(display ? Typeface.SerifBold : Typeface.Interface,size);
        if(center) x-=GameFonts.Measure(font,text,size).X/2;
        if(shadow) GameFonts.Draw(font,text,new(x+2,y+2),size,shopInk,false);
        GameFonts.Draw(font,text,new(x,y),size,ink,false);
    }

    private static void PaperSlip(Rectangle r,Color stock,Color edge)
    {
        Raylib.DrawRectangleRounded(new(r.X+2,r.Y+3,r.Width,r.Height),.055f,8,new(19,8,6,156));
        Raylib.DrawRectangleRounded(r,.055f,8,stock);
        Raylib.DrawRectangleGradientV((int)r.X+3,(int)r.Y+2,(int)r.Width-6,(int)r.Height-4,
            new Color(255,251,230,50),new Color(179,133,70,27));
        Raylib.DrawRectangleRoundedLinesEx(r,.055f,8,1,edge);
    }

    private static bool ShopOwned(int index) => sidegrades ? Floor.Has((Sidegrade)index) :
        index==5 ? _world.HasElevatorKey : Floor.HasWeapon(FloorSystems.WeaponOrder[index]);

    private static void InkStamp(string text,float x,float y,int size)
    {
        var font=GameFonts.Get(Typeface.SerifBold,size);
        float width=GameFonts.Measure(font,text,size).X+12, height=size+8;
        Rlgl.PushMatrix();
        Rlgl.Translatef(x,y,0);
        Rlgl.Rotatef(-12,0,0,1);
        Raylib.DrawRectangleLinesEx(new(-width/2,-height/2,width,height),2,shopRed);
        Raylib.DrawRectangleLinesEx(new(-width/2+3,-height/2+3,width-6,height-6),.7f,shopRed);
        Raylib.DrawTextPro(font,text,Vector2.Zero,new((width-12)/2,size/2f),0,size,size<=16 ? .5f : 0,shopRed);
        Rlgl.PopMatrix();
    }

    private static void CatalogBadge(float x,float y)
    {
        Raylib.DrawRectangleRounded(new(x-53,y-31,106,62),.14f,8,new(67,54,51,255));
        Raylib.DrawRectangleRounded(new(x-51,y-29,102,58),.12f,8,new(208,202,182,255));
        Raylib.DrawRectangle((int)x-49,(int)y-24,98,10,new(126,31,40,255));
        Raylib.DrawRectangle((int)x-44,(int)y-9,26,28,shopInk);
        Raylib.DrawCircleV(new(x-31,y-1),6,shopCream);
        Raylib.DrawEllipse((int)x-31,(int)y+13,9,6,shopCream);
        ShopText("ПРОПУСК СБ",x+15,y-11,12,shopInk);
        Raylib.DrawRectangle((int)x-7,(int)y+7,16,10,new(176,136,65,255));
        for(int i=0;i<10;i++) Raylib.DrawRectangle((int)x+15+i*3,(int)y+8,1+i%2,9,shopInk);
    }

    private static void DrawCatalogCard(int i,Gear gear)
    {
        Rectangle r=FloorRowRect(i);
        bool selected=floorRow==i;
        float cx=r.X+r.Width/2;
        if(selected) Raylib.DrawRectangleRounded(new(r.X-4,r.Y-4,r.Width+8,r.Height+8),.08f,8,new(221,169,82,83));
        PaperSlip(r,selected ? shopCream : new(235,226,208,255),new(166,143,103,255));
        if(selected) Raylib.DrawRectangleRoundedLinesEx(r,.06f,8,3,shopGold);
        if(!sidegrades)
        {
            if(i==5) CatalogBadge(cx,r.Y+42);
            else
            {
                Texture2D icon=_slotIcons[(i+4)%5];
                Rectangle crop=iconCrops[i];
                float k=Math.Min(148/crop.Width,73/crop.Height);
                Raylib.DrawTexturePro(icon,crop,new(cx,r.Y+43,crop.Width*k,crop.Height*k),
                    new(crop.Width*k/2,crop.Height*k/2),0,Color.White);
            }
        }
        ShopText((sidegrades ? gear.Title : catalogNames[i]).ToUpperInvariant(),cx,r.Y+(sidegrades ? 6 : 81),sidegrades ? 21 : 24,shopInk);
        bool owned=ShopOwned(i);
        ShopText($"${Floor.Price(gear)}",owned && sidegrades ? cx-44 : cx,r.Y+(sidegrades ? 34 : 108),26,shopInk);
        if(owned) InkStamp(sidegrades ? "КУПЛЕНО" : "В АРСЕНАЛЕ",r.X+(sidegrades ? 139 : 143),r.Y+(sidegrades ? 47 : 66),sidegrades ? 13 : 15);
    }

    private static void ShopMeters()
    {
        bool combat=!sidegrades && floorRow<5;
        // Base damage per trigger, cyclic rate, and damage against the elevator shield.
        ReadOnlySpan<float> damage=[12,24,42,18,120];
        ReadOnlySpan<float> intervals=[.28f,.30f,.34f,.10f,.42f];
        ReadOnlySpan<Vector3> ratings=[new(.65f,.8f,.45f),new(.95f,.7f,.8f),new(.65f,.8f,.4f),
            new(.85f,.65f,.7f),new(.75f,.9f,.85f),new(.85f,.5f,.95f),new(.8f,.55f,.65f),
            new(.7f,.9f,.45f),new(.6f,.95f,1),new(.7f,.85f,.6f),new(.65f,.95f,.5f)];
        Vector3 values=combat ? new(damage[floorRow]/120,.10f/intervals[floorRow],
            floorRow==3 ? 1 : damage[floorRow]*.35f/60) : sidegrades ? ratings[floorRow] : new(1,1,.85f);
        for(int i=0;i<3;i++)
        {
            int x=128+i*186;
            string label=i switch {0=>combat ? "УРОН" : "ЭФФЕКТ",1=>combat ? "ТЕМП" : "НАДЁЖНОСТЬ",_=>combat ? "ПРОБИТИЕ" : "ПОНТ"};
            ShopText(label,x,444,19,amber,false,true,true);
            Rectangle bar=new(x,469,160,14);
            Raylib.DrawRectangleRounded(bar,.6f,8,new(28,10,14,220));
            Raylib.DrawRectangleRoundedLinesEx(bar,.6f,8,1,new(188,152,88,220));
            Raylib.DrawRectangleGradientV(x+4,472,(int)(152*values[i]),8,new(252,228,163,255),new Color(174,126,59,255));
        }
    }

    private static void DrawBriefcase()
    {
        var goods=sidegrades ? FloorSystems.GearList : FloorSystems.Arsenal;
        Gear item=goods[floorRow];
        string model=sidegrades ? UpgradeModels[floorRow] : floorRow==5 ? "keycard" : FloorSystems.WeaponModels[floorRow];
        float t=(float)(Raylib.GetTime()-shopOpenedAt);
        var preview=LoreAndMenusUI.RenderShopExhibit(model,model=="keycard" ? t*28 : 28*MathF.Sin(t*.37f+.42f),PanelScale*2);
        Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),new(6,4,6,175));
        Raylib.BeginMode2D(new Camera2D { Zoom=PanelScale,Offset=PanelOffset });
        Raylib.DrawTexturePro(shopBackdrop,new(0,0,shopBackdrop.Width,shopBackdrop.Height),new(0,0,1280,720),Vector2.Zero,0,Color.White);
        ShopText("АРСЕНАЛ ВЕРОНИКИ",76,15,54,new(235,206,148,255),false,true,true);
        for(int i=0;i<2;i++)
        {
            var r=ArsenalTab(i);
            bool active=sidegrades==(i==1);
            var stock=active ? new Color(238,224,194,255) : new Color(148,122,86,215);
            PaperSlip(r,stock,new(167,138,94,225));
            ShopText(i==0 ? "ОРУЖИЕ" : "АПГРЕЙДЫ",r.X+r.Width/2,38,32,active ? shopInk : new(52,36,24,255));
        }
        ShopText("Приманка: "+SkyscraperFloor1World.GetBaitInfo(_world.SelectedBaitTier).Name,935,40,15,amber,true,false);
        PaperSlip(new(1044,24,156,48),new(236,226,206,255),new(165,148,120,255));
        for(int i=3;i<=6;i+=3) Raylib.DrawRectangleLinesEx(new(1044+i,24+i,156-2*i,48-2*i),1,new(165,148,120,255));
        ShopText($"${_playerCash}",1122,25,36,shopInk);
        for(int i=0;i<10;i++) Raylib.DrawEllipse(394,373,166-i*9,12-i*.65f,new(14,3,6,15));
        Raylib.DrawTexturePro(preview,new(0,0,preview.Width,-preview.Height),new(100,138,588,254),Vector2.Zero,0,Color.White);
        ShopText((sidegrades ? item.Title : catalogNames[floorRow]).ToUpperInvariant(),394,389,44,shopCream,true,true,true);
        ShopMeters();
        int y=498;
        foreach(var line in LoreAndMenusUI.Wrap(item.Detail,540,17))
        { ShopText(line,394,y,17,shopCream,true,false,true); y+=21; }
        int price=Floor.Price(item);
        bool owned=ShopOwned(floorRow), available=_playerCash>=price;
        Raylib.DrawLineBezier(new(278,575),new(314,581),1.6f,amber);
        Rlgl.PushMatrix(); Rlgl.Translatef(238,578,0); Rlgl.Rotatef(-9,0,0,1);
        Raylib.DrawRectangle(-50,-21,86,42,shopCream);
        Raylib.DrawTriangle(new(36,-21),new(36,21),new(52,0),shopCream);
        Raylib.DrawCircle(37,0,4.5f,shopGold); Raylib.DrawCircle(37,0,2.2f,shopInk);
        ShopText(owned ? "ОК" : $"${price}",-7,-16,28,shopRed); Rlgl.PopMatrix();
        PaperSlip(ShopPurchase,owned ? new(119,91,52,255) : available ? new(128,28,34,255) : new(72,24,28,220),shopGold);
        Raylib.DrawRectangleRoundedLinesEx(new(310,556,254,44),.12f,8,1,new(204,166,98,255));
        bool shells=sidegrades && floorRow<2;
        string action=owned ? shells ? Floor.Shell==(Sidegrade)floorRow ? "СНЯТЬ  [ENTER]" : "СНАРЯДИТЬ  [ENTER]" : !sidegrades && floorRow<5 && _weapons.CurrentWeapon!=FloorSystems.WeaponOrder[floorRow] ? "СНАРЯДИТЬ  [ENTER]" : "В АРСЕНАЛЕ" :
            available ? "КУПИТЬ  [ENTER]" : $"НУЖНО ${price}";
        ShopText(action,437,556,31,available || owned ? shopCream : amber,true,true,true);
        if(panelMessage.Length>0) ShopText(panelMessage,394,611,14,shopCream,true,false);
        for(int i=0;i<goods.Length;i++) DrawCatalogCard(i,goods[i]);
        int left=58;
        void Control(string key,string label,int w,int advance)
        {
            Raylib.DrawRectangleRoundedLinesEx(new(left,674,w,26),.22f,6,1.5f,amber);
            ShopText(key,left+w/2f,675,17,shopCream);
            ShopText(label,left+w+7,674,18,amber,false);
            left+=advance;
        }
        Control("WASD","Выбор",53,133); Control("ENTER","Купить",57,141);
        Control("B","Приманка",24,127); Control("G","Крыша",24,103); Control("ESC","Выход",38,112);
        Rlgl.PushMatrix(); Rlgl.Translatef(1004,633,0); Rlgl.Rotatef(-2,0,0,1);
        PaperSlip(new(0,0,230,76),new(234,218,178,255),new(165,144,99,255));
        ShopText("«Гарантия — ровно",18,9,19,shopInk,false,false);
        ShopText("до турникета.»",18,30,19,shopInk,false,false);
        ShopText("— Вероника",172,52,16,shopInk);
        Raylib.DrawRectangleRoundedLinesEx(new(196,-9,10,31),.95f,10,2,new(94,77,53,255));
        Raylib.DrawRectangleRoundedLinesEx(new(197,-9,8,29),.95f,10,1,new(230,212,164,255));
        Rlgl.PopMatrix();
        Raylib.EndMode2D();
    }
}
