Shader "Simulation/GasOverlay"
{
    Properties
    {
        _MainTex ("Gas Data Texture", 2D) = "black" {}
        _NoiseSpeed ("Noise Speed", Range(0, 2)) = 0.3
        _NoiseStrength ("Noise Strength", Range(0, 0.5)) = 0.08
        _NoiseScale ("Noise Scale", Range(1, 20)) = 5.0
        _BlurRadius ("Blur Radius (texels)", Range(0, 3)) = 1.0
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
            Name "GasOverlay"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;  // (1/width, 1/height, width, height)
                float _NoiseSpeed;
                float _NoiseStrength;
                float _NoiseScale;
                float _BlurRadius;
            CBUFFER_END

            // ================================================================
            //  Simplex-like gradient noise (2D)
            //  GPU 친화적인 해시 기반 노이즈
            // ================================================================

            float2 GradientNoiseDir(float2 p)
            {
                p = p % 289;
                float x = (34 * p.x + 1) * p.x % 289 + p.y;
                x = (34 * x + 1) * x % 289;
                x = frac(x / 41) * 2 - 1;
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }

            float GradientNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(GradientNoiseDir(ip), fp);
                float d01 = dot(GradientNoiseDir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(GradientNoiseDir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(GradientNoiseDir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
                return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
            }

            // ================================================================
            //  Vertex
            // ================================================================

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            // ================================================================
            //  Fragment
            // ================================================================

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // ── 0. 원본 알파(질량 비율)를 먼저 읽어 왜곡 강도 결정 ──
                half4 rawSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float massIntensity = rawSample.a;  // 0(소량) ~ maxAlpha(대량)
                // 정규화: 0~1 범위로 (maxAlpha 이하이므로)
                float normalizedMass = saturate(massIntensity * 2.0);

                // ── 1. Noise UV 왜곡: 질량이 많을수록 강한 흔들림 ──
                float time = _Time.y * _NoiseSpeed;
                float2 noiseUV = uv * _NoiseScale;

                // 질량 기반 왜곡 강도: 소량이면 거의 0, 대량이면 _NoiseStrength
                float dynamicStrength = _NoiseStrength * normalizedMass * normalizedMass;

                float2 distortion = float2(
                    GradientNoise(noiseUV + float2(0, time)) - 0.5,
                    GradientNoise(noiseUV + float2(100, time + 50)) - 0.5
                ) * dynamicStrength;

                float2 distortedUV = clamp(uv + distortion, 0.001, 0.999);

                // ── 2. 블러: 질량이 많을수록 넓은 확산 ──
                float dynamicBlur = _BlurRadius * normalizedMass;
                float2 texelSize = _MainTex_TexelSize.xy * dynamicBlur;

                half4 col = half4(0, 0, 0, 0);

                // 9-tap 박스 블러
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV + float2(-texelSize.x, -texelSize.y));
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV + float2(0, -texelSize.y));
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV + float2(texelSize.x, -texelSize.y));

                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV + float2(-texelSize.x, 0));
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV);
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV + float2(texelSize.x, 0));

                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV + float2(-texelSize.x, texelSize.y));
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV + float2(0, texelSize.y));
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV + float2(texelSize.x, texelSize.y));

                col /= 9.0;

                // ── 3. SpriteRenderer vertex color 반영 ──
                col *= input.color;

                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
