Shader "Hidden/Grass And Fur/GrassAndFurDownsampleBlitter"
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

            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_GnFFrame);
            SAMPLER(sampler_GnFFrame);

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            half4 frag (Varyings input) : SV_Target
            {
                half4 tColor = SAMPLE_TEXTURE2D(_GnFFrame, sampler_GnFFrame, input.texcoord);
                return lerp(SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.texcoord),
                    half4(tColor.rgb, 1), tColor.a);
            }

            ENDHLSL
        }
    }
}
