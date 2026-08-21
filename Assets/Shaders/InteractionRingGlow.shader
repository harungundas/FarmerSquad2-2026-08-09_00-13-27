Shader "FarmerSquad/InteractionRingGlow"
{
    // Dokusuz, sadece matematikle cizilen yumusak halka - hicbir asset paketine (ithappy,
    // UrsaAnimation, Cow.fbx) bagimli degil. Quad mesh uzerine uygulanir (UV 0-1 varsayilir),
    // Quad X ekseninde 90 derece dondurulup zemine yatik konur (bkz. sahne kurulumu).
    Properties
    {
        _RingColor ("Ring Color", Color) = (1, 0.92, 0.4, 1)
        _InnerRadius ("Inner Radius (0-0.5)", Range(0, 0.5)) = 0.32
        _OuterRadius ("Outer Radius (0-0.5)", Range(0, 0.5)) = 0.42
        _Softness ("Edge Softness", Range(0.001, 0.2)) = 0.04
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.5
        _PulseAmount ("Pulse Amount (alpha)", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _RingColor;
            float _InnerRadius;
            float _OuterRadius;
            float _Softness;
            float _PulseSpeed;
            float _PulseAmount;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                float dist = length(centered);

                float outerEdge = smoothstep(_OuterRadius, _OuterRadius - _Softness, dist);
                float innerEdge = smoothstep(_InnerRadius - _Softness, _InnerRadius, dist);
                float ringMask = outerEdge * innerEdge;

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                half4 col = _RingColor;
                col.a *= ringMask * saturate(pulse);
                return col;
            }
            ENDHLSL
        }
    }
}
