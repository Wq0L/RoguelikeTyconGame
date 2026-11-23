// Run sonu kararması (22 Kasım 2026; kullanıcı: "öldüğümüzde hissedilsin, siyahlaşan bir shader; sağ, sol, aşağı, yukarı, çaprazlardan").
// Kitin diliyle (Docs/UI-Dili.md: tram, düz basılı renk, glow / gradyan yok): mürekkep rengi karanlık ekranın dört kenarından ve
// köşelerinden içeri doğru tram noktalarıyla kapanır; noktalar kenara yaklaştıkça büyür, en dışta düz mürekkep olur. Ortada sahne
// hafif loş kalır (_BaseDim). _Progress 0 → 1: kapanma (RunEndVignette, ölçeksiz zamanla, bir kez). Tam ekran UI Image'ı (uv = ekran).
// Biçim: kare-daire (superellipse, _Power) → kenar ortalarından ve köşelerden birlikte gelir. Tek geçiş, doku yok, dal yok.
Shader "UI/HalftoneVignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint (ink comes from the Image colour)", Color) = (1, 1, 1, 1)
        _Progress ("Progress", Range(0, 1)) = 1
        _BaseDim ("Base Dim (centre)", Range(0, 1)) = 0.5
        _Reach ("Reach (how far inward)", Range(0, 1.5)) = 0.78
        _Feather ("Halftone band width", Range(0.05, 1)) = 0.42
        _Cells ("Halftone dots per screen height", Range(10, 120)) = 40
        _Power ("Corner shape (2 round, 4 squarer)", Range(1.5, 6)) = 3

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _Progress, _BaseDim, _Reach, _Feather, _Cells, _Power;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 p = abs(i.uv - 0.5) * 2.0;                                   // 0 merkez, 1 kenar ortası
                float d = pow(pow(p.x, _Power) + pow(p.y, _Power), 1.0 / _Power);   // kare-daire uzaklığı (köşelerde > 1)
                float edge = 1.0 - _Reach * _Progress;                              // karanlığın iç sınırı içeri yürür
                float a = saturate((d - edge) / _Feather);                          // 0 içeride, 1 kenarda

                // Tram: ekran yüksekliğine göre ızgara (kare hücre), her ikinci satır yarım hücre kayık.
                float2 grid = float2(i.uv.x * aspect, i.uv.y) * _Cells;
                grid.x += step(0.5, frac(grid.y * 0.5)) * 0.5;
                float2 cell = frac(grid) - 0.5;
                float radius = 0.74 * sqrt(a);                                      // 0.74: noktalar birleşir (düz mürekkep)
                float edgeAA = fwidth(length(cell)) * 1.2;
                float dotInk = 1.0 - smoothstep(radius - edgeAA, radius + edgeAA, length(cell));
                float ink = max(dotInk * step(0.002, a), step(0.985, a));

                float alpha = max(_BaseDim, ink) * _Progress;
                return fixed4(i.color.rgb, alpha * i.color.a);
            }
            ENDCG
        }
    }
}
