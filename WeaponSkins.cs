#nullable enable
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal sealed partial class FloorSystems
{
    public int[] SkinMasks=new int[7], EquippedSkins=new int[7];
    internal bool HasSkin(WeaponType w,int id) => id==0 || id is >=1 and <=12 && (SkinMasks[(int)w] & (1<<id))!=0;
    internal void EquipSkin(WeaponType w,int id) { if(HasSkin(w,id)) EquippedSkins[(int)w]=id; }
}

internal static class WeaponSkins
{
    internal static readonly string[] Names=["Заводской","Офисный Камуфляж","Кобальт 1995","Терракота Митино","Малиновый Пиджак","Неон Токио-98","Кислотный Баг","Аметист Вероники","Бензиновый Хром","Кровавый Прод","Золотой Олигарх 90-х","Голографический Клодий","Драконий Деплой"];
    internal static readonly string[] Tiers=["Необычный","Редкий","Эпический","Премиум Раритет"];
    internal static readonly Color[] Inks=[new(74,168,255,255),new(178,102,255,255),new(255,77,121,255),new(255,209,92,255)];
    internal static readonly Color[] Swatches=[new(106,103,98,255),new(99,141,129,255),new(50,95,197,255),new(189,91,46,255),new(184,24,56,255),new(246,72,198,255),new(62,222,109,255),new(173,112,220,255),new(52,193,204,255),new(212,34,47,255),new(245,186,62,255),new(146,225,225,255),new(154,97,36,255)];
    internal const string ShaderCode="""
        uniform int weaponSkin;
        float skinHash(vec3 p){return fract(sin(dot(p,vec3(17.1,33.5,57.7)))*4375.23);}
        float skinNoise(vec3 p){
            vec3 i=floor(p), f=fract(p); f=f*f*(3.-2.*f);
            return mix(mix(mix(skinHash(i),skinHash(i+vec3(1,0,0)),f.x),mix(skinHash(i+vec3(0,1,0)),skinHash(i+vec3(1,1,0)),f.x),f.y),
                       mix(mix(skinHash(i+vec3(0,0,1)),skinHash(i+vec3(1,0,1)),f.x),mix(skinHash(i+vec3(0,1,1)),skinHash(i+1.),f.x),f.y),f.z);
        }
        vec3 finishSkin(vec3 p,vec3 n,vec3 v,float time,vec3 original){
            float rim=pow(1.-abs(dot(n,v)),2.), grain=skinNoise(p*16.);
            float phase=dot(p,vec3(7.,13.,9.)), stripe=fract(phase);
            float reflection=dot(reflect(-v,n),normalize(vec3(.31,.81,.45)));
            float shine=pow(1.-abs(reflection),8.);
            vec3 gold=vec3(1.,.63,.14), carbon=vec3(.039,.043,.05), coat=carbon;
            float accent=.23;
            if(weaponSkin==1){
                float tile=skinHash(floor(p*vec3(16.,21.,12.)));
                coat=tile<.28 ? vec3(.81,.78,.61) : tile<.62 ? vec3(.19,.39,.36) : vec3(.17,.22,.26);
            }else if(weaponSkin==2) coat=mix(vec3(.065,.21,.69),vec3(.73,.8,.83),step(.83,stripe))*(.91+.09*cos(p.y*500.));
            else if(weaponSkin==3) coat=mix(carbon,vec3(.78,.29,.12),step(.29,fract(p.z*4.+p.y)));
            else if(weaponSkin==4) coat=mix(vec3(.72,.094,.22),gold, smoothstep(.66,.86,rim));
            else if(weaponSkin==5) coat=stripe<.13 ? vec3(.05,1.,1.) : stripe<.22 ? vec3(1.,.09,.64) : vec3(.057,.052,.18);
            else if(weaponSkin==6) coat=mix(carbon,vec3(.09,.90,.36),step(.60,stripe));
            else if(weaponSkin==7) coat=mix(vec3(.38,.1,.59),vec3(.97,.75,.67),rim*.84+grain*.14);
            else if(weaponSkin==8 || weaponSkin==11){
                float wave=grain*.46+reflection*.38+phase*.026;
                if(weaponSkin==11) wave+=time*.10+.045*sin(p.y*180.+time*6.);
                coat=.5+.48*cos(6.283*(wave+vec3(.04,.38,.68)));
                if(weaponSkin==11) coat=mix(coat,vec3(.97,.9,1.),.29);
            }else if(weaponSkin==9){
                float web=min(abs(stripe-.5),abs(fract(phase-p.y*25.)-.5));
                coat=mix(vec3(.13,.022,.035),vec3(.79,.025,.09),smoothstep(.01,.065,web));
            }else if(weaponSkin==10){
                float etch=abs(fract(phase*.6+.21*sin(p.y*102.))-.5);
                coat=gold*(.44+.56*smoothstep(.012,.048,etch))*(.70+.30*reflection);
                coat+=vec3(1.,.84,.52)*pow(.5+.5*cos(phase+time*2.2),24.)*.52;
            }else if(weaponSkin==12){
                float crack=abs(skinNoise(p*20.)-.5);
                coat=mix(carbon*(.55+.8*grain),gold*(1.15+.28*sin(time*3.+phase)),1.-smoothstep(.015,.082,crack));
            }
            if(weaponSkin>=4) accent=.56;
            if(weaponSkin>=10) accent=.92;
            return coat*(.74+.26*clamp(dot(original,vec3(.3,.5,.2)),0.,1.))+
                   (shine+rim*.31)*accent*mix(coat,vec3(1.,.93,.81),.52);
        }
        """;
}

