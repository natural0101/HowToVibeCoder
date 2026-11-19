using System.Text.Json;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal enum Finish { Paint, Slate, Armor, Rack, Screen, Fabric, Leather, Lamp, Contact, Tarot, Lift, Plaster, Facade, Sky, Glass, Exit, Billboard, Imported, Carpet, CharacterShadow, Fluorescent, Anatomy, BlackMarble, Bronze, RosinSmoke, WaterPuddle, Plasma, Cloud, LiftGlass, JiraSheet, NdaSheet, PitchDeck }

internal static unsafe partial class VisualEngine3D
{
    private static Shader shader;
    private static Model box, bevel, plane, tube, sphere;
    private static readonly Dictionary<string, Model> imports = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ((int Start, int Count) Left, (int Start, int Count) Right)> armGroups = new();
    private static readonly Dictionary<(string Asset, bool Right), Vector3> armWrists = new();
    private static readonly Dictionary<(string Asset, bool Right), Vector3> sleeveEnds = new();
    private static int sleeveAxisLoc,sleevePullLoc,wristLoc,handSocketLoc,gripLoc;
    private static readonly HashSet<int> partnerPropMeshes = new();
    private static int batchLoc, clockLoc, beltTensionLoc;
    private static readonly Texture2D[] maps = new Texture2D[Enum.GetValues<Finish>().Length];
    private static readonly Dictionary<(string Asset,int Mesh),int> handSurfaces=new();
    private static int handSurfaceLoc, weaponFinishLoc;
    private static int firstPersonLoc, muzzleLoc, flashLoc;
    private static int eyeLoc, finishLoc, taperLoc, laptopLoc, tarotLoc, northLoc, southLoc, powerLoc, cabinPowerLoc;
    private static Finish? activeFinish;
    private static float activeTaper;
    private static int podLoc,shadowPodLoc,loadLoc;
    internal static HashSet<string> CapturedAssets;

    // The workstation GLB has one joined mesh. Its wheeled chair and monitor heads
    // occupy separate local volumes. Keep the same deformation in the depth pass.
    private const string PodGeometry = """
        uniform vec2 podPose;
        void posePod(inout vec3 p,inout vec3 n) {
            if(podPose.x==0.) return;
            bool chair=p.z<-.405 && abs(p.x)<.38 && (p.y<.70 || p.z<-.77);
            bool screen=p.y>.897 && abs(p.x)<.81 && abs(p.z-.24)<.18;
            if(!chair && !screen) return;
            vec2 pivot=chair ? vec2(0.,-.72) : vec2(p.x<0. ? -.42 : .34,.25);
            float a=podPose.x*(chair ? 1. : .22);
            mat2 rotation=mat2(cos(a),-sin(a),sin(a),cos(a));
            p.xz=pivot+rotation*(p.xz-pivot);
            if(chair) p.z-=podPose.y;
            n.xz=rotation*n.xz;
        }
        """;

    private const string Vertex = """
        #version 330 core
        in vec3 vertexPosition;
        in vec3 vertexNormal;
        in vec4 vertexColor;
        uniform int batch, boothColor;
        out vec4 tint;
        in vec2 vertexTexCoord;
        uniform mat4 mvp, matModel;
        uniform float taper;
        uniform float beltTension;
        uniform vec3 sleeveAxis,sleevePull,wrist,handSocket;
        uniform float gripAmount;
        uniform int firstPerson;
        out vec3 pos, norm;
        out vec2 localXZ;
        out vec2 uv;
        void main() {
            vec3 p = vertexPosition;
            vec3 n = vertexNormal;
            posePod(p,n);
            // Only jacket rings carry this UV channel. Rotate each complete section
            // toward its shoulder, preserving its radius and the rigid wrist opening.
            if (vertexTexCoord.x>1.99 && dot(sleevePull,sleevePull)>.0001) {
                float t=clamp(vertexTexCoord.y,0.,1.);
                // Hermite centreline between cuff and elbow. Use its tangent to
                // turn the complete cloth ring and stitching without flattening it.
                vec3 a=normalize(sleeveAxis);
                vec3 tangent0=a*min(length(sleeveAxis),length(sleevePull)*.54);
                vec3 tangent1=sleevePull*1.34;
                float t2=t*t, t3=t2*t;
                vec3 center=(t3-2.*t2+t)*tangent0+(3.*t2-2.*t3)*sleevePull+(t3-t2)*tangent1;
                vec3 b=normalize((3.*t2-4.*t+1.)*tangent0+6.*t*(1.-t)*sleevePull+(3.*t2-2.*t)*tangent1);
                float blend=smoothstep(.055,.22,t);
                vec4 q=normalize(mix(vec4(0.,0.,0.,1.),vec4(cross(a,b),1.+dot(a,b)),blend));
                vec3 offset=p-wrist-sleeveAxis*t;
                p+=2.*cross(q.xyz,cross(q.xyz,offset)+q.w*offset);
                p+=(center-sleeveAxis*t)*blend;
                n+=2.*cross(q.xyz,cross(q.xyz,n)+q.w*n);
            }
            if(gripAmount!=0.) {
                float flex=smoothstep(.040,.092,length(p-wrist))*(1.-smoothstep(.052,.091,length(p-handSocket)));
                p.xy-=gripAmount*flex*(p.xy-handSocket.xy);
            }
            if(beltTension>0.) {
                float span=smoothstep(-.36,-.04,p.x);
                p.x+=.014*beltTension*(2.*span-1.);
                p.y+=.024*beltTension*4.*span*(1.-span);
            }
            p.xz *= 1.0 + p.y*taper;
            n.y -= taper * dot(vertexPosition.xz,n.xz);
            pos = (matModel * vec4(p,1)).xyz;
            norm = normalize(transpose(inverse(mat3(matModel))) * n);
            uv = vertexTexCoord; localXZ = vertexPosition.xz;
            tint = batch == 1 || firstPerson==1 || boothColor==1 ? vertexColor : vec4(1.);
            gl_Position = mvp * vec4(p,1);
            // Reserve the nearest tenth for the viewmodel, retaining depth
            // precision between the closely spaced fingers and grip surfaces.
            if (firstPerson==1) gl_Position.z = gl_Position.z*.1 + gl_Position.w*(-.9);
        }
        """;

