using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private const int FinaleAction=43;
    private static readonly string[] FinaleTitles=["Контейнировать агента","Отпустить агента в сеть"];
    private static Rectangle DecisionRow(int i) => new(164,279+90*i,952,80);

    private static bool HandleDecision()
    {
        var pending=Floor.FinalePending && Floor.ShutdownTime<=0 ? FloorPanel.BossFinale :
            FloorPanel.None;
        if(pending==FloorPanel.None)
        {
            if(floorPanel is not (FloorPanel.Audit or FloorPanel.BossFinale)) return false;
            OpenFloorPanel(FloorPanel.None); return true;
        }
        if(floorPanel!=pending) { OpenFloorPanel(pending); return true; }
        Vector2 mouse=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
        for(int i=0;i<FinaleTitles.Length;i++)
        {
            if(!Raylib.IsKeyPressed(KeyboardKey.One+i) && !Raylib.IsKeyPressed(KeyboardKey.Kp1+i) &&
                !(Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mouse,DecisionRow(i)))) continue;
            panelMessage=FloorAction(FinaleAction+i);
            if(!Floor.FinalePending) { SetHudBanner(panelMessage); OpenFloorPanel(FloorPanel.None); }
            break;
        }
        return true;
    }

    private static void DrawDecision()
    {
        var ink=LoreAndMenusUI.ThemeSepiaInk; var muted=LoreAndMenusUI.ThemeSepiaMuted; var red=LoreAndMenusUI.ThemeStampRed;
        LoreAndMenusUI.DrawManilaPaper(new(116,26,1048,668));
        void Print(string s,float x,float y,int size,Color c,Typeface f=Typeface.Interface) => DossierPrint.Text(s,x,y,size,c,f);
        Print("«ЭНТРОПИК»     Первый этаж",178,94,16,muted,Typeface.InterfaceBold);
        string stamp="ЭКЗЕМПЛЯР 01";
        float stampWidth=GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold,17),stamp,17).X+26;
        Rlgl.PushMatrix();
        Rlgl.Translatef(938,108,0);
        Rlgl.Rotatef(-5.5f,0,0,1);
        Raylib.DrawRectangleLinesEx(new(-stampWidth/2,-17,stampWidth,34),2.4f,red);
        Raylib.DrawRectangleLinesEx(new(-stampWidth/2+3,-14,stampWidth-6,28),1f,red);
        Print(stamp,-stampWidth/2+13,-9,17,red,Typeface.InterfaceBold);
        Rlgl.PopMatrix();
        Print("АКТ УТИЛИЗАЦИИ ИИ-АГЕНТА",175,136,40,ink,Typeface.SerifBold);
        Print("Объект v0.1 остановлен у Северного лифта.",178,202,22,ink);
        Print("Босс, на втором вас ждёт директор по кадрам. Я уже заказала лифт. С любовью, В.",178,236,18,muted);
        Vector2 mouse=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
        for(int i=0;i<FinaleTitles.Length;i++)
        {
            var row=DecisionRow(i);
            Raylib.DrawLine((int)row.X,(int)row.Y,(int)(row.X+row.Width),(int)row.Y,LoreAndMenusUI.ThemeSepiaRule);
            Print($"[{i+1}]",178,row.Y+16,22,red,Typeface.InterfaceBold);
            Print(FinaleTitles[i],236,row.Y+10,25,ink,
                Raylib.CheckCollisionPointRec(mouse,row) ? Typeface.SerifBold : Typeface.Serif);
            string detail=i==0 ? "+$1500. Узел в Опенспейсе приносит $14/с и наращивает техдолг." :
                "+$900. СБ обнуляется. Артель снижает рост риска на 20%. Откроется запись в архиве.";
            Print(detail,236,row.Y+46,17,muted);
        }
        Print("P.S. Кассетное ядро v0.1 эвакуировано службой отката наверх — объект ушёл на восстановление.",178,496,16,red,Typeface.InterfaceBold);
        if(panelMessage.Length>0) Print(panelMessage,178,560,17,ink);
        Print("После подписи распоряжение вступает в силу окончательно.",178,608,17,muted);
    }

    private static void DrawBossTelemetry()
    {
        var b=_world.ElevatorBoss;
        float fade=_world.BossHudOpacity;
        if(fade<=0) return;
        int center=UiViewport.Width/2;
        string phase=b.Phase switch {1=>"I",2=>"II",_=>"III"};
        string label=$"ИИ-АГЕНТ v0.1    ФАЗА {phase}    "+(b.Phase==3 ? "SYSTEM ZERO" : b.ShieldHp>0 ? "ЩИТ" : "ЯДРО");
        var face=GameFonts.Get(Typeface.InterfaceBold,17);
        void Center(string s,int y,int size,Color color) =>
            ShadowText(face,s,center-GameFonts.Measure(face,s,size).X/2,y,size,Raylib.Fade(color,fade));
        Center(label,17,17,DossierPrint.Ivory);
        void Meter(int y,float fraction,Color c)
        {
            Raylib.DrawRectangle(center-124,y,248,4,new Color(39,41,44,(int)(180*fade)));
            Raylib.DrawRectangle(center-124,y,(int)(248*Math.Clamp(fraction,0,1)),4,Raylib.Fade(c,fade));
        }
        Meter(43,b.Hp/b.MaxHp,new(199,127,111,255));
        Meter(50,b.ShieldHp/EscapedAgentBoss.ShieldCapacity,new(143,201,216,255));
        if(b.Phase==3) Center(_world.CurrentTape,62,15,DossierPrint.Ivory);
    }
}
