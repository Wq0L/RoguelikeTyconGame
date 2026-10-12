Shader "ClickerGame/BountyPlantGold"
{
 SubShader
 {
  Tags { "Queue"="Transparent-10" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   ZTest LEqual
   Offset -1, -1
   Cull Back
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 vertex:POSITION;float3 normal:NORMAL;};
   struct V {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;};
   V vert(A v){V o;o.pos=TransformWorldToHClip(TransformObjectToWorld(v.vertex.xyz));o.normal=TransformObjectToWorldNormal(v.normal);return o;}
   half4 frag(V i):SV_Target {half band=dot(normalize(i.normal),normalize(float3(.3,1,-.4)))>.3?1:.76;return half4(half3(1,.76,.15)*band,.38);}
   ENDHLSL
  }
 }
}