    private const string Fragment = """
        #version 330 core
        in vec3 pos, norm;
        in vec2 localXZ;
        in vec2 uv;
        in vec4 tint;
        uniform sampler2D texture0;
        uniform int boothColor;
        uniform vec4 colDiffuse, colSpecular;
        uniform vec3 eye, laptop, tarot, northBeacon, southBeacon;
        uniform float power, clock, flashStrength, cabinPower;
        uniform vec2 pendants[9];
        uniform float rackLoad;
        uniform vec3 muzzle;
        uniform int firstPerson;
        uniform int finish,handSurface;
        uniform vec2 weaponFinish;
        uniform sampler2D shadowMap;
        uniform mat4 lightVP;
        uniform vec3 puddleLight;
        uniform float puddlePower, puddleElectric;
        uniform int shadows;
        const vec3 sunDir=vec3(-.76825,.21412,-.60323);
        out vec4 color;
        float sunlight(vec3 n) {
            if(firstPerson==1 || shadows==0) return 1.;
            vec4 light=lightVP*vec4(pos,1.);
            vec3 q=light.xyz/light.w*.5+.5;
            if(any(lessThan(q,vec3(.001))) || any(greaterThan(q,vec3(.999)))) return 1.;
            float bias=max(.0008,.0032*(1.-dot(n,sunDir)));
            float visibility=0.;
            for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
                visibility+=step(q.z-bias,texture(shadowMap,q.xy+vec2(x,y)/2048.).r);
            return visibility/9.;
        }
        float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
        float noise(vec2 p) {
            vec2 i=floor(p), u=fract(p); u=u*u*(3.-2.*u);
            return mix(mix(hash(i),hash(i+vec2(1,0)),u.x),mix(hash(i+vec2(0,1)),hash(i+1.),u.x),u.y);
        }
        float fbm(vec2 p) { return noise(p)*.55+noise(p*2.13)*.3+noise(p*4.61)*.15; }
        vec3 skyline(vec3 r) {
            float h=max(r.y,0.);
            float s=max(dot(r,sunDir),0.);
            vec3 horizon=mix(vec3(.50,.215,.24),vec3(1.45,.48,.125),pow(s,5.));
            vec3 sky=mix(horizon,vec3(.095,.145,.34),smoothstep(.023,.42,h));
            vec2 air=r.xz/max(.06,r.y+.11);
            float cloud=fbm(air*vec2(1.25,5.1)+vec2(.4,2.));
            float cirrus=smoothstep(.46,.71,cloud)*smoothstep(.01,.12,h)*(1.-smoothstep(.31,.63,h));
            sky=mix(sky,vec3(.55,.265,.285)+vec3(.43,.16,.02)*pow(s,12.),cirrus*.48);
            sky=mix(sky,vec3(.035,.065,.20),smoothstep(.28,.88,h));
            return sky+vec3(1.4,.57,.16)*pow(s,42.)+vec3(3.5,2.3,1.25)*smoothstep(.99955,.99990,s);
        }
        vec3 reflectionBands(vec3 r) {
            vec2 wallDistance=(sign(r.xz)*38.-pos.xz)/(sign(r.xz+vec2(1e-8))*max(abs(r.xz),vec2(.001)));
            float wall=min(wallDistance.x,wallDistance.y);
            float ceiling=11.2/max(.015,r.y);
            vec2 hit=pos.xz+r.xz*ceiling;
            vec2 strip=abs(mod(hit+3.,6.)-3.);
            vec2 aa=clamp(fwidth(hit),vec2(.07),vec2(3.));
            vec2 footprint=1.-smoothstep(vec2(.93,.18)-aa,vec2(1.18,.35)+aa,strip);
            float active=step(.64,hash(floor((hit+3.)/6.)));
            float lamps=footprint.x*footprint.y*active*exp(-.016*ceiling);
            vec3 ceilingColor=mix(vec3(.60,.76,1.),vec3(1.25,.80,.42),step(3.,mod(hit.x,12.)))*lamps;
            vec3 intersect=pos+r*wall;
            float u=wallDistance.x<wallDistance.y ? intersect.z : intersect.x;
            float mullion=abs(fract(u/4.5+.5)-.5)*4.5;
            float frame=mix(.28,1.,smoothstep(.025,.16+min(fwidth(u),.5),mullion));
            vec3 window=mix(vec3(.13,.18,.26),vec3(.92,.39,.16),pow(max(dot(r,sunDir),0.),4.));
            return mix(ceilingColor,window*frame,1.-smoothstep(9.8,11.3,intersect.y));
        }
        vec3 point(vec3 at, vec3 tint, vec3 n, vec3 v, float gloss) {
            vec3 d=at-pos;
            float r=dot(d,d);
            vec3 l=d*inversesqrt(max(.001,r));
            float spec=pow(max(0.,dot(n,normalize(v+l))),gloss);
            return tint*(.12+max(0.,dot(n,l))+spec*1.4)/(1.+r*.17);
        }
        vec3 muzzleLight(vec3 n,vec3 v) {
            vec3 d=muzzle-pos;
            vec3 l=d*inversesqrt(max(.0001,dot(d,d)));
            float facing=max(0.,dot(n,l));
            float glint=pow(max(0.,dot(n,normalize(l+v))),18.);
            return vec3(1.,.51,.19)*flashStrength*(.12+.45*facing+.58*glint)/(1.+18.*dot(d,d));
        }
        float limb(vec2 p,vec2 a,vec2 b,float r) {
            vec2 v=b-a;
            return length(p-a-v*clamp(dot(p-a,v)/dot(v,v),0.,1.))-r;
        }
        void main() {
            if(finish==19) {
                vec2 p=vec2(localXZ.x,localXZ.y+.5);
                float d=limb(p,vec2(-.13,.04),vec2(-.06,.47),.057);
                d=min(d,limb(p,vec2(.13,.07),vec2(.06,.47),.06));
                d=min(d,limb(p,vec2(0,.44),vec2(0,.68),.145));
                d=min(d,limb(p,vec2(-.12,.69),vec2(-.27,.44),.048));
                d=min(d,limb(p,vec2(.12,.68),vec2(.30,.55),.053));
                d=min(d,length((p-vec2(.015,.875))*vec2(.88,1.17))-.105);
                color=vec4(.001,.003,.012,(1.-smoothstep(-.007,.034,d))*.82); return;
            }
            if(finish==27) {
                float rim=clamp(dot(normalize(norm),normalize(eye-pos)),0.,1.);
                float drift=sin(pos.x*4.+clock)+sin(pos.z*5.-clock*.7)+sin(pos.y*6.-clock*1.4);
                color=vec4(colDiffuse.rgb,colDiffuse.a*rim*rim*(.70+.09*drift)); return;
            }
            if(finish==24) {
                float h=clamp(.5-localXZ.y,0.,1.);
                float center=h*(.17*sin(h*8.-clock*2.)+.12*sin(h*14.-clock*1.3));
                float d=(localXZ.x-center)/(.033+h*.15);
                float opacity=exp(-d*d)*smoothstep(0.,.07,h)*(1.-smoothstep(.18,1.,h));
                color=vec4(.70,.77,.85,.48*opacity); return;
            }
            vec3 v=normalize(eye-pos), flatN=normalize(cross(dFdx(pos),dFdy(pos)));
            if(dot(flatN,v)<0.) flatN=-flatN;
            vec3 n=flatN;
            if((finish==21 || finish==17) && dot(norm,norm)>.01) {
                n=normalize(norm);
                if(dot(n,flatN)<0.) n=-n;
            } else if(dot(n,norm)<0.) n=-n;
            vec2 st=uv;
            if(finish==1) st=pos.xz*.36;
            if(finish==11) st=(abs(n.y)>.6 ? pos.xz : (abs(n.x)>.6 ? pos.zy : pos.xy))*.5;
            vec4 tex=texture(texture0,st);
            if(finish==8) {
                float halo=1.-smoothstep(.35,1.,length(uv*2.-1.));
                color=vec4(.008,.010,.019,halo*.55); return;
            }
            vec4 ink=colDiffuse*tint;
            vec3 base=tex.rgb*ink.rgb;
            bool leather=finish==6 || (finish==17 && colSpecular.a>.49 && colSpecular.a<.51);
            vec3 emission=(finish==17 || finish==21) ? colSpecular.rgb*tex.rgb : vec3(0.);
            if(boothColor==1) emission*=tint.rgb;
            if(finish==17 && colSpecular.a>.70 && colSpecular.a<.80) {
                float tick=floor(clock*(2.2+7.*rackLoad));
                float active=step(.40,hash(vec2(floor(pos.y*44.)+floor(pos.x*24.),tick)));
                vec3 lamp=emission;
                if(lamp.b>lamp.g && lamp.b>.1) lamp=vec3(1.,.095,.045);
                emission=lamp*(.09+1.55*active);
                base*=.38+.62*active;
            } else if(finish==17 && colSpecular.a>.85 && colSpecular.a<.94) {
                float scan=.97+.03*sin(clock*17.+pos.y*53.+pos.x);
                base*=scan; emission*=scan;
            }
            float dist=length(eye-pos);
            if(finish==13) {
                vec3 ray=normalize(pos-eye);
                color=vec4(skyline(ray),1.); return;
            }
            if(finish==12) {
                vec2 wall=vec2(abs(n.x)>.6 ? pos.z : pos.x,pos.y);
                vec2 grid=wall*vec2(1.75,1.28);
                vec2 id=floor(grid), a=abs(fract(grid)-.5);
                vec2 aa=clamp(fwidth(grid),.008,.24);
                vec2 pane=1.-smoothstep(vec2(.32,.24)-aa,vec2(.32,.24)+aa,a);
                float seed=dot(floor(pos.xz*.02),vec2(37.,71.));
                float occupancy=fract(sin(dot(id,vec2(12.9898,78.233))+seed)*4375.5453);
                float sunHit=max(0.,dot(n,sunDir));
                vec3 paneColor=mix(vec3(.033,.062,.105),vec3(.38,.17,.072),sunHit);
                paneColor+=skyline(reflect(-v,n))*.12;
                float room=hash(floor(id/vec2(3.,1.))+seed);
                float lights=smoothstep(.76,.86,room)*smoothstep(.35,.65,occupancy);
                paneColor+=mix(vec3(1.75,.80,.29),vec3(.48,1.22,1.54),step(.92,room))*lights;
                vec3 facade=mix(vec3(.055,.075,.11),paneColor,pane.x*pane.y*step(abs(n.y),.5));
                color=vec4(mix(facade,vec3(.46,.29,.27),smoothstep(48.,165.,dist)*.46),1.); return;
            }
            if(finish==26) { color=vec4(vec3(1.2,2.2,3.)*ink.rgb,ink.a); return; }
            if(finish==25) {
                vec2 p=localXZ*2.;
                float radius=length(p), angle=atan(p.y,p.x);
                // A broad spill, two runnels and drops pinned to the tile beside it.
                vec2 q=p+vec2(.075*sin(6.*p.y),.075*sin(5.*p.x+1.2));
                float basin=.72-length((q-vec2(-.12,.04))*vec2(.98,1.23));
                float runnel=.36-length((q-vec2(.43,-.19))*vec2(.86,1.60));
                float lip=.29-length((q-vec2(-.36,.44))*vec2(1.06,.86));
                float drops=max(.078-length((p-vec2(.56,.42))*vec2(.85,1.4)),
                                .045-length(p-vec2(.65,.48)));
                float inside=max(max(basin,runnel),max(lip,drops))+.105*(fbm(p*6.1)-.52);
                if(inside<0.) discard;
                float wave=cos(radius*52.-clock*3.6)*exp(-radius*1.7);
                float ring=pow(max(wave,0.),3.);
                vec3 wn=normalize(vec3(.026*wave*cos(angle),1.,.026*wave*sin(angle)));
                vec3 r=reflect(-v,wn);
                float fres=.12+.88*pow(1.-abs(dot(v,wn)),4.);
                vec3 wet=vec3(.025,.043,.055)+reflectionBands(r)*(.16+fres*.94);
                float rim=exp(-inside*140.)*smoothstep(.14,.74,fbm(p*13.));
                wet+=vec3(.42,.51,.52)*rim+vec3(.035,.065,.083)*ring;
                float frame=floor(clock*13.);
                float lightning=0.;
                for(int i=0;i<3;i++) {
                    float a=float(i)*2.1+.45*sin(frame*.78+float(i));
                    vec2 q=mat2(cos(a),-sin(a),sin(a),cos(a))*p;
                    float bend=.25*(noise(vec2(q.x*9.,frame+i))-.5)+.075*(noise(vec2(q.x*36.,frame))-.5);
                    float d=abs(q.y-bend-.09*sin(q.x*8.+float(i)));
                    float branch=abs(q.y-bend*.60-.16*sin(q.x*9.+float(i))-.15*(q.x-.32));
                    lightning+=(exp(-d/max(.002,fwidth(q.y)*.72))+
                        .54*exp(-branch/max(.0015,fwidth(q.y)*.58))*smoothstep(.20,.44,q.x))*smoothstep(-.12,.19,q.x);
                }
                wet+=vec3(1.8,3.1,4.)*lightning*puddleElectric;
                color=vec4(wet,smoothstep(0.,max(.005,fwidth(inside)),inside)*(.52+.37*fres+.10*min(lightning,1.))); return;
            }
            if(finish==14 || finish==28) {
                float edge=pow(1.-abs(dot(n,v)),3.);
                float glint=pow(max(0.,dot(reflect(-v,n),normalize(vec3(-.55,.72,-.42)))),16.);
                vec4 glass=vec4(mix(ink.rgb*.55,vec3(.90,.70,.51),glint),ink.a*.63+edge*.025+glint*.12);
                if(finish==28) {
                    float frost=clamp((ink.a*255.-138.)/110.,0.,1.);
                    float grain=fract(sin(dot(floor(pos.xy*180.),vec2(12.9898,78.233)))*43758.5453);
                    glass=mix(glass,vec4(ink.rgb*(.90+.15*grain),ink.a),frost);
                }
                color=glass; return;
            }
            if(finish==21) {
                // Imported MPFB swatches are linear glTF factors. Convert once
                // for the display framebuffer and keep the planes readable.
                vec3 pigment=pow(base,vec3(1./2.2));
                vec3 key=normalize(vec3(-.44,.80,-.38));
                float sun=max(0.,dot(n,key));
                float sky=.5+.5*n.y;
                float bounce=.5+.5*dot(n,normalize(vec3(.75,.16,.5)));
                if(firstPerson==0) pigment=mix(base,pigment,.40);
                vec3 value=pigment*(vec3(.23,.27,.35)+sky*.12+bounce*.10+max(0.,dot(n,sunDir))*sunlight(n)*vec3(1.48,1.02,.64));
                if(firstPerson==0) {
                    vec3 portrait=vec3(-.65,1.92,11.95)-pos;
                    float falloff=1.-smoothstep(.45,2.45,length(portrait));
                    float wrap=clamp(dot(n,normalize(portrait))*.85+.15,0.,1.);
                    value+=pigment*falloff*(vec3(.06,.04,.08)+vec3(.48,.36,.28)*wrap);
                    float weave=sin(uv.x*480.)*cos(uv.y*380.)*(1.-smoothstep(.2,1.,fwidth(uv.x)*480.));
                    value*=1.+.035*weave;
                    value+=vec3(.09,.14,.18)*pow(1.-abs(dot(n,v)),3.);
                    value+=point(puddleLight,vec3(.06,.55,.76)*puddlePower,n,v,28.)*(pigment*.66+.038);
                }
                if(firstPerson==1) {
                    value=pigment*(vec3(.42,.46,.52)+sky*.19+bounce*.14+sun*vec3(.58,.50,.40));
                    if(handSurface==1) {
                        pigment=tex.rgb*tint.rgb;

                        float wrap=max(0.,dot(n,key)*.65+.35);
                        value=pigment*(vec3(.36,.31,.29)+sky*.14+wrap*vec3(.52,.46,.42));
                        value*=vec3(.96,.92,.90);
                        value+=vec3(.085,.032,.019)*pow(1.-abs(dot(n,v)),2.)*wrap;
                        value+=vec3(.045,.032,.024)*pow(max(0.,dot(n,normalize(key+v))),19.);
                    } else if(handSurface==3) {
                        vec3 wool=mix(base,pigment,.28);
                        value=wool*(vec3(.34,.36,.42)+sky*.15+bounce*.12+sun*vec3(.56,.48,.40));
                        vec2 thread=uv*vec2(235.,540.);
                        float twill=sin(6.283*(thread.x+thread.y));
                        float resolve=1.-smoothstep(.16,.60,fwidth(thread.x+thread.y));
                        value*=.94+.042*twill*resolve;
                        value+=vec3(.048,.012,.018)*pow(1.-abs(dot(n,v)),3.);
                    } else if(handSurface==2) {
                        float glint=pow(max(0.,dot(n,normalize(key+v))),25.);
                        value+=vec3(.31,.29,.24)*glint;
                    }
                    value+=muzzleLight(n,v)*(.14+.65*pigment);
                }
                color=vec4(mix(value+emission*1.65,vec3(.26,.23,.28),1.-exp(-.000055*dist*dist)),ink.a);
                return;
            }
            if(finish==17) base=pow(base,vec3(.78));
            if(finish==18) base*=.93+.07*cos(pos.z*133.)*cos(pos.x*107.);
            if(finish==1 || leather || finish==18 || finish==11 || finish==2) {
                vec3 t=abs(n.y)>.7 ? vec3(1,0,0) : normalize(cross(vec3(0,1,0),n));
                vec3 b=cross(n,t);
                vec2 p=vec2(dot(pos,t),dot(pos,b));
                float freq=finish==18 ? 148. : leather ? 105. : finish==2 ? 80. : 56.;
                float resolve=1.-smoothstep(.3,2.8,length(fwidth(p))*freq);
                vec2 slope=.035*resolve*vec2(sin(p.x*freq),cos(p.y*freq+sin(p.x*freq)));
                if(finish==1) {
                    vec2 tile=fract(p*.72)-.5;
                    float tileDetail=1.-smoothstep(.009,.053,length(fwidth(p)));
                    slope=sign(tile)*smoothstep(vec2(.474),vec2(.493),abs(tile))*.041*tileDetail;
                    slope+=.0029*cos(p*vec2(4.,8.)+sin(p.y*3.))*tileDetail;
                }
                n=normalize(n+t*slope.x+b*slope.y);
            }
            vec3 key=sunDir;
            float shadow=sunlight(n);
            float gloss=finish==1 ? 65. : (finish==2 ? 44. : 24.);
            bool weapon=firstPerson==1 && finish==17 && weaponFinish.y>0.;
            if(weapon) gloss=mix(96.,18.,weaponFinish.y);
            float ao=mix(1.,mix(.68,1.,smoothstep(0.,.42,pos.y)),1.-abs(n.y));
            vec3 light=mix(vec3(.16,.12,.10),vec3(.18,.25,.36),.5+.5*n.y)*ao;
            light+=vec3(1.48,1.02,.64)*max(0.,dot(n,key))*shadow;
            if(firstPerson==0 && pos.y<9.) {
                float pool=0.;
                for(int i=0;i<9;i++) {
                    vec2 offset=pos.xz-pendants[i];
                    pool=max(pool,1.-smoothstep(.18,1.,dot(offset,offset*vec2(.075,.13))));
                }
                light+=vec3(.92,.55,.25)*pool*(.29+.71*max(0.,n.y));
            }
            if(firstPerson==0) light+=point(puddleLight,vec3(.08,1.12,1.55)*puddlePower,n,v,gloss);
            light+=point(laptop,vec3(.25,.95,1.25)*power,n,v,gloss);
            light+=point(tarot,vec3(1.6,.85,.38)*(.94+.06*sin(clock*8.)),n,v,gloss);
            light+=point(tarot+vec3(.55,-.30,.18),vec3(.42,.18,.82),n,v,gloss);
            float beacon=.82+.28*sin(clock*3.2);
            light+=point(northBeacon,vec3(1.4,.12,.15)*beacon,n,v,gloss);
            light+=point(northBeacon+vec3(0.,-.5,2.35),vec3(2.1,.09,.06)*cabinPower,n,v,gloss);
            light+=point(southBeacon,vec3(1.4,.12,.15)*beacon,n,v,gloss);
            if(firstPerson==1) light=max(light,vec3(.68,.64,.66));
            float fres=pow(1.-max(0.,dot(n,v)),3.);
            vec3 lit=base*light+vec3(.08,.09,.11)*fres*(.35+tex.r);
            float nh=max(0.,dot(n,normalize(v+key))),nv=max(.01,dot(n,v));
            float rough=finish==1 ? .32 : finish==2 || finish==23 ? .32 : .53;
            float a2=pow(rough,4.), denom=nh*nh*(a2-1.)+1.;
            float D=a2/(3.14159*denom*denom+.0001);
            vec3 F0=finish==23 ? base*.72 : vec3(.04);
            vec3 F=F0+(1.-F0)*pow(1.-max(0.,dot(v,normalize(v+key))),5.);
            lit+=vec3(1.48,1.02,.64)*F*min(3.,D)*max(0.,dot(n,key))*shadow/(.45+nv);
            if(weapon) {
                // Broad overhead softbox across curved barrels and a softer satin lobe on wood.
                vec3 r=reflect(-v,n);
                float strip=pow(max(0.,1.-abs(dot(r,normalize(vec3(.32,.85,.41))))),gloss*.46);
                lit+=mix(vec3(.045,.036,.028),vec3(.29,.34,.40),weaponFinish.x)*strip;
            }
            if(finish==22) {
                vec2 p=vec2(abs(n.x)>.7 ? pos.z : pos.x,pos.y);
                float vein=p.y*2.9+p.x*1.8+.36*sin(p.x*3.+p.y)+.07*sin(p.y*12.);
                float filament=1.-smoothstep(.015,.047+fwidth(vein),abs(sin(vein)));
                lit+=vec3(.13,.13,.12)*filament*(.6+.4*cos(p.x*2.));
            }
            if(finish==23) {
                vec3 r=reflect(-v,n);
                float band=.5+.5*sin(r.y*13.+r.x*3.);
                lit=base*(.26+.30*band+.65*max(0.,dot(n,key))*shadow)+vec3(.21,.14,.07)*pow(band,8.);
            }
            if(finish==1) {
                lit=lit*.76+reflectionBands(reflect(-v,n))*(.025+.34*fres);
                lit+=point(puddleLight,vec3(.07,.85,1.3)*puddlePower,n,v,54.)*(.08+fres*.22);
            }
            if(finish==7 || finish==15 || finish==16 || finish==20) lit=base*1.62;
            if(finish==4) lit=base*(.44+1.40*max(tex.g,tex.r));
            if(finish==3) lit+=base*max(tex.g,tex.r)*.65;
            if(finish==10) lit=base;
            if(firstPerson==1) lit+=muzzleLight(n,v)*(.26+base);
            lit+=emission*1.65;
            float fog=1.-exp(-.000055*dist*dist);
            color=vec4(mix(lit,vec3(.26,.23,.28),fog),ink.a);
        }
        """;

