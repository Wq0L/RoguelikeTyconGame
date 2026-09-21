Shader "UI/ComicCard"
{
 Properties {
  [PerRendererData] _MainTex("Sprite",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  _BodyHue("Body hue",Float)=0.76
  _BodySaturation("Body saturation",Float)=1
  _Accent("Rarity edge",Color)=(0.7,0.35,1,1)
  _Banner("Banner",Color)=(1,0.83,0.3,1)
  _Hue("Hue",Float)=0
  _TargetHue("Palette hue (-1 preserves source)",Float)=-1
  _Saturation("Saturation",Float)=1
  _Brightness("Brightness",Float)=1
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Alpha Clip",Float)=0
 }
 SubShader {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
  Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata {float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct v2f {float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO};
   sampler2D _MainTex; fixed4 _Color, _TextureSampleAdd; float4 _ClipRect;
   float _Hue, _TargetHue, _Saturation, _Brightness, _BodyHue, _BodySaturation; float4 _Accent, _Banner;
   v2f vert(appdata v) {v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.world=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o;}
   float3 rgb2hsv(float3 c) {float4 K=float4(0,-1.0/3,2.0/3,-1); float4 p=lerp(float4(c.bg,K.wz),float4(c.gb,K.xy),step(c.b,c.g)); float4 q=lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r)); float d=q.x-min(q.w,q.y); return float3(abs(q.z+(q.w-q.y)/(6*d+1e-10)),d/(q.x+1e-10),q.x);}
   float3 hsv2rgb(float3 c) {float3 p=abs(frac(c.xxx+float3(0,2.0/3,1.0/3))*6-3); return c.z*lerp(float3(1,1,1),saturate(p-1),c.y);}
   fixed4 frag(v2f i):SV_Target {
    fixed4 c=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
        float3 h=rgb2hsv(c.rgb);
    if(h.z>.22) {
     if(h.x>.5) { h.x=_BodyHue; h.y=saturate(h.y*_BodySaturation); c.rgb=hsv2rgb(h); }
     else if(h.x>.07 && h.x<.22) {
      float banner=step(.82,i.uv.y)+step(.40,i.uv.y)*step(i.uv.y,.50);
      c.rgb=lerp(_Accent.rgb,_Banner.rgb,saturate(banner))*h.z;
     }
    }
    c*=i.color;
    #ifdef UNITY_UI_CLIP_RECT
    c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(c.a-.001);
    #endif
    return c;
   }
   ENDCG
  }
 }
}
