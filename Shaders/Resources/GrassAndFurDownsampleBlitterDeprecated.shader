Shader "Hidden/Grass And Fur/GrassAndFurDownsampleBlitterDeprecated"
{
    Properties
    { }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent"}
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 posOS    : POSITION;
                float2 uv       : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posCS    : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.posCS = TransformObjectToHClip(i.posOS);
                o.texcoord = i.uv;
                return o;
            }

            TEXTURE2D(_GnFFrame);
            SAMPLER(sampler_GnFFrame);

            half4 frag(Varyings i) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_GnFFrame, sampler_GnFFrame, i.texcoord);
            }

            ENDHLSL
        }
    }
}