    internal static bool HasAsset(string name) => imports.ContainsKey(name);

    public static void Load()
    {
        shader = Raylib.LoadShaderFromMemory(Vertex.Replace("void main()",PodGeometry+"\nvoid main()"), Fragment);
        if (shader.Id == 0 || shader.Id == Rlgl.GetShaderIdDefault())
            throw new InvalidOperationException("Failed to compile the world lighting shader.");
        int Loc(string name) => Raylib.GetShaderLocation(shader, name);
        shader.Locs[(int)ShaderLocationIndex.ColorSpecular]=Loc("colSpecular");
        boothColorLoc=Loc("boothColor");
        handSurfaceLoc=Loc("handSurface"); weaponFinishLoc=Loc("weaponFinish");
        firstPersonLoc = Loc("firstPerson");
        muzzleLoc = Loc("muzzle");
        flashLoc = Loc("flashStrength");
        beltTensionLoc = Loc("beltTension");
        sleeveAxisLoc=Loc("sleeveAxis"); sleevePullLoc=Loc("sleevePull"); wristLoc=Loc("wrist");
        handSocketLoc=Loc("handSocket"); gripLoc=Loc("gripAmount");
        batchLoc = Loc("batch"); clockLoc = Loc("clock");
        eyeLoc = Loc("eye"); finishLoc = Loc("finish"); taperLoc = Loc("taper");
        laptopLoc = Loc("laptop"); tarotLoc = Loc("tarot"); northLoc = Loc("northBeacon");
        southLoc = Loc("southBeacon"); powerLoc = Loc("power"); cabinPowerLoc = Loc("cabinPower");
        InitLightingPipeline();
        podLoc=Loc("podPose");
        shadowPodLoc=Raylib.GetShaderLocation(shadowShader,"podPose");
        loadLoc=Loc("rackLoad");
        Raylib.SetShaderValueV(shader,Loc("pendants[0]"),SkyscraperFloor1World.PendantLocations,ShaderUniformDataType.Vec2,SkyscraperFloor1World.PendantLocations.Length);
        box = ModelFor(Raylib.GenMeshCube(1,1,1));
        bevel = ModelFor(RoundedBox());
        plane = ModelFor(Raylib.GenMeshPlane(1,1,1,1));
        tube = ModelFor(Raylib.GenMeshCylinder(1,1,10));
        sphere = ModelFor(Raylib.GenMeshSphere(1,6,9));
        foreach (Finish f in Enum.GetValues<Finish>())
            maps[(int)f] = MakeTexture(f);
        string directory = Path.Combine(AppContext.BaseDirectory, "assets", "models");
        foreach (string path in Directory.EnumerateFiles(directory, "*.glb", SearchOption.AllDirectories))
        {
            Model model = Raylib.LoadModel(path);
            if (model.MeshCount == 0 || model.MaterialCount < 2)
                throw new InvalidDataException($"Invalid Blender model: {path}");
            ApplyShaderToModel(model);
            ReadMaterialLighting(path,model);
            string name = Path.GetFileNameWithoutExtension(path);
            RestoreBoothPalette(path,name,model);
            imports.Add(name, model);
            if (name.StartsWith("fps_hands_", StringComparison.Ordinal) || name.StartsWith("fps_left_", StringComparison.Ordinal) || name == "char_partner_90s") ReadMeshGroups(path, name, model.MeshCount);
        }
    }

