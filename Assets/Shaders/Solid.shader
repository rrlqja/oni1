Shader "Simulation/Solid"
{
    Properties
    {
        _MainTex ("Element Texture", 2D) = "white" {}
        _CrackTex ("Crack Mask", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _TextureStrength ("Texture Strength", Range(0, 1)) = 0.0
        _CrackStrength ("Crack Strength", Range(0, 1)) = 0.3
        _CrackScale ("Crack Scale", Range(0.1, 4)) = 1.0
        _EdgeDarken ("Edge Darken", Range(0, 0.5)) = 0.15
        _EdgeWidth ("Edge Width", Range(0.02, 0.25)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Solid"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;      // 월드UV
                float2 uv1 : TEXCOORD1;      // 로컬UV
                float2 uv2 : TEXCOORD2;      // edge L,R
                float2 uv3 : TEXCOORD3;      // edge B,T
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldUV : TEXCOORD0;
                float2 localUV : TEXCOORD1;
                float2 edgeLR : TEXCOORD2;
                float2 edgeBT : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_CrackTex);
            SAMPLER(sampler_CrackTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _CrackTex_ST;
                float4 _BaseColor;
                float _TextureStrength;
                float _CrackStrength;
                float _CrackScale;
                float _EdgeDarken;
                float _EdgeWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldUV = input.uv0;
                output.localUV = input.uv1;
                output.edgeLR = input.uv2;
                output.edgeBT = input.uv3;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 luv = input.localUV;

                // ── 1. 원소 텍스처 또는 BaseColor ──
                half3 color;

                if (_TextureStrength > 0.001)
                {
                    // 전용 텍스처가 있는 원소
                    half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.worldUV);
                    // 텍스처 색상을 BaseColor로 약간 tint
                    color = lerp(_BaseColor.rgb, texColor.rgb, _TextureStrength);
                }
                else
                {
                    // 전용 텍스처 없음 → BaseColor 단색
                    color = _BaseColor.rgb;
                }

                // ── 2. 균열 마스크 합성 ──
                if (_CrackStrength > 0.001)
                {
                    float2 crackUV = input.worldUV * _CrackScale;
                    half crackVal = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, crackUV).r;
                    half darkening = (1.0 - crackVal) * _CrackStrength;
                    color *= (1.0 - darkening * 0.5);
                    color += crackVal * _CrackStrength * 0.15;
                }

                // ── 3. 가장자리 어둡게 ──
                float edgeL = input.edgeLR.x;
                float edgeR = input.edgeLR.y;
                float edgeB = input.edgeBT.x;
                float edgeT = input.edgeBT.y;

                float ew = _EdgeWidth;

                float maskL = lerp(1.0, smoothstep(0.0, ew, luv.x), edgeL);
                float maskR = lerp(1.0, smoothstep(0.0, ew, 1.0 - luv.x), edgeR);
                float maskB = lerp(1.0, smoothstep(0.0, ew, luv.y), edgeB);
                float maskT = lerp(1.0, smoothstep(0.0, ew, 1.0 - luv.y), edgeT);

                float edgeMask = maskL * maskR * maskB * maskT;
                color *= lerp(1.0 - _EdgeDarken, 1.0, edgeMask);

                return half4(saturate(color), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
