Shader "ClickerGame/BossWeather"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _Weather ("Weather", Float) = 0
        _WeatherTime ("Time", Float) = 0
        _Strength ("Strength", Range(0,1)) = 0
        _Aspect ("Aspect", Float) = 1.7778
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float _Weather, _WeatherTime, _Strength, _Aspect;
            output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            fixed4 frag(output i) : SV_Target
            {
                float2 uv=i.uv, p=uv*float2(_Aspect,1);
                float edge=smoothstep(.12,.7,length(uv-.5));
                float t=_WeatherTime;
                if (_Weather < 1.5)
                {
                    float cloud=noise(p*3+float2(t*.035,t*.008))*.65+noise(p*7-float2(t*.02,0))*.35;
                    float rim=smoothstep(.28,.68,length(uv-.5));
                    float mist=0.025+cloud*.10;
                    float border=rim*(.32+.14*cloud);
                    float alpha=mist+border*(1-mist);
                    float3 tint=(float3(.76,.83,.87)*mist+float3(.52,.49,.67)*border*(1-mist))/max(alpha,.0001);
                    return float4(tint, alpha*_Strength);
                }
                if (_Weather < 2.5)
                {
                    // Tipi (A1, 5 Kasım 2026, kullanıcı: kar taneleri yıldızlarla karışıyor, kum fırtınası gibi olsun). Nokta yok:
                    // rüzgâr yönünde (sağdan sola, hafif aşağı) uzun ince kar çizgileri + dalga dalga geçen kar pusu; rüzgâr hamlelerle
                    // güçlenip zayıflar. Tarlanın ortası açık kalır: bitkiler ve darbeler örtülmez.
                    float2 w=float2(p.x*.94+p.y*.34, p.y*.94-p.x*.34);
                    float gust=.65+.35*noise(float2(t*.35,3.7));
                    float drift=noise(float2(w.x*2.2+t*.55,w.y*7+t*.05))*.6+noise(float2(w.x*5+t*.9,w.y*16))*.4;
                    float streakA=pow(noise(float2(w.x*9+t*3.4,w.y*110)),9);
                    float streakB=pow(noise(float2(w.x*15+t*5.2,w.y*190+11)),11);
                    float clearField=lerp(.25,1,smoothstep(.12,.5,length(uv-.5)));
                    float snowA=saturate((.03+drift*.11*gust+streakA*.30*gust+streakB*.22)*clearField);
                    float3 snowColor=lerp(float3(.74,.86,.95),float3(.95,.98,1),saturate((streakA+streakB)*2));
                    float flakeAlpha=snowA;
                    float3 flakeColor=snowColor;
                    // Cool perimeter, clear central field. Noise breaks up the frost softly.
                    float rim=smoothstep(.28,.68,length(uv-.5));
                    float frost=noise(p*9+float2(t*.018,0));
                    float ice=rim*(.36+.12*frost);
                    float alpha=flakeAlpha+ice*(1-flakeAlpha);
                    float3 tint=(flakeColor*flakeAlpha+float3(.24,.65,.92)*ice*(1-flakeAlpha))/max(alpha,.0001);
                    return float4(tint, alpha*_Strength);
                }
                // Kum fırtınası (A2, 5 Kasım 2026: Don tipisiyle aynı yapı, kullanıcı onaylı). Rüzgâr soldan sağa, hafif aşağı:
                // uzun kum çizgileri iki hızda + hamlelerle dalgalanan toz pusu. Tarlanın ortası açık: bitkiler ve darbeler örtülmez.
                float2 w=float2(p.x*.97-p.y*.24, p.y*.97+p.x*.24);
                float gust=.65+.35*noise(float2(t*.32,8.1));
                float dust=noise(float2(w.x*2-t*.6,w.y*6+t*.04))*.6+noise(float2(w.x*4.5-t,w.y*15))*.4;
                float streakA=pow(noise(float2(w.x*8-t*3,w.y*100)),8);
                float streakB=pow(noise(float2(w.x*13-t*4.6,w.y*170+5)),10);
                float clearField=lerp(.25,1,smoothstep(.12,.5,length(uv-.5)));
                float sand=saturate((.03+dust*.11*gust+streakA*.28*gust+streakB*.2)*clearField);
                float3 sandColor=lerp(float3(.77,.59,.35),float3(.95,.82,.6),saturate((streakA+streakB)*2));
                float rim=smoothstep(.28,.68,length(uv-.5));
                float border=rim*(.34+.12*dust);
                float alpha=sand+border*(1-sand);
                float3 tint=(sandColor*sand+float3(.76,.40,.12)*border*(1-sand))/max(alpha,.0001);
                return float4(tint, alpha*_Strength);
            }
            ENDCG
        }
    }
}