    // Raylib exposes the authored base textures. Pack glTF emission and the
    // leather response into the unused specular colour, leaving roughness intact.
    private static void ReadMaterialLighting(string path,Model model)
    {
        using var reader=new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position=12;
        int bytes=reader.ReadInt32();
        if(reader.ReadUInt32()!=0x4E4F534A) throw new InvalidDataException(path);
        using var json=JsonDocument.Parse(reader.ReadBytes(bytes));
        if(!json.RootElement.TryGetProperty("materials",out var materials)) return;
        int i=1;
        foreach(var material in materials.EnumerateArray())
        {
            if(i>=model.MaterialCount) break;
            string name=material.TryGetProperty("name",out var n) ? n.GetString() ?? "" : "";
            Vector3 glow=Vector3.Zero;
            if(material.TryGetProperty("emissiveFactor",out var e))
                glow=new(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle());
            if(name.Equals("display",StringComparison.OrdinalIgnoreCase) && glow==Vector3.Zero) glow=new(.25f,.66f,.78f);
            if(name.Contains("nichrome",StringComparison.OrdinalIgnoreCase)) glow=new(1,.48f,.15f);
            int tag=name.Contains("Leather",StringComparison.OrdinalIgnoreCase) ? 128 :
                name is "LedAmber" or "LedCyan" or "LedGreen" ? 192 :
                name is "MonitorCode1" or "MonitorCode2" or "CrtPhosphorScreen" ? 228 : 0;
            model.Materials[i++].Maps[1].Color=new Color((int)(255*glow.X),(int)(255*glow.Y),(int)(255*glow.Z),tag);
        }
    }

