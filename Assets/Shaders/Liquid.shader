Shader "Simulation/Liquid"
{
    Properties
    {
        _PatternTex ("Pattern Texture", 2D) = "gray" {}
        _PatternScale ("Pattern Scale", Range(0.5, 8)) = 1.0
        _PatternStrength ("Pattern Strength", Range(0, 0.4)) = 0.12
        _FlowSpeedX ("Flow Speed X", Range(-1, 1)) = 0.15
        _FlowSpeedY ("Flow Speed Y", Range(-1, 1)) = -0.05
        _SurfaceHighlight ("Surface Highlight Brightness", Range(0, 0.5)) = 0.2
        _SurfaceWidth ("Surface Highlight Width", Range(0.02, 0.2)) = 0.08
        _EdgeDarken ("Edge Darken Amount", Range(0, 0.3)) = 0.08
        _EdgeWidth ("Edge Darken Width", Range(0.02, 0.2)) = 0.06
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
            Name "Liquid"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;   // BaseColor from vertex color
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;      // 셀 내 로컬 UV (가장자리/표면 판정)
                float2 worldUV : TEXCOORD1;  // 월드 좌표 기반 UV (패턴 심리스 타일링)
                float4 color : COLOR;
            };

            TEXTURE2D(_PatternTex);
            SAMPLER(sampler_PatternTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _PatternTex_ST;
                float _PatternScale;
                float _PatternStrength;
                float _FlowSpeedX;
                float _FlowSpeedY;
                float _SurfaceHighlight;
                float _SurfaceWidth;
                float _EdgeDarken;
                float _EdgeWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.worldUV = input.positionOS.xy;  // 오브젝트 좌표 = 그리드 좌표
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = input.color;
                float2 uv = input.uv;

                // UV 인코딩 규칙:
                //   uv.x = 셀 내 수평 위치 (0~1)
                //   uv.y:
                //     0~0.49 범위 = 비표면 셀 (전체 채움)
                //     0.50~1.0 범위 = 표면 셀 (상단이 수면)
                //     정확한 값 = 채움 비율

                bool isSurface = uv.y >= 0.5;
                float fillHeight = isSurface ? (uv.y - 0.5) * 2.0 : 1.0;

                // ── 1. 패턴 텍스처 합성 ──
                // worldUV로 타일링하여 셀 경계에서 패턴이 끊기지 않음
                float2 patternUV = input.worldUV * _PatternScale + _Time.y * float2(_FlowSpeedX, _FlowSpeedY);
                half patternVal = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, patternUV).r;

                // 패턴 변조: 0.5 중심으로 밝기 조절
                half patternOffset = (patternVal - 0.5) * _PatternStrength * 2.0;
                half3 color = baseColor.rgb + patternOffset;

                // ── 2. 표면 하이라이트 ──
                if (isSurface)
                {
                    // fillHeight 상단 근처에서 밝은 라인
                    float distFromTop = 1.0 - (uv.y - 0.5) * 2.0;  // 0=상단, 1=하단
                    float highlight = smoothstep(_SurfaceWidth, 0, distFromTop);
                    color += highlight * _SurfaceHighlight;
                }

                // ── 3. 좌우 가장자리 어둡게 (벽 경계 효과) ──
                float edgeL = smoothstep(0, _EdgeWidth, uv.x);
                float edgeR = smoothstep(0, _EdgeWidth, 1.0 - uv.x);
                float edgeFactor = edgeL * edgeR;
                color *= lerp(1.0 - _EdgeDarken, 1.0, edgeFactor);

                // ── 4. 하단 약간 어둡게 (깊이감) ──
                float depthFactor = lerp(0.95, 1.0, uv.y * 0.5 + 0.5);
                color *= depthFactor;

                return half4(saturate(color), baseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
