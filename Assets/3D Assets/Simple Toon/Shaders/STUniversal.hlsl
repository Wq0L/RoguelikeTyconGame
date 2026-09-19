// URP port of Simple Toon's stepped lighting and light-facing shine.
#ifndef SIMPLE_TOON_UNIVERSAL_INCLUDED
#define SIMPLE_TOON_UNIVERSAL_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST, _Color, _BaseColor, _ShnColor, _OtlColor, _ToonFlashColor;
float _ColIntense, _ColBright, _AmbientCol, _Segmented, _Steps;
float _StpSmooth, _Offset, _Clipped, _MinLight, _MaxLight, _Lumin;
float _ShnOverlap, _ShnIntense, _ShnRange, _ShnSmooth, _OtlWidth, _OtlWorldWidth, _Cull, _ToonFlash;
CBUFFER_END

struct Attributes {
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings {
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
Varyings ToonVertex(Attributes input) {
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    return output;
}
float ToonLighting(float ndl, float shadow) {
    float offset = clamp(_Offset, -1.0, 1.0);
    float intensity = saturate((ndl + offset) / max(1.0 + offset, 0.0001));
    float steps = _Segmented > 0.5 ? max(floor(_Steps), 1.0) : 1.0;
    float smoothing = _Segmented > 0.5 ? saturate(_StpSmooth) : 1.0;
    float width = rcp(steps);
    float band = ceil(intensity / width);
    float lit = band * width;
    // Handle zero smoothness explicitly: the original used equal smoothstep edges.
    if (smoothing > 0.0001) {
        float start = lit - width;
        float t = saturate((intensity - start) / (width * smoothing));
        float curved = t * t * (3.0 - 2.0 * t);
        float blend = saturate(-(2.0 / ((smoothing + 0.34) * 4.7)) + 1.3);
        float reduce = band == 1.0 ? 1.0 - saturate((_Offset - 1.0) / 0.1) : 1.0;
        lit -= (1.0 - lerp(curved, t, blend)) * reduce * width;
    }
    float dim = saturate(lit) * shadow;
    float maximum = max(_MinLight, _MaxLight);
    if (_Clipped > 0.5) {
        float range = maximum - _MinLight;
        if (range < 0.0001) return _MinLight;
        return clamp(_MinLight + saturate(dim - _MinLight) / range *
            (maximum + _Lumin - _MinLight), _MinLight, maximum + _Lumin);
    }
    return lerp(_MinLight, maximum + _Lumin, dim);
}
half4 ToonFragment(Varyings input) : SV_Target {
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(TOON_TRANSPARENT)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
    #else
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #endif
    Light mainLight = GetMainLight(shadowCoord);
    float ndl = dot(normalize(input.normalWS), mainLight.direction);
    float attenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    half3 baseColor = tex.rgb * lerp(_Color.rgb * _BaseColor.rgb, mainLight.color, _AmbientCol) * _ColIntense + _ColBright;
    // Keep the demo's graphic, display-space bands in a Linear URP project.
    // A linear-space multiply otherwise makes its dark gold bands look washed out.
    float toon = ToonLighting(ndl, attenuation);
    #if defined(UNITY_COLORSPACE_GAMMA)
    half3 result = baseColor * toon;
    #else
    half3 result = SRGBToLinear(LinearToSRGB(max(baseColor, 0.0)) * toon);
    #endif
    // Preserve the asset's N.L highlight (not a PBR specular/Fresnel highlight).
    float len = max(_ShnRange * 2.0, 0.0);
    float distance = abs(ndl - 1.0);
    float shine = step(distance, len);
    if (_ShnSmooth > 0.0001 && len > 0.0001)
        shine *= smoothstep(0.0, 1.0, saturate((len - distance) / (len * _ShnSmooth)));
    shine *= _ShnIntense * lerp(attenuation, 1.0, _ShnOverlap);
    result = lerp(result, _ShnColor.rgb, shine);
    result = lerp(result, _ToonFlashColor.rgb, saturate(_ToonFlash));
    #if defined(TOON_TRANSPARENT)
    return half4(result, tex.a * _Color.a * _BaseColor.a);
    #else
    return half4(result, 1);
    #endif
}
Varyings OutlineVertex(Attributes input) {
    if (_OtlWorldWidth > 0.0) {
        Varyings output = ToonVertex(input);
        output.positionWS += normalize(output.normalWS) * _OtlWorldWidth;
        output.positionCS = TransformWorldToHClip(output.positionWS);
        return output;
    }
    input.positionOS.xyz += normalize(input.normalOS) * _OtlWidth * 0.008;
    return ToonVertex(input);
}
half4 OutlineFragment(Varyings input) : SV_Target {
    UNITY_SETUP_INSTANCE_ID(input);
    clip(max(_OtlWidth, _OtlWorldWidth) - 0.00001);
    return _OtlColor;
}
float3 _LightDirection;
float3 _LightPosition;
Varyings ToonShadowVertex(Attributes input) {
    Varyings output = ToonVertex(input);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirection = normalize(_LightPosition - output.positionWS);
    #else
    float3 lightDirection = _LightDirection;
    #endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, normalize(output.normalWS), lightDirection));
    output.positionCS = ApplyShadowClamping(output.positionCS);
    return output;
}
half4 ToonDepthFragment(Varyings input) : SV_Target { return 0; }
half4 ToonNormalsFragment(Varyings input) : SV_Target {
    float3 normalWS = normalize(input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normalWS);
    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
    #else
    return half4(normalWS, 0);
    #endif
}
#endif
