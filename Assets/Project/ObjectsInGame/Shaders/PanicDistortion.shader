Shader "RecoveryGame/PanicDistortion"
{
    Properties
    {
        _MainTex   ("Texture", 2D) = "white" {}
        _Intensity ("Overall Intensity", Range(0, 1)) = 0
        _ChromaOffset ("Chromatic Aberration", Range(0, 0.05)) = 0.01
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.05)) = 0.01
        _WaveFrequency ("Wave Frequency", Range(0, 50)) = 15
        _WaveSpeed     ("Wave Speed", Range(0, 10)) = 3
        _RedTint       ("Red Tint", Range(0, 0.5)) = 0
        _PulseSpeed    ("Pulse Speed", Range(0, 10)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay+100"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _ChromaOffset;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _RedTint;
                float _PulseSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float intensity = _Intensity;

                // ── Wave distortion ───────────────────────────
                float wave = sin(uv.y * _WaveFrequency + _Time.y * _WaveSpeed) * _WaveAmplitude * intensity;
                float wave2 = cos(uv.x * _WaveFrequency * 0.7 + _Time.y * _WaveSpeed * 1.3) * _WaveAmplitude * intensity * 0.5;
                uv.x += wave;
                uv.y += wave2;

                // ── Chromatic aberration ──────────────────────
                float chromaStrength = _ChromaOffset * intensity;
                float2 direction = uv - float2(0.5, 0.5);
                float dist = length(direction);

                // Stronger at edges (radial)
                float edgeFactor = smoothstep(0.1, 0.7, dist);
                chromaStrength *= edgeFactor;

                half r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + direction * chromaStrength).r;
                half g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).g;
                half b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - direction * chromaStrength).b;
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;

                half4 col = half4(r, g, b, a);

                // ── Pulsing red tint overlay ─────────────────
                float pulse = (sin(_Time.y * _PulseSpeed) + 1.0) * 0.5; // 0→1
                float redAmount = _RedTint * intensity * pulse;
                col.r = saturate(col.r + redAmount);
                col.g = saturate(col.g - redAmount * 0.3);
                col.b = saturate(col.b - redAmount * 0.3);

                // When intensity is 0, make fully transparent so the overlay is invisible
                col.a *= intensity;

                return col;
            }
            ENDHLSL
        }
    }
}
