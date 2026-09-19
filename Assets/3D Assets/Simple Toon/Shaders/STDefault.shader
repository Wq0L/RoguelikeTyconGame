Shader "Simple Toon/SToon Default"
{
    Properties
    {
        [HideInInspector] _ToonFlash ("Hit Flash", Float) = 0
        [HideInInspector] _ToonFlashColor ("Hit Flash Color", Color) = (1,1,1,1)
        _BaseColor ("Gameplay Tint", Color) = (1,1,1,1)
        _OtlWorldWidth ("World Outline Width (0 = original)", Range(0,0.1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _MainTex ("Texture", 2D) = "white" {}

        [Header(Colorize)][Space(5)]  //colorize
		_Color ("Color", COLOR) = (1,1,1,1)

		[HideInInspector] _ColIntense ("Intensity", Range(0,3)) = 1
        [HideInInspector] _ColBright ("Brightness", Range(-1,1)) = 0
		_AmbientCol ("Ambient", Range(0,1)) = 0

        [Header(Detail)][Space(5)]  //detail
        [Toggle] _Segmented ("Segmented", Float) = 1
        _Steps ("Steps", Range(1,25)) = 3
        _StpSmooth ("Smoothness", Range(0,1)) = 0
        _Offset ("Lit Offset", Range(-1,1.1)) = 0

        [Header(Light)][Space(5)]  //light
        [Toggle] _Clipped ("Clipped", Float) = 0
        _MinLight ("Min Light", Range(0,1)) = 0
        _MaxLight ("Max Light", Range(0,1)) = 1
        _Lumin ("Luminocity", Range(0,2)) = 0

        [Header(Shine)][Space(5)]  //shine
		[HDR] _ShnColor ("Color", COLOR) = (1,1,0,1)
        [Toggle] _ShnOverlap ("Overlap", Float) = 0

        _ShnIntense ("Intensity", Range(0,1)) = 0
        _ShnRange ("Range", Range(0,1)) = 0.15
        _ShnSmooth ("Smoothness", Range(0,1)) = 0
        [HideInInspector] _OtlWidth ("Outline Width", Float) = 0
        [HideInInspector] _OtlColor ("Outline Color", Color) = (0,0,0,1)
    }

    SubShader {
        Cull [_Cull]
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            
            #include "STUniversal.hlsl"
            ENDHLSL
        }
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonShadowVertex
            #pragma fragment ToonDepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            
            #include "STUniversal.hlsl"
            ENDHLSL
        }
        Pass {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVertex
            #pragma fragment ToonDepthFragment
            #pragma multi_compile_instancing
            
            
            #include "STUniversal.hlsl"
            ENDHLSL
        }
        Pass {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVertex
            #pragma fragment ToonNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            
            #include "STUniversal.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