    // Raylib expands each glTF primitive into a mesh, in mesh/primitive order.
    private static void ReadMeshGroups(string path, string asset, int meshCount)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position = 12;
        int length = reader.ReadInt32();
        if (reader.ReadUInt32() != 0x4E4F534A) throw new InvalidDataException(path);
        using var json = JsonDocument.Parse(reader.ReadBytes(length));
        int index = 0;
        (int Start, int Count) leftArm = default, rightArm = default;
        foreach (var mesh in json.RootElement.GetProperty("meshes").EnumerateArray())
        {
            int count = mesh.GetProperty("primitives").GetArrayLength();
            if (asset == "char_partner_90s")
            {
                var materials = json.RootElement.GetProperty("materials");
                foreach (var part in mesh.GetProperty("primitives").EnumerateArray())
                {
                    string material = materials[part.GetProperty("material").GetInt32()].GetProperty("name").GetString();
                    if (material is "GunMetal" or "Gold90s" or "PolymerGrip") partnerPropMeshes.Add(index);
                    index++;
                }
                continue;
            }
            int primitiveIndex=index;
            foreach(var prim in mesh.GetProperty("primitives").EnumerateArray())
            {
                string material=json.RootElement.GetProperty("materials")[prim.GetProperty("material").GetInt32()].GetProperty("name").GetString();
                handSurfaces[(asset,primitiveIndex++)]=material=="fps_skin" ? 1 : material is "steel" or "gold" ? 2 : material=="crimson" ? 3 : 0;
            }
            if (asset != "fps_hands_cs2")
            {
                var wrist = mesh.GetProperty("extras").GetProperty("wrist");
                armWrists.Add((asset, mesh.GetProperty("name").GetString() == "Right"),
                    new Vector3(wrist[0].GetSingle(), wrist[1].GetSingle(), wrist[2].GetSingle()));
                if(mesh.GetProperty("extras").TryGetProperty("sleeveEnd",out var end))
                    sleeveEnds.Add((asset,mesh.GetProperty("name").GetString()=="Right"),
                        new(end[0].GetSingle(),end[1].GetSingle(),end[2].GetSingle()));
            }
            switch (mesh.GetProperty("name").GetString())
            {
                case "Left": leftArm = (index,count); break;
                case "Right": rightArm = (index,count); break;
                default: throw new InvalidDataException(path);
            }
            index += count;
        }
        bool isFpsArm = asset.StartsWith("fps_hands_", StringComparison.Ordinal) || asset.StartsWith("fps_left_", StringComparison.Ordinal);
        if (index != meshCount || (isFpsArm ? leftArm.Count * rightArm.Count == 0 : partnerPropMeshes.Count != 3))
            throw new InvalidDataException("Incomplete first-person arm mesh");
        if (isFpsArm) armGroups.Add(asset, (leftArm, rightArm));
    }

    public static void Arm(bool right, Vector3 wrist, string handAsset = "fps_hands_cs2", Matrix4x4? assemblyTransform = null,float grip=0)
    {
        // The existing prop camera uses a mirrored X axis; the anatomy already
        // faces the view direction. Undo that mirror for anatomical handedness.
        Vector3 bind = right ? new(-.20f,-.22f,.54f) : new(-.085f,-.185f,.68f);
        if (handAsset != "fps_hands_cs2") bind = armWrists[(handAsset,right)];
        Vector3 offset = new Vector3(-wrist.X,wrist.Y,wrist.Z) - bind;
        Model model = imports[handAsset];
        var groups = armGroups[handAsset];
        var range = right ? groups.Right : groups.Left;
        Matrix4x4 transform = Matrix4x4.CreateTranslation(offset) * Matrix4x4.CreateScale(-1,1,1);
        if (assemblyTransform.HasValue) transform *= assemblyTransform.Value;
        Surface(Finish.Anatomy,0);
        bool pull=sleeveEnds.ContainsKey((handAsset,right));
        if(pull)
        {
            Vector3 end=sleeveEnds[(handAsset,right)], axis=end-bind;
            // Keep the cuff on the authored wrist. The far end of the sleeve
            // follows an offscreen elbow while the hand turns with the prop.
            Vector3 anchor=new(right ? .56f : -.18f,-.395f,.23f);
            // The left sleeve enters from the lower-left edge, including when
            // the animated wrist passes through the middle of the frame.
            if (!right && (handAsset is "fps_hands_uzi" or "fps_hands_shotgun" || handAsset.StartsWith("fps_left_", StringComparison.Ordinal)))
                anchor.X = -.44f;
            Matrix4x4.Invert(transform,out var inverse);
            Vector3 target=Vector3.Transform(anchor,inverse)-bind;
            Raylib.SetShaderValue(shader,wristLoc,bind,ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader,sleeveAxisLoc,axis,ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader,sleevePullLoc,target,ShaderUniformDataType.Vec3);
        }
        if(grip!=0)
        {
            Vector3 socket=right ? handAsset=="fps_hands_uzi" ? new(-.18f,-.19f,.67f) : new(-.18f,-.17f,.61f)
                                 : handAsset=="fps_hands_uzi" ? new(-.183f,-.127f,.84f) : new(-.181f,-.127f,.858f);
            Raylib.SetShaderValue(shader,handSocketLoc,socket,ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader,gripLoc,grip,ShaderUniformDataType.Float);
        }
        for (int i=range.Start; i<range.Start+range.Count; i++)
        {
            Raylib.SetShaderValue(shader,handSurfaceLoc,handSurfaces[(handAsset,i)],ShaderUniformDataType.Int);
            Raylib.DrawMesh(model.Meshes[i],model.Materials[model.MeshMaterial[i]],Matrix4x4.Transpose(transform));
        }
        Raylib.SetShaderValue(shader,handSurfaceLoc,0,ShaderUniformDataType.Int);
        if(grip!=0) Raylib.SetShaderValue(shader,gripLoc,0f,ShaderUniformDataType.Float);
        if(pull) Raylib.SetShaderValue(shader,sleevePullLoc,Vector3.Zero,ShaderUniformDataType.Vec3);
    }

    // Keep the authored contacts while moving/rotating an entire weapon assembly.
    public static void ArmAssembly(bool right, Vector3 offset, string handAsset, Matrix4x4? transform = null,float grip=0)
    {
        Vector3 bind = armWrists[(handAsset,right)];
        Arm(right,new Vector3(-bind.X,bind.Y,bind.Z)+offset,handAsset,transform,grip);
    }

    internal static void MagazineInsert(Matrix4x4 socket,bool fresh)
    {
        Surface(Finish.Imported,0);
        var material=bevel.Materials[0];
        void Stamping(Vector3 center,Vector3 size,Color color)
        {
            var matrix=Matrix4x4.CreateScale(size)*Matrix4x4.CreateTranslation(center)*socket;
            material.Maps[0].Color=color;
            Raylib.DrawMesh(bevel.Meshes[0],material,Matrix4x4.Transpose(matrix));
        }
        var old=material.Maps[0];
        material.Maps[0].Texture=maps[(int)Finish.Imported];
        Stamping(new(0,.055f,0),new(.024f,.087f,.032f),new(40,47,53,255));
        Stamping(new(0,.055f,-.016f),new(.014f,.083f,.0025f),new(58,62,66,255));
        if(fresh) Stamping(new(0,.099f,0),new(.014f,.006f,.023f),new(178,139,76,255));
        material.Maps[0]=old;
    }

    private static Model ModelFor(Mesh mesh)
    {
        Model model = Raylib.LoadModelFromMesh(mesh);
        ApplyShaderToModel(model);
        return model;
    }

    private static void ApplyShaderToModel(Model model)
    {
        for (int m = 0; m < model.MaterialCount; m++)
        {
            model.Materials[m].Shader=shader;
            model.Materials[m].Maps[1].Color=Color.Blank;
            if(shadowTarget.Id!=0) model.Materials[m].Maps[1].Texture=shadowTarget.Depth;
        }
    }

    internal static void AssetOriented(string name,Vector3 pos,float yaw,float pitch,float roll,float scale=1)
    {
        const float rad=MathF.PI/180;
        var transform=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateFromYawPitchRoll(yaw*rad,pitch*rad,roll*rad)
            *Matrix4x4.CreateTranslation(pos);
        Asset(name,Vector3.Zero,assemblyTransform:transform);
    }

    public static void Asset(string name, Vector3 position, float yaw = 0, float scale = 1,
                             Color? tint = null, float tilt = 0, Vector3? stretch = null, bool hideProp = false, Matrix4x4? assemblyTransform = null)
    {
        Model model = imports[name];
        if(CastingShadows)
        {
            var pose=Matrix4x4.CreateRotationX(tilt)*Matrix4x4.CreateScale((stretch ?? Vector3.One)*scale)*
                Matrix4x4.CreateRotationY(yaw*MathF.PI/180)*Matrix4x4.CreateTranslation(position);
            DrawDepth(model,pose*(assemblyTransform ?? Matrix4x4.Identity),hideProp);
            return;
        }
        model.Transform = Matrix4x4.CreateRotationX(tilt);
        Surface(name.StartsWith("char_",StringComparison.Ordinal) ? Finish.Anatomy : Finish.Imported, 0);
        if (assemblyTransform.HasValue)
        {
            var transform = Matrix4x4.CreateRotationX(tilt)
                * Matrix4x4.CreateScale((stretch ?? Vector3.One)*scale)
                * Matrix4x4.CreateRotationY(yaw*MathF.PI/180)
                * Matrix4x4.CreateTranslation(position) * assemblyTransform.Value;
            if(name.StartsWith("weapon_",StringComparison.Ordinal))
            {
                for(int i=0;i<model.MeshCount;i++)
                {
                    var material=model.Materials[model.MeshMaterial[i]];
                    var albedo=material.Maps[0];
                    material.Maps[0].Color=Raylib.ColorTint(albedo.Color,tint ?? Color.White);
                    Vector2 finish=new(material.Maps[(int)MaterialMapIndex.Metalness].Value,
                        Math.Clamp(material.Maps[(int)MaterialMapIndex.Roughness].Value,.12f,1));
                    Raylib.SetShaderValue(shader,weaponFinishLoc,finish,ShaderUniformDataType.Vec2);
                    Raylib.DrawMesh(model.Meshes[i],material,Matrix4x4.Transpose(transform));
                    material.Maps[0]=albedo;
                }
                Raylib.SetShaderValue(shader,weaponFinishLoc,Vector2.Zero,ShaderUniformDataType.Vec2);
                return;
            }
            model.Transform = Matrix4x4.Transpose(transform);
            DrawPaintedModel(model,name,Vector3.Zero,0,Vector3.One,tint ?? Color.White);
            return;
        }
        if (hideProp)
        {
            var transform = model.Transform * Matrix4x4.CreateScale((stretch ?? Vector3.One)*scale)
                * Matrix4x4.CreateRotationY(yaw*MathF.PI/180) * Matrix4x4.CreateTranslation(position);
            for (int i=0;i<model.MeshCount;i++)
                if (!partnerPropMeshes.Contains(i)) Raylib.DrawMesh(model.Meshes[i],model.Materials[model.MeshMaterial[i]],Matrix4x4.Transpose(transform));
            return;
        }
        DrawPaintedModel(model,name,position,yaw,(stretch ?? Vector3.One)*scale,tint ?? Color.White);
        CapturedAssets?.Add(name);
    }

    private static void DrawPaintedModel(Model model,string name,Vector3 pos,float yaw,Vector3 scale,Color tint)
    {
        bool painted=paintedBoothMeshes.Contains(name);
        if(painted) Raylib.SetShaderValue(shader,boothColorLoc,1,ShaderUniformDataType.Int);
        Raylib.DrawModelEx(model,pos,Vector3.UnitY,yaw,scale,tint);
        if(painted) Raylib.SetShaderValue(shader,boothColorLoc,0,ShaderUniformDataType.Int);
    }

    internal static void Workstation(Vector3 feet,Vector2 pose,float yaw)
    {
        var pass=CastingShadows ? shadowShader : shader;
        int loc=CastingShadows ? shadowPodLoc : podLoc;
        Raylib.SetShaderValue(pass,loc,pose,ShaderUniformDataType.Vec2);
        Asset("prop_workstation_full",feet,yaw,SkyscraperFloor1World.WorkstationScale);
        Raylib.SetShaderValue(pass,loc,Vector2.Zero,ShaderUniformDataType.Vec2);
    }

    internal static void Sheet(Vector3 p,float yaw,float pitch,Finish finish=Finish.JiraSheet)
    {
        Quaternion q=Quaternion.CreateFromYawPitchRoll(yaw,pitch,0);
        float angle=2*MathF.Acos(Math.Clamp(q.W,-1,1));
        Vector3 axis=new(q.X,q.Y,q.Z);
        Draw(box,p,new(.210f,.003f,.297f),Color.White,finish,
            axis.LengthSquared()>.000001f ? Vector3.Normalize(axis) : Vector3.UnitY,angle*180/MathF.PI);
    }

    // A rounded unit box with flat face centres and two bevel bands along each edge.
    private static Mesh RoundedBox()
    {
        float[] grid = [-.5f,-.486f,-.45f,.45f,.486f,.5f];
        Mesh mesh = new() { VertexCount = 900, TriangleCount = 300 };
        mesh.AllocVertices(); mesh.AllocNormals(); mesh.AllocTexCoords();
        int i=0;
        void Vertex(Vector3 normal, Vector3 right, Vector3 up, float x, float y)
        {
            Vector3 p=normal*.5f+right*x+up*y;
            Vector3 inner=Vector3.Clamp(p,new(-.45f),new(.45f));
            Vector3 n=Vector3.Normalize(p-inner);
            ((Vector3*)mesh.Vertices)[i]=inner+n*.05f;
            ((Vector3*)mesh.Normals)[i]=n;
            ((Vector2*)mesh.TexCoords)[i++]=new(x+.5f,.5f-y);
        }
        foreach(Vector3 n in new[]{Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ})
        {
            Vector3 right=Vector3.Normalize(Vector3.Cross(Math.Abs(n.Y)>.9f ? Vector3.UnitZ : Vector3.UnitY,n));
            Vector3 up=Vector3.Cross(n,right);
            for(int a=0;a<5;a++) for(int b=0;b<5;b++)
            {
                float x=grid[a], y=grid[b], xx=grid[a+1], yy=grid[b+1];
                Vertex(n,right,up,x,y); Vertex(n,right,up,xx,y); Vertex(n,right,up,xx,yy);
                Vertex(n,right,up,x,y); Vertex(n,right,up,xx,yy); Vertex(n,right,up,x,yy);
            }
        }
        Raylib.UploadMesh(ref mesh,false);
        return mesh;
    }

    public static void World(Camera3D cam, SkyscraperFloor1World world)
    {
        Raylib.SetShaderValue(shader,loadLoc,Math.Clamp((world.Systems.PT+world.Systems.TD)/130f,0,1),ShaderUniformDataType.Float);
        var nearest=world.Systems.Puddles.Where(p=>p.Electric>0).MinBy(p=>Vector3.DistanceSquared(p.Position,cam.Position));
        Raylib.SetShaderValue(shader,puddlePosLoc,(nearest?.Position ?? Vector3.Zero)+Vector3.UnitY*.42f,ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader,puddlePowerLoc,nearest==null ? 0f : 1.28f+.23f*MathF.Sin((float)Raylib.GetTime()*37),ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader,firstPersonLoc,0,ShaderUniformDataType.Int);
        Raylib.SetShaderValue(shader,clockLoc,(float)Raylib.GetTime(),ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader,cabinPowerLoc,1-world.ElevatorGlassOpacity,ShaderUniformDataType.Float);
        View(cam.Position,world.CentralMacBookPos+new Vector3(0,1.6f,.35f),
             world.HrTarotBoothPos+new Vector3(0,1.65f,-.6f),
             world.NorthElevatorPos+new Vector3(0,3.7f,-1),
             world.SouthElevatorDoorPos+new Vector3(0,3.7f,1),world.IsMacBookTrapActive ? 1.4f : .42f);
    }

    public static void SetBeltTension(float amount) =>
        Raylib.SetShaderValue(shader,beltTensionLoc,amount,ShaderUniformDataType.Float);

    internal static void DrawCloud(Vector3 origin,float time,float remaining,float radius,bool steam)
    {
        Rlgl.DisableDepthMask();
        for(int i=0;i<42;i++)
        {
            float t=(time*(steam ? .48f : .29f)+i*.317f)%1;
            float a=i*2.39996f+t*.8f+.22f*MathF.Sin(i*3.1f+2*t);
            float spread=radius*(steam ? .57f : .76f)*t;
            var p=origin+new Vector3(MathF.Cos(a)*spread,(steam ? .32f+2*t : .32f+.54f*t)+.22f*MathF.Sin(a),MathF.Sin(a)*spread);
            float r=.18f+(steam ? .7f : 1.05f)*t;
            int alpha=(int)((steam ? 103 : 145)*MathF.Sin(MathF.PI*t)*Math.Min(1,remaining));
            Oval(p,new(r,r*(steam ? 1.2f : .72f),r),new(223,229,232,alpha),Finish.Cloud);
        }
        Rlgl.EnableDepthMask();
    }

    public static void DrawRosinSmoke(Vector3 tip)
    {
        Rlgl.DisableDepthMask();
        Draw(plane,tip+Vector3.UnitY*.0475f,new(.043f,1,.095f),Color.White,Finish.RosinSmoke,Vector3.UnitX,90);
        Rlgl.EnableDepthMask();
    }

    internal static void FlashLight(Vector3 tip,float strength)
    {
        Raylib.SetShaderValue(shader,muzzleLoc,tip,ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader,flashLoc,strength,ShaderUniformDataType.Float);
    }

    public static void Hands()
    {
        Raylib.SetShaderValue(shader,flashLoc,0f,ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader,firstPersonLoc,1,ShaderUniformDataType.Int);
        Vector3 distant=new(50,50,50);
        Raylib.SetShaderValue(shader,cabinPowerLoc,0f,ShaderUniformDataType.Float);
        View(Vector3.Zero,distant,distant,distant,distant,0);
    }

    private static void View(Vector3 eye,Vector3 laptop,Vector3 tarot,Vector3 north,Vector3 south,float power)
    {
        void Vec(int loc,Vector3 v) => Raylib.SetShaderValue(shader,loc,v,ShaderUniformDataType.Vec3);
        Vec(eyeLoc,eye); Vec(laptopLoc,laptop); Vec(tarotLoc,tarot); Vec(northLoc,north); Vec(southLoc,south);
        Raylib.SetShaderValue(shader,powerLoc,power,ShaderUniformDataType.Float);
    }

    private static void Draw(Model m, Vector3 p, Vector3 scale, Color tint, Finish f, Vector3 axis, float angle=0,float taper=0)
    {
        if(CastingShadows)
        {
            if(f is Finish.Glass or Finish.Contact or Finish.CharacterShadow or Finish.RosinSmoke or Finish.WaterPuddle or Finish.Plasma) return;
            if(taper!=0) Raylib.SetShaderValue(shadowShader,depthTaper,taper,ShaderUniformDataType.Float);
            DrawDepth(m,Matrix4x4.CreateScale(scale)*Matrix4x4.CreateFromAxisAngle(axis,angle*MathF.PI/180)*Matrix4x4.CreateTranslation(p));
            if(taper!=0) Raylib.SetShaderValue(shadowShader,depthTaper,0f,ShaderUniformDataType.Float);
            return;
        }
        Surface(f,taper);
        m.Materials[0].Maps[0].Texture=maps[(int)f];
        Raylib.DrawModelEx(m,p,axis,angle,scale,tint);
    }

    private static void Surface(Finish f, float taper)
    {
        if (f!=activeFinish)
        {
            Raylib.SetShaderValue(shader,finishLoc,(int)f,ShaderUniformDataType.Int);
            activeFinish=f;
        }
        if(taper!=activeTaper)
        {
            Raylib.SetShaderValue(shader,taperLoc,taper,ShaderUniformDataType.Float);
            activeTaper=taper;
        }
    }

    public static void Box(Vector3 p,float w,float h,float d,Color tint,Finish f=Finish.Paint)
        => Draw(f is Finish.Paint or Finish.Plaster or Finish.Lamp ? box : bevel,p,new(w,h,d),tint,f,Vector3.UnitY);
    internal static void DisplayLid(Vector3 hinge,float opening)
    {
        float angle=(8+62*opening)*MathF.PI/180;
        Vector3 center=hinge+new Vector3(0,MathF.Sin(angle),-MathF.Cos(angle))*.29f;
        Rlgl.DisableDepthMask();
        Draw(bevel,center,new(1.46f,.018f,.58f),new(166,201,211,68),Finish.Glass,Vector3.UnitX,angle*180/MathF.PI);
        Rlgl.EnableDepthMask();
    }

    public static void Orb(Vector3 p,float r,Color tint,Finish f=Finish.Paint)
        => Oval(p,new(r),tint,f);
    public static void Oval(Vector3 p,Vector3 radii,Color tint,Finish f=Finish.Paint)
        => Draw(sphere,p,radii,tint,f,Vector3.UnitY);
    public static void Rod(Vector3 start, Vector3 end,float r,float tip,int sides,Color tint,Finish f=Finish.Paint)
    {
        Vector3 d=end-start;
        float len=d.Length();
        if(len<.00001f || Math.Max(r,tip)<=0) return;
        if(r<tip) { Rod(end,start,tip,r,sides,tint,f); return; }
        Vector3 dir=d/len;
        Vector3 axis=Vector3.Cross(Vector3.UnitY,dir);
        axis=axis.LengthSquared()<1e-10f ? Vector3.UnitX : Vector3.Normalize(axis);
        Draw(tube,start,new(r,len,r),tint,f,axis,MathF.Acos(Math.Clamp(dir.Y,-1,1))*180/MathF.PI,tip/r-1);
    }

    public static void Floor(float w,float d) => Draw(plane,Vector3.Zero,new(w,1,d),Color.White,Finish.Slate,Vector3.UnitY);
    public static void Shadow(Vector3 p,float w,float d) => Draw(plane,new(p.X,.006f,p.Z),new(w,1,d),Color.White,Finish.Contact,Vector3.UnitY);

    public static void DrawCharacterDirectionalShadow(Vector3 pos, float height, float radius)
    {
        if(shadowTarget.Id!=0) return;
        Vector3 direction = Vector3.Normalize(new Vector3(.65f, 0, .45f));
        float length = height * 1.7f;
        Vector3 middle = new Vector3(pos.X, Math.Max(.043f, pos.Y + .012f), pos.Z) + direction * (length * .48f);
        Rlgl.DisableDepthMask();
        Draw(plane, middle, new(radius * 3.4f, 1, length), Color.White, Finish.CharacterShadow,
             Vector3.UnitY, MathF.Atan2(direction.X, direction.Z) * 180 / MathF.PI);
        Rlgl.EnableDepthMask();
    }

    public static void OrientedBox(Vector3 p, Vector3 size, float yaw, Color color) =>
        Draw(bevel,p,size,color,Finish.Paint,Vector3.UnitY,yaw*180/MathF.PI);

    // Upload the fixed architecture once. An entire bank of blinds or lit towers
    // then costs one draw per finish, independent of the number of window bays.
    private static readonly List<(Finish Finish, Model Model)> architecture = new();

    internal sealed class ArchitectureBuilder
    {
        private readonly record struct Block(Vector3 Center, Vector3 Size, Color Color, float Yaw);
        private readonly Dictionary<Finish,List<Block>> groups = new();

        public void Add(Vector3 center, Vector3 size, Color color, Finish finish=Finish.Paint, float yaw=0)
        {
            if (!groups.TryGetValue(finish,out var blocks)) groups[finish]=blocks=new();
            blocks.Add(new(center,size,color,yaw));
        }

        public void Upload()
        {
            Mesh cube = box.Meshes[0];
            foreach (var (finish, blocks) in groups)
            {
                Mesh mesh = new() { TriangleCount=blocks.Count*cube.TriangleCount };
                mesh.VertexCount=mesh.TriangleCount*3;
                mesh.AllocVertices(); mesh.AllocNormals(); mesh.AllocTexCoords(); mesh.AllocColors();
                int vertex=0;
                foreach (var block in blocks)
                {
                    var rotation=Matrix4x4.CreateRotationY(block.Yaw);
                    for (int i=0;i<cube.TriangleCount*3;i++,vertex++)
                    {
                        int source=cube.Indices==null ? i : cube.Indices[i];
                        Vector3 local=((Vector3*)cube.Vertices)[source]*block.Size;
                        ((Vector3*)mesh.Vertices)[vertex]=Vector3.Transform(local,rotation)+block.Center;
                        ((Vector3*)mesh.Normals)[vertex]=Vector3.TransformNormal(((Vector3*)cube.Normals)[source],rotation);
                        ((Vector2*)mesh.TexCoords)[vertex]=((Vector2*)cube.TexCoords)[source];
                        ((Color*)mesh.Colors)[vertex]=block.Color;
                    }
                }
                Raylib.UploadMesh(ref mesh,false);
                Model model=ModelFor(mesh);
                model.Materials[0].Maps[0].Texture=maps[(int)finish];
                architecture.Add((finish,model));
            }
        }
    }

    public static void DrawArchitecture(Action<ArchitectureBuilder> build, bool glass=false)
    {
        if (architecture.Count==0)
        {
            var builder=new ArchitectureBuilder();
            build(builder);
            builder.Upload();
        }
        if(CastingShadows)
        {
            foreach(var group in architecture)
                if(group.Finish is not (Finish.Sky or Finish.Facade or Finish.Glass or Finish.Billboard))
                    DrawDepth(group.Model,Matrix4x4.Identity);
            return;
        }
        Raylib.SetShaderValue(shader,batchLoc,1,ShaderUniformDataType.Int);
        if (glass) Rlgl.DisableDepthMask();
        foreach (var group in architecture)
        {
            if ((group.Finish==Finish.Glass)!=glass) continue;
            Surface(group.Finish,0);
            Raylib.DrawModel(group.Model,Vector3.Zero,1,Color.White);
        }
        if (glass) Rlgl.EnableDepthMask();
        Raylib.SetShaderValue(shader,batchLoc,0,ShaderUniformDataType.Int);
    }

    private static int Hash(int x,int y)
    {
        uint h=unchecked((uint)(x*374761393+y*668265263));
        h=(h^(h>>13))*1274126177u;
        return (int)((h^(h>>16))&255);
    }

    private static Texture2D MakeTexture(Finish f)
    {
        int size=f is Finish.Paint or Finish.Contact or Finish.Lamp or Finish.Sky or Finish.Facade
                    or Finish.Glass or Finish.Imported or Finish.Carpet or Finish.CharacterShadow or Finish.Fluorescent or Finish.Anatomy
                    or Finish.BlackMarble or Finish.Bronze or Finish.LiftGlass or Finish.Cloud or Finish.RosinSmoke or Finish.Plasma or Finish.WaterPuddle ? 1 : 512;
        Image img=f==Finish.Lift ? Raylib.GenImageColor(1792,384,new Color(16,25,30,255))
                                 : Raylib.GenImageColor(size,size,Color.White);
        Color* pixels=(Color*)img.Data;
        if(f!=Finish.Lift)
            for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=Pixel(f,x,y);
        void Inscription(string value, int size, int y, Color ink)
        {
            Font native = GameFonts.Get(Typeface.InterfaceBold, Math.Max(1, size / 2));
            int drawSize = native.BaseSize;
            int x = (512 - (int)Raylib.MeasureTextEx(native,value,drawSize,0).X)/2;
            Raylib.ImageDrawTextEx(ref img,native,value,new(x,y),drawSize,0,ink);
        }
        if(f==Finish.Lift)
        {
            Color brass=new(235,195,128,255), ivory=new(253,242,215,255);
            void Letter(string value,int x,int y,int height,Color color)
            {
                Font native = GameFonts.Get(Typeface.InterfaceBold, Math.Max(1, height / 2));
                Raylib.ImageDrawTextEx(ref img,native,value,new(x,y),native.BaseSize,0,color);
            }
            Letter("ЭТАЖ",92,32,76,brass);
            Letter("01",78,104,236,ivory);
            Raylib.ImageDrawRectangle(ref img,496,48,6,288,brass);
            Letter("CLOUD ENTROPIC",548,46,128,ivory);
            Letter("T O W E R",552,204,112,brass);
            Raylib.ImageDrawTriangle(ref img,new(372,155),new(410,102),new(448,155),new Color(111,234,218,255));
            Raylib.ImageDrawTriangle(ref img,new(372,211),new(448,211),new(410,264),brass);
        }
        if(f==Finish.Tarot) Inscription("A R C A N A",48,436,new Color(244,209,146,255));
        if(f is Finish.JiraSheet or Finish.NdaSheet or Finish.PitchDeck)
        {
            bool nda=f==Finish.NdaSheet;
            Raylib.ImageClearBackground(ref img,nda ? new(234,228,207,255) : new(219,224,217,255));
            var ink=new Color(49,55,57,255);
            Inscription(f==Finish.PitchDeck ? "SEED   2026" : nda ? "NDA" : "ЖИРА   B12-409",64,44,ink);
            string[] lines=f==Finish.PitchDeck ? ["Башня Энтропии","План запуска","Раунд инвестиций","Материалы встречи"] : nda ? ["Соглашение","о неразглашении","Отдел кадров","Подпись"] :
                ["Ошибка сборки","Приоритет   P1","Исполнитель","Шаги повтора"];
            for(int i=0;i<lines.Length;i++)
                Raylib.ImageDrawTextEx(ref img,GameFonts.Get(Typeface.Interface,16),lines[i],new(42,154+i*64),24,0,ink);
        }
        if(f is Finish.Exit or Finish.Billboard)
        {
            Raylib.ImageClearBackground(ref img, f==Finish.Exit ? new Color(156,18,39,255) : new Color(27,17,57,255));
            Inscription(f==Finish.Exit ? "ВЫХОД" : "NOVA",96,202,Color.White);
            Raylib.ImageDrawRectangle(ref img,24,165,464,5,Color.White);
            Raylib.ImageDrawRectangle(ref img,24,344,464,5,Color.White);
        }
        Texture2D tex=Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        if(tex.Id==0) throw new InvalidOperationException($"Could not upload {f}.");
        if(size>1) { Raylib.GenTextureMipmaps(ref tex); Raylib.SetTextureFilter(tex,TextureFilter.Anisotropic8X); }
        return tex;
    }

    private static Color Pixel(Finish f,int x,int y)
    {
        int grain=Hash(x,y)%11-5;
        int band=Hash(0,y)%13-6;
        static Color Rgb(float r,float g,float b) => new((int)Math.Clamp(r,0,255),(int)Math.Clamp(g,0,255),(int)Math.Clamp(b,0,255),255);
        switch(f)
        {
            case Finish.Slate:
                float phase=x*.026f+y*.043f+1.7f*MathF.Sin(y*.025f)+MathF.Sin(x*.04f);
                float vein=MathF.Pow(MathF.Max(0,MathF.Cos(phase)),15);
                int seam=Math.Min(x%256,y%256);
                float s=(seam<3 ? -22 : seam==4 ? 10 : 0)+grain+vein*4+Hash(x/256,y/256)%8;
                return Rgb(53+s,58+s,67+s);
            case Finish.Armor:
                int panel=Math.Min(Math.Min(x,511-x),Math.Min(y%256,255-y%256));
                int boltX=Math.Min(x,511-x)-22, boltY=Math.Min(y%256,255-y%256)-18;
                int bolt=boltX*boltX+boltY*boltY;
                if(bolt<37) return Rgb(85-boltY*6,92-boltY*6,97-boltY*6);
                if(y>442 && y<466) return ((x+y)/22)%2==0 ? Rgb(189+grain,133+grain,53) : Rgb(38,39,44);
                float steel=panel<5 ? 41 : panel<8 ? 138 : 96+grain+band+12f*MathF.Cos(x*.016f);
                return Rgb(steel,steel+7,steel+14);
            case Finish.Rack:
                int row=y%64;
                if(row<4 || x<13 || x>498) return Rgb(57+band,66+band,73+band);
                if(x<140 && row>20 && row<28 && x%22<9) return x%44<22 ? Rgb(48,231,162) : Rgb(43,169,218);
                if(Math.Abs(x-405-26*MathF.Sin(y*.023f))<5) return Rgb(57,81,92);
                int mesh=(x+(y/8%2)*4)%9<4 && y%8<5 ? 13 : 45+grain;
                return Rgb(mesh,mesh+5,mesh+10);
            case Finish.Screen:
                if(y<33 || x<66) return Rgb(24,38,52);
                int line=y/20, col=x/12;
                int indent=90+24*(line%4);
                if(y%20>5 && y%20<13 && x>indent && x<indent+76+Hash(line,7)%184 && x%12<10)
                    return ((col/4+line)%3) switch {0=>Rgb(106,222,185),1=>Rgb(95,197,248),_=>Rgb(236,117,182)};
                return Rgb(10,21,31);
            case Finish.Fabric:
                float weave=grain+((x+y)%9<3 ? 15 : -6)+(y%5==0 ? 8 : 0)+9*MathF.Sin(x*.035f+y*.014f);
                return Rgb(161+weave,35+weave*.48f,62+weave*.72f);
            case Finish.Leather:
                float a=(x+y)%128/128f,b=(x-y+512)%128/128f;
                float quilt=MathF.Pow(MathF.Max(0,MathF.Sin(MathF.PI*a)*MathF.Sin(MathF.PI*b)),.42f);
                float l=grain+quilt*41;
                return Rgb(76+l,32+l*.62f,36+l*.48f);
            case Finish.Plaster:
                float concrete=grain*.85f+3*MathF.Sin(y*.023f+x*.017f);
                if(y<3 || x<2) concrete-=33;
                return Rgb(201+concrete,204+concrete,210+concrete);
            case Finish.Tarot:
                float dx=x-256,dy=y-204,r=MathF.Sqrt(dx*dx+dy*dy);
                bool gilded=Math.Abs(r-106)<3 || Math.Abs(r-83)<2 || (r>114 && r<188 && MathF.Cos(32*MathF.Atan2(dy,dx))>.91f);
                if(r<62 && (dx-25)*(dx-25)+(dy+10)*(dy+10)>51*51) gilded=true;
                return gilded ? Rgb(232+grain,185+grain,112+grain) : Rgb(56+grain,28+grain,52+grain);
            case Finish.Lift: return Rgb(25+grain,35+grain,39+grain);
            default: return Color.White;
        }
    }

    public static void Unload()
    {
        foreach(Model m in imports.Values) Raylib.UnloadModel(m);
        imports.Clear();
        partnerPropMeshes.Clear();
        armGroups.Clear();
        armWrists.Clear();
        sleeveEnds.Clear();
        handSurfaces.Clear();
        lastPartPose.Clear();
        reloadMeshes.Clear();
        foreach(var group in architecture) Raylib.UnloadModel(group.Model);
        architecture.Clear();
        foreach(Model m in new[]{box,bevel,plane,tube,sphere}) Raylib.UnloadModel(m);
        foreach(Texture2D t in maps) Raylib.UnloadTexture(t);
        UnloadLightingPipeline();
        Raylib.UnloadShader(shader);
    }
}

