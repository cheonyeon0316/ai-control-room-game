Shader "ControlRoom/CCTVFeed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Camera feed", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Saturation ("Saturation", Range(0,1)) = 0.72
        _Noise ("Noise", Range(0,0.1)) = 0.025
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;
            float _Saturation;
            float _Noise;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            float NoiseAt(float2 samplePosition)
            {
                return frac(sin(dot(samplePosition, float2(12.9898, 78.233))) * 43758.5453);
            }
            fixed4 frag(v2f input) : SV_Target
            {
                float tick = floor(_Time.y * 12.0);
                // A short, narrow disturbance every eleven seconds; never hide a full frame.
                float glitch = step(10.91, fmod(_Time.y, 11.0));
                float band = step(0.94, NoiseAt(float2(floor(input.uv.y * 20.0), tick)));
                float2 uv = input.uv;
                uv.x += glitch * band * _MainTex_TexelSize.x;
                fixed4 source = tex2D(_MainTex, uv);
                // Subpixel colour separation retains labels and small object silhouettes.
                float shift = _MainTex_TexelSize.x * 0.25;
                float3 rgb = float3(tex2D(_MainTex, uv + float2(shift,0)).r, source.g, tex2D(_MainTex, uv - float2(shift,0)).b);
                float grey = dot(rgb, float3(0.2126, 0.7152, 0.0722));
                rgb = lerp(grey.xxx, rgb, _Saturation);
                rgb += (NoiseAt(floor(uv * float2(640,360)) + tick) - 0.5) * _Noise;
                fixed4 color = fixed4(saturate(rgb), source.a) * input.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