internal sealed class SlotRoulette
{
    internal const int Winner=35;
    internal readonly (int Row,int Skin)[] Tickets=new (int,int)[40];
    internal bool Active;
    internal float Time;
    internal int Cashback;
    internal bool Reveal=>Time>=3.8f;
    internal float Position=>Winner*(1-MathF.Exp(-5*Math.Min(Time/3.8f,1)))/(1-MathF.Exp(-5));
    internal (int Row,int Skin) Prize=>Tickets[Winner];
    internal void Start(FloorSystems floor,VibeCoderRarity rarity,Random rng,Action<int,string>? pay)
    {
        for(int i=0;i<Tickets.Length;i++) Tickets[i]=(rng.Next(5),rng.Next(1,13));
        int roll=rng.Next(100);
        int tier=rarity switch {
            VibeCoderRarity.Legendary=>roll<32 ? 2 : 3,
            VibeCoderRarity.Epic=>roll<70 ? 2 : 3,
            VibeCoderRarity.Rare=>roll<58 ? 1 : roll<88 ? 2 : 3,
            _=>roll<54 ? 0 : roll<84 ? 1 : roll<96 ? 2 : 3};
        var preferred=Enumerable.Range(0,5).Where(i=>i>=3 || floor.HasWeapon(FloorSystems.WeaponOrder[i])).ToArray();
        int row=rng.Next(2)==0 ? preferred[rng.Next(preferred.Length)] : rng.Next(5);
        int skin=1+3*tier+rng.Next(3);
        Tickets[Winner]=(row,skin);
        var weapon=FloorSystems.WeaponOrder[row];
        Cashback=floor.HasSkin(weapon,skin) ? rng.Next(75,401) : 0;
        if(Cashback>0) pay?.Invoke(Cashback,"Возврат за повтор");
        floor.SkinMasks[(int)weapon]|=1<<skin;
        Time=0; Active=true;
        FloorSystems.SpeakCoaching(SpinLine);
    }
    internal const string SpinLine="Опа, необычный кадр! Включаю режим автомата — крутим рулетку на скин для твоей пушки!";
    internal const string JackpotLine="Джекпот! Премиум-скин выпал — такого во всей башне ни у кого нет!";
}
public static partial class Program
{
    private static readonly Rectangle EquipPrize=new(636,551,248,40), StorePrize=new(898,551,242,40);
    internal static void ShareSlot(SlotRoulette slot)
    {
        if(Net.Host && Net.Ready)
            Net.Send(new("skin-spin",Amount:slot.Cashback,Tickets:slot.Tickets.Select(t=>t.Row*16+t.Skin).ToArray()));
    }

    private static void SelectFinish(WeaponType weapon,int id)
    {
        if(Net.Guest) Net.Send(new("skin-equip",Amount:(int)weapon*16+id));
        Floor.EquipSkin(weapon,id);
    }

    private static void UpdateSlot(float dt)
    {
        var slot=_world.Slot;
        bool landed=slot.Reveal;
        int before=(int)MathF.Round(slot.Position);
        slot.Time+=dt;
        if(Net.Ready) TickFloor(dt); else _world.AdvanceLighting(dt);
        if(!Net.Host) _world.UpdateCashParticles(dt);
        var cam=BuildPlayerCamera();
        Vector3 target=_world.HrTarotBoothPos+new Vector3(0,1.32f,-.05f);
        var dir=Vector3.Normalize(target-cam.Position);
        float blend=1-MathF.Exp(-5*dt);
        float delta=MathF.IEEERemainder(MathF.Atan2(dir.X,dir.Z)-_playerYaw,MathF.Tau);
        _playerYaw+=delta*blend;
        _playerPitch+=(MathF.Asin(dir.Y)-_playerPitch)*blend;
        for(int i=before;i<(int)MathF.Round(slot.Position);i++) AudioMusicSystem.PlayCaseTick();
        if(!landed && slot.Reveal) {
            _world.SpawnCashBurst(target+new Vector3(0,.12f,-.15f),36,"$");
            if(slot.Prize.Skin>=7) FloorSystems.SpeakCoaching(SlotRoulette.JackpotLine);
            else AudioMusicSystem.PlayTarotCash();
            Raylib.EnableCursor();
            return;
        }
        if(!slot.Reveal) return;
        Vector2 p=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
        bool click=Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool equip=Raylib.IsKeyPressed(KeyboardKey.Enter) || click && Raylib.CheckCollisionPointRec(p,EquipPrize);
        if(equip || Raylib.IsKeyPressed(KeyboardKey.Escape) || click && Raylib.CheckCollisionPointRec(p,StorePrize)) {
            if(equip) SelectFinish(FloorSystems.WeaponOrder[slot.Prize.Row],slot.Prize.Skin);
            slot.Active=false;
            Raylib.DisableCursor();
        }
    }