internal static unsafe partial class VisualEngine3D
{
    private const int ShadowSize=2048;
    private static readonly Vector3 SunDirection=Vector3.Normalize(new(-.76825f,.21412f,-.60323f));
    private static Shader shadowShader,bloomExtractShader,bloomBlurShader,compositeShader;
    private static RenderTexture2D shadowTarget,sceneRt,bloomRtA,bloomRtB;
    private static Material depthMaterial;
    private static int worldVp,shadowVp,depthTaper,blurStep,bloomSampler,puddlePosLoc,puddlePowerLoc,puddleElectricLoc;
    internal static bool CastingShadows { get; private set; }
    internal static bool MatchesViewport(int w,int h) =>
        sceneRt.Texture.Width==w && sceneRt.Texture.Height==h && bloomRtA.Texture.Width==w/2 &&
        bloomRtA.Texture.Height==h/2 && bloomRtB.Texture.Width==w/2 && bloomRtB.Texture.Height==h/2;
    internal static bool HdrAndShadowsReady => shadowTarget.Id!=0 && sceneRt.Texture.Format==PixelFormat.UncompressedR16G16B16A16;

    [System.Runtime.InteropServices.DllImport("opengl32.dll")]
    private static extern void glDrawBuffer(uint mode);
    [System.Runtime.InteropServices.DllImport("opengl32.dll")]
    private static extern void glReadBuffer(uint mode);

