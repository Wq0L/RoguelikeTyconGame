// Parçacık rengini (vertex color) doğrudan gösteren opak unlit shader. Rarity hasat patlaması kullanır:
// hit partikülündeki URP/Lit malzemesi vertex rengini yok saydığı için tüm parçacıklar tek renk çıkıyordu.
Shader "ClickerGame/ParticleVertexColor"
{
    Properties
    {
        _Brightness ("Brightness", Range(0.5, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Unity parçacık rengini lineer projede zaten kendisi çevirir (ResonanceBurst da böyle kullanır);
                // burada tekrar çevirmek renkleri koyulaştırıp turuncuyu kırmızıya kaydırıyordu.
                return half4(input.color.rgb * _Brightness, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
