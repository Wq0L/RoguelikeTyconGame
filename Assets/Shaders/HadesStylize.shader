// "Hades geçişi" prototipi (11 Kasım 2026, kullanıcı: "Hades seviyesi tooniklik"; yalnız izole kopyada, menüde denenir).
// Tek tam ekran geçişi (URP FullScreenPassRendererFeature), sahnedeki her şeye aynı kuralla:
// - Mürekkep siluet çizgisi: derinlik farkından, nesnenin DIŞINA çizilir (oyunun kendi konturu gibi; ince yapraklar
//   mürekkebe boğulmaz). Kalınlık 1080p'ye göre piksel.
// - İç kıvrım çizgisi: yalnız gerçek kırışıklarda (normal farkı büyük + az da olsa derinlik basamağı), ince.
// - Renkli gölge: ışığa bakmayan taraf koyulaşmakla kalmaz, mora / laciverte kayar (arka yüz normalleri kameraya çevrilir).
// - Tarama: gölge tarafında ekran çaprazı çizgi roman taraması.
// - Kenar ışığı: ekran-uzayı; ışık yönündeki komşu arka plansa silüet üstünde ince sıcak bant.
// - Hafif renk canlılığı.
// Uzak arka plan (uzay dörtgeni) gölgelenmez; ekran üstü arayüz etkilenmez. Kamera post-processing'i gerekmez.
Shader "Hidden/HadesStylize"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.08, 0.05, 0.1, 1)
        _Thickness ("Silhouette px at 1080p", Float) = 2.2
        _InnerThickness ("Crease px at 1080p", Float) = 1.0
        _DepthSens ("Depth edge (relative)", Float) = 0.04
        _NormalSens ("Crease (1 - cos)", Float) = 0.45
        _LightDir ("Direction toward light", Vector) = (0.4, 0.8, -0.3, 0)
        _LitThreshold ("Lit threshold (N.L)", Float) = 0.1
        _ShadowTint ("Shadow tint", Color) = (0.62, 0.52, 0.95, 1)
        _ShadowStrength ("Shadow tint strength", Range(0, 1)) = 0.75
        _HatchSpacing ("Hatch spacing px at 1080p", Float) = 7
        _HatchStrength ("Hatch strength", Range(0, 1)) = 0.35
        _RimColor ("Rim colour", Color) = (1, 0.8, 0.55, 1)
        _RimStrength ("Rim strength", Range(0, 1)) = 0.55
        _RimWidth ("Rim px at 1080p", Float) = 3
        _Saturation ("Saturation", Float) = 1.15
        _MaxDepth ("Max eye depth (background excluded)", Float) = 400
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "HadesStylize"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _InkColor, _LightDir, _ShadowTint, _RimColor;
            float _Thickness, _InnerThickness, _DepthSens, _NormalSens, _LitThreshold, _ShadowStrength, _HatchSpacing, _HatchStrength,
                  _RimStrength, _RimWidth, _Saturation, _MaxDepth;

            float EyeAt(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float raw = SampleSceneDepth(uv);
                float eye = LinearEyeDepth(raw, _ZBufferParams);
                float2 px = 1.0 / _ScreenParams.xy;
                float scale = _ScreenParams.y / 1080.0;

                // Siluet: bu piksel UZAKTA ve yakınında belirgin derecede YAKIN bir nesne var -> nesnenin dışına mürekkep.
                // 8 yön, kalınlık çemberi; arka plan pikselleri de çizgi alabilir (siluet uzaya karşı da kapanır).
                float t = _Thickness * scale;
                float silhouette = 0;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float a = k * 0.785398;
                    float2 o = float2(cos(a), sin(a)) * t;
                    float ek = EyeAt(uv + o * px);
                    silhouette = max(silhouette, step(_DepthSens, (eye - ek) / ek));
                }
                if (eye > _MaxDepth) return half4(lerp(col, _InkColor.rgb, silhouette), 1);

                float3 n = normalize(SampleSceneNormals(uv));
                float3 posWS = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 V = normalize(GetCameraPositionWS() - posWS);
                if (dot(n, V) < 0) n = -n;   // çift yüzlü yaprakların arka yüzü

                // İç kıvrım: normal farkı büyük ve aynı yüzeyde (derinlik farkı küçük) -> ince çizgi.
                float ti = _InnerThickness * scale;
                float crease = 0;
                float2 io[2] = { float2(ti, 0), float2(0, ti) };
                [unroll] for (int j = 0; j < 2; j++)
                {
                    float2 uvj = uv + io[j] * px;
                    float ej = EyeAt(uvj);
                    if (abs(ej - eye) / eye > _DepthSens) continue;   // siluet zaten çiziliyor
                    float3 nj = normalize(SampleSceneNormals(uvj));
                    float3 posJ = ComputeWorldSpacePosition(uvj, SampleSceneDepth(uvj), UNITY_MATRIX_I_VP);
                    if (dot(nj, normalize(GetCameraPositionWS() - posJ)) < 0) nj = -nj;
                    crease = max(crease, step(_NormalSens, 1 - saturate(dot(n, nj))));
                }

                float3 L = normalize(_LightDir.xyz);
                float ndl = dot(n, L);
                float shadow = 1 - smoothstep(_LitThreshold - 0.04, _LitThreshold + 0.04, ndl);

                // Kenar ışığı: ekranda ışığa doğru küçük adım arka plana / çok uzağa düşüyorsa silüetin ışık tarafı.
                float3 lightVS = mul((float3x3)UNITY_MATRIX_V, L);
                float2 toLight = normalize(lightVS.xy + 1e-4);
                float er = EyeAt(uv + toLight * _RimWidth * scale * px);
                float rim = step(0.12, (er - eye) / eye) * (1 - shadow * 0.5);

                float luma = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(luma.xxx, col, _Saturation);
                col = lerp(col, col * _ShadowTint.rgb, shadow * _ShadowStrength);
                float2 pix = uv * _ScreenParams.xy;
                float spacing = max(2.0, _HatchSpacing * scale);
                float hatch = step(frac((pix.x + pix.y) / spacing), 0.22);
                col = lerp(col, _InkColor.rgb, hatch * shadow * _HatchStrength);
                col = lerp(col, _RimColor.rgb, rim * _RimStrength);
                col = lerp(col, _InkColor.rgb, max(silhouette, crease * 0.85));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
