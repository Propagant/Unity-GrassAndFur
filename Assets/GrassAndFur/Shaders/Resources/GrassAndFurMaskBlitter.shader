Shader "Hidden/Grass And Fur Mask Blitter"
{
    Properties
    {
        [HideInInspector] _MainTex("_MainTex", 2D) = "white"{}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue"="Transparent" }
        Lighting Off Cull Off ZTest Always ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGINCLUDE

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            uniform float2 _Point;
            uniform float2 _Dir;
            uniform float _Radius;
            uniform float _Intensity;
            uniform float _Smoothness;
            uniform float _Height;
            uniform half3 _Color;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float CalculateCustomRadius(float radius, float smoothness, float2 uv, float2 coords)
            {
                return 1 - smoothstep(radius, radius + smoothness, length(uv - coords));
            }

            float CalculateRadius(Varyings i)
            {
                return CalculateCustomRadius(_Radius, _Smoothness, i.uv, _Point.xy);
            }

            float CalculateFullBrush(Varyings i)
            {
                return CalculateRadius(i) * _Intensity;
            }

        ENDCG

        Pass
        {
            Name "MaskTex_Additive"
            CGPROGRAM

            float4 frag(Varyings i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv).rgb;
                col.r = saturate(col.r - CalculateFullBrush(i));
                return float4(col, 1);
            }

            ENDCG
        }

        Pass
        {
            Name "MaskTex_DirectSet"
            CGPROGRAM

            float4 frag(Varyings i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                col.r = lerp(col.r, 1 - _Height, CalculateFullBrush(i));
                return col;
            }

            ENDCG
        }

        Pass
        {
            Name "ColorTex_DirectSet"
            CGPROGRAM

            float4 frag(Varyings i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv).rgb;
                col.rgb = lerp(col.rgb, _Color.rgb, CalculateFullBrush(i));
                return float4(col, 1);
            }

            ENDCG
        }

        Pass
        {
            Name "MotionTex_Additive"
            CGPROGRAM

            float4 frag(Varyings i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv).rgb;
                col.rg = saturate(col.rg + (_Dir.xy * 2 * CalculateFullBrush(i)));
                return float4(col, 1);
            }

            ENDCG
        }

        Pass
        {
            Name "MotionTex_DirectSet"
            CGPROGRAM

            float4 frag(Varyings i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv).rgb;
                col.rg = lerp(col.rg, float2(0.5, 0.5), CalculateFullBrush(i));
                return float4(col, 1);
            }

            ENDCG
        }

        Pass
        {
            Name "MotionTex_Clear"
            CGPROGRAM

            float4 frag(Varyings i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv).rgb;
                col.rgb = float3(0.5, 0.5, 1.0);
                return float4(col, 1);
            }

            ENDCG
        }

        Pass
        {
            Name "TrackingFeature_DirectSet"
            CGPROGRAM

            struct TrackingData
            {
                float2 uv;
                float radius;
                float shellCutRadius;
                float radiusSmoothness;
                float shellCutHeight;

                float2 dummy; // Data alignment
            };

            StructuredBuffer<TrackingData> _TrackingData;
            static const int MAX_ELEMENTS = 16;

            float4 frag(Varyings i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv).rgb;

                col.b = 1.0;
                for (int x = 0; x < MAX_ELEMENTS; x++)
                {
                    TrackingData entry = _TrackingData[x];

                    col.rg = lerp(col.rg, (entry.uv - i.uv) + 0.5,
                        CalculateCustomRadius(entry.radius, entry.radiusSmoothness, i.uv, entry.uv)); // Direction

                    col.b = lerp(col.b, entry.shellCutHeight, 
                        CalculateCustomRadius(entry.shellCutRadius, entry.radiusSmoothness, i.uv, entry.uv)); // Shell cut
                }
                return float4(col, 1);
            }

            ENDCG
        }
    }
}
