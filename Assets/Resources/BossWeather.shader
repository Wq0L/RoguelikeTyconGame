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
            float flakes(float2 uv, float scale, float speed)
            {
                float2 p=uv*scale+float2(_WeatherTime*.15,_WeatherTime*speed);
                float2 cell=floor(p), f=frac(p);
                float seed=hash(cell);
                float2 centre=float2(.2+.6*seed,.2+.6*hash(cell+37));
                centre.x+=sin(_WeatherTime*.8+seed*6.28)*.08;
                return (1-smoothstep(.025,.075,length(f-centre)))*step(.55,seed);
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
                    float snow=flakes(p,14,.65)+flakes(p+19,23,1.05)*.55;
                    // Cool perimeter, clear central field. Noise breaks up the frost softly.
                    float rim=smoothstep(.28,.68,length(uv-.5));
                    float frost=noise(p*9+float2(t*.018,0));
                    float ice=rim*(.36+.12*frost);
                    float flakeAlpha=saturate(snow*.65);
                    float alpha=flakeAlpha+ice*(1-flakeAlpha);
                    float3 tint=(float3(.88,.96,1)*flakeAlpha+float3(.24,.65,.92)*ice*(1-flakeAlpha))/max(alpha,.0001);
                    return float4(tint, alpha*_Strength);
                }
                float dust=noise(float2(p.x*3-t*.32,p.y*14+t*.08));
                float streak=noise(float2(p.x*12-t*2,p.y*160));
                float rim=smoothstep(.28,.68,length(uv-.5));
                float sand=.025+dust*.09+pow(streak,8)*.16;
                float border=rim*(.34+.12*dust);
                float alpha=sand+border*(1-sand);
                float3 tint=(float3(.77,.59,.35)*sand+float3(.76,.40,.12)*border*(1-sand))/max(alpha,.0001);
                return float4(tint, alpha*_Strength);
            }
            ENDCG
        }
    }
}