    private static void DrawSlot()
    {
        var slot=_world.Slot;
        var (row,skin)=slot.Prize;
        int tier=(skin-1)/3;
        Texture2D preview=slot.Reveal ? LoreAndMenusUI.RenderShopExhibit(FloorSystems.WeaponModels[row],slot.Time*37,PanelScale*2,skin) : default;
        Raylib.BeginMode2D(new Camera2D{Zoom=PanelScale,Offset=PanelOffset});
        Rectangle caseRect=new(78,400,1124,207);
        // Sample actual walnut from the briefcase's wooden rim.
        Raylib.DrawTexturePro(shopBackdrop,new(0,85,55,485),caseRect,Vector2.Zero,0,Color.White);
        Raylib.DrawRectangleRoundedLinesEx(caseRect,.06f,10,3,shopGold);
        Rectangle velvet=new(96,414,1088,178);
        Raylib.DrawRectangleRounded(velvet,.06f,10,new(81,24,38,255));
        if(slot.Reveal) {
            Raylib.DrawTexturePro(preview,new(0,0,preview.Width,-preview.Height),new(129,412,428,185),Vector2.Zero,0,Color.White);
            ShopText((skin>=7 ? Loc.T("ДЖЕКПОТ   ","JACKPOT   ") : "")+Loc.Tr(WeaponSkins.Tiers[tier]).ToUpperInvariant(),637,425,18,WeaponSkins.Inks[tier],false);
            ShopText(WeaponSkins.Names[skin],635,450,32,shopCream,false,width:521);
            ShopText(catalogNames[row],637,492,22,shopCream,false,false);
            if(slot.Cashback>0) ShopText($"Повтор в коллекции   +${slot.Cashback}",637,519,20,shopGold,false,false);
            Button(EquipPrize,"[ENTER] НАДЕТЬ СКИН"); Button(StorePrize,"[ESC] В КОЛЛЕКЦИЮ");
        } else {
            var p=PanelOffset;
            Raylib.BeginScissorMode((int)(p.X+100*PanelScale),(int)(p.Y+417*PanelScale),(int)(1080*PanelScale),(int)(170*PanelScale));
            for(int i=0;i<slot.Tickets.Length;i++){
                float x=640+(i-slot.Position)*208;
                if(x< -10 || x>1290) continue;
                var ticket=slot.Tickets[i];
                int tTier=Math.Clamp((ticket.Skin-1)/3,0,3);
                (Color top,Color bot,Color edge)=tTier switch{
                    0=>(new Color(22,52,110,255),new Color(58,132,235,255),new Color(128,192,255,255)),
                    1=>(new Color(56,22,114,255),new Color(138,68,242,255),new Color(198,144,255,255)),
                    2=>(new Color(102,14,50,255),new Color(224,44,110,255),new Color(255,134,178,255)),
                    _=>(new Color(118,74,10,255),new Color(246,196,64,255),new Color(255,244,182,255))
                };
                Rectangle card=new(x-98,429,196,143);
                Raylib.DrawRectangleRounded(new(card.X+2,card.Y+3,card.Width,card.Height),.07f,8,new(12,4,8,175));
                Raylib.DrawRectangleGradientV((int)card.X+2,(int)card.Y+2,(int)card.Width-4,(int)card.Height-4,top,bot);
                Raylib.DrawRectangleGradientV((int)card.X+3,(int)card.Y+3,(int)card.Width-6,(int)(card.Height*.48f),new Color(255,255,255,tTier==3 ? 72 : 34),Color.Blank);
                Raylib.DrawRectangleRoundedLinesEx(card,.06f,8,tTier==3 ? 2.6f : 1.6f,edge);
                var icon=_slotIcons[(ticket.Row+4)%5];
                var raw=iconCrops[ticket.Row];
                float sx=icon.Width/256f, sy=icon.Height/256f;
                Rectangle crop=new(raw.X*sx,raw.Y*sy,raw.Width*sx,raw.Height*sy);
                float k=Math.Min(154/crop.Width,108/crop.Height);
                Vector2 sz=new(crop.Width*k,crop.Height*k);
                Raylib.DrawTexturePro(icon,crop,new(x+3,503,sz.X,sz.Y),sz/2,0,new Color(10,6,12,115));
                Raylib.DrawTexturePro(icon,crop,new(x,500,sz.X,sz.Y),sz/2,0,Color.White);
            }
            Raylib.EndScissorMode();
            Raylib.DrawLineEx(new(640,418),new(640,586),2.5f,new Color(248,216,126,210));
            Raylib.DrawTriangle(new(626,406),new(640,434),new(654,406),shopGold);
        }
        Raylib.EndMode2D();
        void Button(Rectangle r,string label){
            PaperSlip(r,shopCream,shopGold);
            ShopText(label,r.X+r.Width/2,r.Y+10,19,shopInk,width:(int)r.Width-10);
        }
    }
}