    private static Shader Compile(string vertex,string fragment)
    {
        var pass=Raylib.LoadShaderFromMemory(vertex,fragment);
        if(pass.Id==0 || pass.Id==Rlgl.GetShaderIdDefault()) throw new InvalidOperationException("Lighting shader failed to compile.");
        return pass;
    }

    private static void InitLightingPipeline()
    {
        shadowShader=Compile("""
            #version 330
            in vec3 vertexPosition;
            uniform mat4 lightVP,matModel;
            uniform float taper;
            void main() {
                vec3 p=vertexPosition,n=vec3(0); posePod(p,n); p.xz*=1.+p.y*taper;
                gl_Position=lightVP*matModel*vec4(p,1.);
            }
            """.Replace("void main()",PodGeometry+"\nvoid main()"),"""
            #version 330
            void main() { }
            """);
        shadowVp=Raylib.GetShaderLocation(shadowShader,"lightVP");
        depthTaper=Raylib.GetShaderLocation(shadowShader,"taper");
        worldVp=Raylib.GetShaderLocation(shader,"lightVP");
        puddlePosLoc=Raylib.GetShaderLocation(shader,"puddleLight");
        puddlePowerLoc=Raylib.GetShaderLocation(shader,"puddlePower");
        puddleElectricLoc=Raylib.GetShaderLocation(shader,"puddleElectric");
        shadowTarget.Id=Rlgl.LoadFramebuffer();
        if(shadowTarget.Id!=0)
        {
            shadowTarget.Texture.Width=shadowTarget.Texture.Height=ShadowSize;
            shadowTarget.Depth.Id=Rlgl.LoadTextureDepth(ShadowSize,ShadowSize,false);
            Rlgl.FramebufferAttach(shadowTarget.Id,shadowTarget.Depth.Id,FramebufferAttachType.Depth,FramebufferAttachTextureType.Texture2D,0);
            Rlgl.EnableFramebuffer(shadowTarget.Id);
            if(OperatingSystem.IsWindows()) { glDrawBuffer(0); glReadBuffer(0); }
            if(shadowTarget.Depth.Id==0 || !Rlgl.FramebufferComplete(shadowTarget.Id))
            {
                Rlgl.UnloadFramebuffer(shadowTarget.Id);
                shadowTarget=default;
            }
            Rlgl.DisableFramebuffer();
        }
        if(shadowTarget.Id==0) Console.WriteLine("[RENDER] Depth target unavailable, using contact shadows.");
        shader.Locs[(int)ShaderLocationIndex.MapMetalness]=Raylib.GetShaderLocation(shader,"shadowMap");
        Raylib.SetShaderValue(shader,Raylib.GetShaderLocation(shader,"shadows"),shadowTarget.Id==0 ? 0 : 1,ShaderUniformDataType.Int);
        depthMaterial=Raylib.LoadMaterialDefault();
        depthMaterial.Shader=shadowShader;
        const string header="#version 330\nin vec2 fragTexCoord; uniform sampler2D texture0; out vec4 color;\n";
        bloomExtractShader=Compile(null,header+"""
            void main() {
                vec3 rgb=texture(texture0,fragTexCoord).rgb;
                float l=dot(rgb,vec3(.2126,.7152,.0722));
                // Preserve saturated neon whose luminance is lower than its HDR radiance.
                l=max(l,.66*max(rgb.r,max(rgb.g,rgb.b)));
                float knee=clamp(l-.70,0.,.36);
                float amount=max(knee*knee/.72,l-.88);
                color=vec4(rgb*amount/max(.001,l),1.);
            }
            """);
        bloomBlurShader=Compile(null,header+"""
            uniform vec2 axis;
            void main() {
                vec3 sum=texture(texture0,fragTexCoord).rgb*.227027;
                const float w[4]=float[4](.194595,.121622,.054054,.016216);
                for(int i=1;i<=4;i++) {
                    vec2 d=axis*float(i);
                    sum+=(texture(texture0,fragTexCoord+d).rgb+texture(texture0,fragTexCoord-d).rgb)*w[i-1];
                }
                color=vec4(sum,1.);
            }
            """);
        compositeShader=Compile(null,header+"""
            uniform sampler2D bloom;
            void main() {
                vec2 uv=fragTexCoord, px=1./vec2(textureSize(texture0,0));
                vec3 source=texture(texture0,uv).rgb;
                const vec3 luma=vec3(.2126,.7152,.0722);
                float nw=dot(texture(texture0,uv-px).rgb,luma), ne=dot(texture(texture0,uv+vec2(px.x,-px.y)).rgb,luma);
                float sw=dot(texture(texture0,uv+vec2(-px.x,px.y)).rgb,luma), se=dot(texture(texture0,uv+px).rgb,luma);
                float lo=min(min(nw,ne),min(sw,se)), hi=max(max(nw,ne),max(sw,se));
                if(hi-lo>max(.045,hi*.13)) {
                    vec2 dir=vec2(sw+se-nw-ne,nw+sw-ne-se);
                    dir=clamp(dir/(min(abs(dir.x),abs(dir.y))+max(.012,(lo+hi)*.035)),vec2(-5.),vec2(5.))*px;
                    vec3 edge=.5*(texture(texture0,uv+dir*.22).rgb+texture(texture0,uv-dir*.22).rgb);
                    source=mix(source,edge,.72);
                }
                vec3 x=max(vec3(0.),source+.42*texture(bloom,uv).rgb);
                float l=dot(x,vec3(.2126,.7152,.0722));
                x*=mix(vec3(.97,1.,1.035),vec3(1.035,1.,.97),smoothstep(.18,1.15,l));
                x=(x*(2.51*x+.03))/(x*(2.43*x+.59)+.14);
                vec2 p=fragTexCoord-.5;
                x*=1.-.13*smoothstep(.06,.52,dot(p,p));
                color=vec4(pow(clamp(x,0.,1.),vec3(.84)),1.);
            }
            """);
        blurStep=Raylib.GetShaderLocation(bloomBlurShader,"axis");
        bloomSampler=Raylib.GetShaderLocation(compositeShader,"bloom");
    }

