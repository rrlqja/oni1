Shader "Simulation/GasOverlay"
{
    Properties
    {
        _MainTex ("Gas Data Texture", 2D) = "black" {}
        _NoiseSpeed ("Noise Speed", Range(0, 2)) = 0.3
        _NoiseStrength ("Noise Strength", Range(0, 0.5)) = 0.08
        _NoiseScale ("Noise Scale", Range(1, 20)) = 5.0
        _BlurRadius ("Blur Radius (texels)", Range(0, 3)) = 1.0

        // ── 신규: 구름 텍스처 합성 ──
        _CloudTex ("Cloud Pattern", 2D) = "white" {}
        _CloudScale ("Cloud Scale", Range(0.5, 8)) = 2.0
        _CloudDrift ("Cloud Drift Speed", Range(0, 0.3)) = 0.05
        _CloudContrast ("Cloud Contrast", Range(0.5, 3)) = 1.5
        _EdgeFade ("Edge Fade", Range(0, 2)) = 0.8
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

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float _NoiseSpeed;
                float _NoiseStrength;
                float _NoiseScale;
                float _BlurRadius;
                float4 _CloudTex_ST;
                float _CloudScale;
                float _CloudDrift;
                float _CloudContrast;
                float _EdgeFade;
            CBUFFER_END

            // ================================================================
            //  Gradient Noise (기존 유지)
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

                // ── 1. 노이즈 UV 왜곡 (기존 로직) ──
                float2 noiseUV = uv * _NoiseScale;
                float noiseX = GradientNoise(noiseUV + float2(_Time.y * _NoiseSpeed, 0));
                float noiseY = GradientNoise(noiseUV + float2(0, _Time.y * _NoiseSpeed + 100));
                float2 distortedUV = uv + float2(noiseX - 0.5, noiseY - 0.5) * _NoiseStrength;

                // ── 2. 기체 데이터 샘플링 (블러 포함, 기존 로직) ──
                half4 centerSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV);
                float normalizedMass = centerSample.a;

                float dynamicBlur = _BlurRadius * normalizedMass;
                float2 texelSize = _MainTex_TexelSize.xy * dynamicBlur;

                half4 col = half4(0, 0, 0, 0);
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

                // ── 3. 구름 패턴 합성 (신규) ──
                float2 cloudUV = uv * _CloudScale + _Time.y * float2(_CloudDrift, _CloudDrift * 0.7);
                half cloudRaw = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, cloudUV).r;

                // 대비 조정 (덩어리감)
                half cloud = saturate((cloudRaw - 0.5) * _CloudContrast + 0.5);

                // 밀도(데이터 알파) × 구름 형태 = 최종 알파
                half dataAlpha = col.a;
                half cloudAlpha = dataAlpha * cloud;

                // ── 4. 경계 페이드 (신규) ──
                // 알파가 낮은 영역에서 부드럽게 사라짐
                half finalAlpha = smoothstep(0, _EdgeFade * 0.1, cloudAlpha);

                // 최소 밀도 보장: 데이터 알파가 충분하면 구름이 없는 곳도 약간 보임
                half minVisible = dataAlpha * 0.3;
                finalAlpha = max(finalAlpha, minVisible);

                // ── 5. SpriteRenderer vertex color 반영 ──
                half4 result = half4(col.rgb, finalAlpha);
                result *= input.color;

                return result;
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
