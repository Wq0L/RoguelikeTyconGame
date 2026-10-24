Shader "ClickerGame/ThornComicToon"
{
    Properties
    {
        _Opacity ("Opacity", Range(0,1)) = 1
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
        _ToonFlash ("Hit flash", Range(0,1)) = 0
        _ToonFlashColor ("Hit flash color", Color) = (1,1,1,1)
        _Ink ("Ink", Color) = (.10,.065,.12,1)
        _OutlineWidth ("Outline width", Float) = 0
        _InkMode ("Ink silhouette", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Ink;
                half4 _ToonFlashColor;
                float _ToonFlash;
                float _Opacity;
                float _OutlineWidth;
                float _InkMode;
                float _Cull;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; half4 color:COLOR; };
            V vert(A v)
            {
                V o;
                // Radial hull keeps hard-edged authored meshes connected at the outline.
                float3 p = v.positionOS.xyz + normalize(v.positionOS.xyz + float3(0,.001,0)) * _OutlineWidth;
                o.positionCS = TransformObjectToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }
            half4 frag(V i):SV_Target
            {
                Light light = GetMainLight();
                float n = dot(normalize(i.normalWS), light.direction);
                half band = n > .55 ? 1.0 : n > -.12 ? .8 : .57;
                half3 paper = i.color.rgb * band;
                paper = lerp(paper, _ToonFlashColor.rgb, saturate(_ToonFlash));
                return half4(lerp(paper, _Ink.rgb, _InkMode), saturate(_Opacity));
            }
            ENDHLSL
        }
    }
}