    internal static bool BeginShadowPass(Vector3 focusCenter)
    {
        if(shadowTarget.Id==0) return false;
        Vector3 side=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,SunDirection));
        Vector3 up=Vector3.Cross(SunDirection,side);
        // Fit the complete 76 m floor on the sun's horizontal axis. The vertical
        // axis retains the 68 m span. Follow the focus only inside the spare margin.
        Vector3 floorHalf=new(SkyscraperFloor1World.HalfWidth,SkyscraperFloor1World.CeilingHeight*.5f,SkyscraperFloor1World.HalfDepth);
        Vector3 floorCenter=Vector3.UnitY*floorHalf.Y;
        float spanX=MathF.Max(34,Vector3.Dot(Vector3.Abs(side),floorHalf)+.35f);
        float marginY=34-Vector3.Dot(Vector3.Abs(up),floorHalf)-.25f;
        focusCenter=floorCenter+up*Math.Clamp(Vector3.Dot(focusCenter-floorCenter,up),-marginY,marginY);
        float texel=68f/ShadowSize;
        float Snap(float n)=>MathF.Round(n/texel)*texel-n;
        focusCenter+=side*Snap(Vector3.Dot(focusCenter,side))+up*Snap(Vector3.Dot(focusCenter,up));
        var view=Matrix4x4.CreateLookAt(focusCenter+SunDirection*90,focusCenter,Vector3.UnitY);
        // OpenGL clip depth is -1..1; System.Numerics' default orthographic depth is 0..1.
        Matrix4x4 projection=Matrix4x4.Identity;
        projection.M11=1f/spanX; projection.M22=1f/34;
        projection.M33=-2f/179; projection.M43=-181f/179;
        var lightVP=Matrix4x4.Transpose(view*projection);
        Raylib.SetShaderValueMatrix(shadowShader,shadowVp,lightVP);
        Raylib.SetShaderValueMatrix(shader,worldVp,lightVP);
        Raylib.BeginTextureMode(shadowTarget);
        Raylib.ClearBackground(Color.White);
        Rlgl.EnableDepthTest();
        CastingShadows=true;
        return true;
    }

    private static void DrawDepth(Model model,Matrix4x4 pose,bool hideProp=false,bool droppedLaptop=false)
    {
        var transform=Matrix4x4.Transpose(pose);
        for(int i=0;i<model.MeshCount;i++)
            if(!(hideProp && partnerPropMeshes.Contains(i)) && !(droppedLaptop && model.MeshMaterial[i]>=11))
                Raylib.DrawMesh(model.Meshes[i],depthMaterial,transform);
    }

    internal static void EndShadowPass()
    {
        CastingShadows=false;
        Rlgl.DisableDepthTest();
        Raylib.EndTextureMode();
    }

    private static RenderTexture2D HdrTarget(int w,int h,bool depth)
    {
        RenderTexture2D target=new() { Id=Rlgl.LoadFramebuffer() };
        if(target.Id==0) throw new InvalidOperationException("Could not allocate HDR framebuffer.");
        target.Texture=new Texture2D { Id=Rlgl.LoadTexture(null,w,h,PixelFormat.UncompressedR16G16B16A16,1),
            Width=w,Height=h,Format=PixelFormat.UncompressedR16G16B16A16,Mipmaps=1 };
        Rlgl.FramebufferAttach(target.Id,target.Texture.Id,FramebufferAttachType.ColorChannel0,FramebufferAttachTextureType.Texture2D,0);
        if(depth)
        {
            target.Depth.Id=Rlgl.LoadTextureDepth(w,h,true);
            Rlgl.FramebufferAttach(target.Id,target.Depth.Id,FramebufferAttachType.Depth,FramebufferAttachTextureType.Renderbuffer,0);
        }
        if(target.Texture.Id==0 || depth && target.Depth.Id==0 || !Rlgl.FramebufferComplete(target.Id))
        {
            Raylib.UnloadRenderTexture(target);
            throw new InvalidOperationException("Incomplete HDR framebuffer.");
        }
        Raylib.SetTextureFilter(target.Texture,TextureFilter.Bilinear);
        Raylib.SetTextureWrap(target.Texture,TextureWrap.Clamp);
        return target;
    }

    internal static void BeginScene()
    {
        int w=Math.Max(1,Raylib.GetRenderWidth()),h=Math.Max(1,Raylib.GetRenderHeight());
        var settings=MainMenuAndPauseUI.Display;
        if(MainMenuAndPauseUI.InteractiveDisplay && settings.DisplayMode==DisplayMode.Fullscreen && settings.ResolutionIndex>0)
        {
            var size=DisplaySettings.Sizes[settings.ResolutionIndex];
            float scale=Math.Min((float)size.Width/w,(float)size.Height/h);
            w=Math.Max(1,(int)MathF.Round(w*scale)); h=Math.Max(1,(int)MathF.Round(h*scale));
        }
        if(sceneRt.Texture.Width!=w || sceneRt.Texture.Height!=h)
        {
            ReleaseScene();
            sceneRt=HdrTarget(w,h,true);
            bloomRtA=HdrTarget(Math.Max(1,w/2),Math.Max(1,h/2),false);
            bloomRtB=HdrTarget(bloomRtA.Texture.Width,bloomRtA.Texture.Height,false);
        }
        Raylib.BeginTextureMode(sceneRt);
        Raylib.ClearBackground(new Color(30,33,57,255));
    }

    private static void Blit(Texture2D source,int width,int height)
    {
        Raylib.DrawTexturePro(source,new(0,0,source.Width,-source.Height),new(0,0,width,height),Vector2.Zero,0,Color.White);
    }

    private static void BloomPass(Texture2D source,RenderTexture2D target,Shader pass)
    {
        Raylib.BeginTextureMode(target);
        Raylib.BeginShaderMode(pass);
        Blit(source,target.Texture.Width,target.Texture.Height);
        Raylib.EndShaderMode();
        Raylib.EndTextureMode();
    }

    internal static void Composite()
    {
        Raylib.EndTextureMode();
        BloomPass(sceneRt.Texture,bloomRtA,bloomExtractShader);
        Raylib.SetShaderValue(bloomBlurShader,blurStep,new Vector2(1.65f/bloomRtA.Texture.Width,0),ShaderUniformDataType.Vec2);
        BloomPass(bloomRtA.Texture,bloomRtB,bloomBlurShader);
        Raylib.SetShaderValue(bloomBlurShader,blurStep,new Vector2(0,1.65f/bloomRtB.Texture.Height),ShaderUniformDataType.Vec2);
        BloomPass(bloomRtB.Texture,bloomRtA,bloomBlurShader);
        Raylib.BeginShaderMode(compositeShader);
        Raylib.SetShaderValueTexture(compositeShader,bloomSampler,bloomRtA.Texture);
        Blit(sceneRt.Texture,Raylib.GetScreenWidth(),Raylib.GetScreenHeight());
        Raylib.EndShaderMode();
    }

    internal static void Water(Vector3 center,float electric)
    {
        Raylib.SetShaderValue(shader,puddleElectricLoc,electric,ShaderUniformDataType.Float);
        Rlgl.DisableDepthMask();
        Draw(plane,center,new(5.2f,1,5.2f),Color.White,Finish.WaterPuddle,Vector3.UnitY);
        Rlgl.EnableDepthMask();
    }

    [System.Runtime.InteropServices.DllImport("opengl32.dll")]
    private static extern void glReadPixels(int x,int y,int w,int h,uint format,uint type,float* data);

    internal static void VerifyLightingBuffers()
    {
        if(!HdrAndShadowsReady) throw new InvalidDataException("Expected FP16 and a depth shadow texture.");
        // Read real GPU pixels. A translated occluder must clear the same light-space texel.
        float Depth(float offset)
        {
            BeginShadowPass(Vector3.Zero);
            Box(new(offset,0,0),3,3,3,Color.White);
            float depth;
            glReadPixels(ShadowSize/2,ShadowSize/2,1,1,0x1902,0x1406,&depth);
            EndShadowPass();
            return depth;
        }
        float blocked=Depth(0),clear=Depth(12);
        float Peak(RenderTexture2D buffer)
        {
            Rlgl.EnableFramebuffer(buffer.Id);
            var rgb=new float[buffer.Texture.Width*buffer.Texture.Height*3];
            fixed(float* dst=rgb) glReadPixels(0,0,buffer.Texture.Width,buffer.Texture.Height,0x1907,0x1406,dst);
            Rlgl.DisableFramebuffer();
            return rgb.Max();
        }
        float scene=Peak(sceneRt),bloom=Peak(bloomRtA);
        if(blocked<=0 || blocked>=.99f || clear<=.999f || scene<=1 || bloom<=.01f)
            throw new InvalidDataException($"GPU lighting readback failed: {blocked} {clear} {scene} {bloom}");
        Console.WriteLine($"[AUTOTEST] PASS rendering: moving shadow depth {blocked:F3} -> {clear:F3}, FP16 peak {scene:F2}, blurred bloom {bloom:F2}");
    }

    private static void ReleaseScene()
    {
        foreach(var target in new[]{sceneRt,bloomRtA,bloomRtB})
            if(target.Id!=0) Raylib.UnloadRenderTexture(target);
        sceneRt=bloomRtA=bloomRtB=default;
    }

    private static void UnloadLightingPipeline()
    {
        ReleaseScene();
        if(shadowTarget.Id!=0) Rlgl.UnloadFramebuffer(shadowTarget.Id);
        // UnloadMaterial also owns its custom shader.
        Raylib.UnloadMaterial(depthMaterial);
        Raylib.UnloadShader(compositeShader);
        Raylib.UnloadShader(bloomBlurShader);
        Raylib.UnloadShader(bloomExtractShader);
    }
}
