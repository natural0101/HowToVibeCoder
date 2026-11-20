using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private const int AuditAction=40, FinaleAction=43;
    private static readonly string[] AuditTitles=["Подделать ведомость","Сдать одного кодера","Послать аудитора"];
    private static readonly string[] FinaleTitles=["Контейнировать агента","Отпустить агента в сеть"];
    private static Rectangle DecisionRow(int i) => new(164,279+90*i,952,80);

    private static bool HandleDecision()
    {
        var pending=Floor.FinalePending && Floor.ShutdownTime<=0 ? FloorPanel.BossFinale :
            Floor.Audit>0 ? FloorPanel.Audit : FloorPanel.None;
        if(pending==FloorPanel.None)
        {
            if(floorPanel is not (FloorPanel.Audit or FloorPanel.BossFinale)) return false;
            OpenFloorPanel(FloorPanel.None); return true;
        }
        if(pending==FloorPanel.Audit && !Net.Guest) Floor.AuditPresented=true;
        if(floorPanel!=pending) { OpenFloorPanel(pending); return true; }
        bool audit=pending==FloorPanel.Audit;
        Vector2 mouse=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
        for(int i=0;i<(audit ? 3 : 2);i++)
        {
            if(!Raylib.IsKeyPressed(KeyboardKey.One+i) && !Raylib.IsKeyPressed(KeyboardKey.Kp1+i) &&
                !(Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mouse,DecisionRow(i)))) continue;
            panelMessage=FloorAction((audit ? AuditAction : FinaleAction)+i);
            if(audit ? Floor.Audit<=0 : !Floor.FinalePending) { SetHudBanner(panelMessage); OpenFloorPanel(FloorPanel.None); }
            break;
        }
        return true;
    }

    private static void DrawDecision()
    {
        bool audit=floorPanel==FloorPanel.Audit;
        var ink=DossierPrint.Ink; var muted=DossierPrint.Muted; var red=DossierPrint.RedInk;
        DossierPrint.Sheet(new(126,52,1028,615));
        void Print(string s,float x,float y,int size,Color c,Typeface f=Typeface.Interface) => DossierPrint.Text(s,x,y,size,c,f);
        Print("АО «Энтропия»     Первый этаж",164,88,17,muted);
        string stamp=audit ? "ПРОВЕРКА СБ" : "ЭКЗЕМПЛЯР 01";
        float stampWidth=GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold,19),stamp,19).X+26;
        Raylib.DrawRectangleLinesEx(new(1116-stampWidth,79,stampWidth,34),1.5f,red);
        Print(stamp,1129-stampWidth,84,19,red,Typeface.InterfaceBold);
        Print(audit ? "ПРЕДПИСАНИЕ АУДИТА БАШНИ" : "АКТ УТИЛИЗАЦИИ ИИ-АГЕНТА",161,135,42,ink,Typeface.SerifBold);
        Print(audit ? "В штатной ведомости обнаружены расхождения." : "Объект v0.1 остановлен у Северного лифта.",165,204,23,ink);
        Print(audit ? "Выберите способ урегулирования до передачи дела в СБ." :
            "Босс, на втором вас ждёт директор по кадрам. Я уже заказала лифт. С любовью, В.",165,238,18,muted);
        Vector2 mouse=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
        for(int i=0;i<(audit ? 3 : 2);i++)
        {
            var row=DecisionRow(i);
            bool available=!audit || i switch { 0=>Floor.Badge>0 || _playerCash>=180,1=>_world.GalleyDesks.Exists(d=>d.IsOccupied),_=>true };
            var titleColor=available ? ink : muted;
            Raylib.DrawLine((int)row.X,(int)row.Y,(int)(row.X+row.Width),(int)row.Y,DossierPrint.Rule);
            Print($"[{i+1}]",165,row.Y+16,22,red,Typeface.InterfaceBold);
            Print((audit ? AuditTitles : FinaleTitles)[i],227,row.Y+10,26,titleColor,
                available && Raylib.CheckCollisionPointRec(mouse,row) ? Typeface.SerifBold : Typeface.Serif);
            string detail=audit ? i switch {
                0=>(Floor.Badge>0 ? "Списать липовый пропуск" : available ? "Списать $180" : "Недоступно: нужно $180 или пропуск")+"     СБ −25, ТД +12",
                1=>available ? "Один сотрудник покинет Опенспейс. СБ −40" : "Недоступно: в Опенспейсе нет сотрудников",
                _=>"Вызов патруля, СБ +28. Прирост авторитета и множитель жадности +35% на 45 с"
            } : i==0 ? "+$1500. Узел в Опенспейсе приносит $14/с и наращивает техдолг." :
                "+$900. СБ обнуляется. Артель снижает рост риска на 20%. Откроется запись в архиве.";
            Print(detail,227,row.Y+46,17,muted);
        }
        if(panelMessage.Length>0) Print(panelMessage,165,563,17,ink);
        Print(audit ? $"Общак   ${_playerCash}       СБ   {Floor.SEC:F0}" : "После подписи распоряжение вступает в силу окончательно.",165,612,18,muted);
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
