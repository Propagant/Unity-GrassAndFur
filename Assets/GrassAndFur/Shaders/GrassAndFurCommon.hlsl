#ifndef GRASS_AND_FUR_COMMON
#define GRASS_AND_FUR_COMMON

#define EPSILON 1.0e-4

float2 TCoords(float2 uv, float4 ST)
{
	return uv * ST.xy + ST.zw;
}

#ifdef DOWNSAMPLE
    #ifdef UNITY_URP
        float SampleDepth(float4 posSC)
        {
            return SampleSceneDepth(posSC.xy / posSC.w);
        }
    #endif
#endif

half3 CalculateLambert(in Varyings i, Light light)
{
    return (light.color * lerp(_ShdTint.rgb, half3(1, 1, 1),
        saturate(smoothstep(_ShdCoverage, _ShdCoverage + _ShdSmh, dot(light.direction, i.normalWS)))))
        * lerp(1., light.shadowAttenuation, _ShadowOpac);
}

half3 CalculateMainLight(in Varyings i, float4 shadowCoord)
{
    return CalculateLambert(i, GetMainLight(shadowCoord));
}

half3 CalculateAdditionalLights(in Varyings i, float4 shadowCoord)
{
    half3 col = half3(0., 0., 0.);

#ifdef _ADDITIONAL_LIGHTS
    
    InputData inputData = (InputData)0;
    inputData.positionWS = i.posWS;
    inputData.normalWS = i.normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.posWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.posCS * 
    #ifdef DOWNSAMPLE
        _DownsampleValue
    #else
        1.0
    #endif
    );
    
    #if USE_CLUSTER_LIGHT_LOOP
        UNITY_LOOP for (uint lindx = 0; lindx < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lindx++)
        {
            Light light = GetAdditionalLight(lindx,  i.posWS, shadowCoord);
            col += CalculateLambert(i, light) * light.distanceAttenuation;
        }
    #endif
    
    #if UNITY_VERSION < 202200
        uint meshRenderingLayers = GetMeshRenderingLightLayer();
    #else
        uint meshRenderingLayers = GetMeshRenderingLayer();
    #endif

    #ifndef DOWNSAMPLE
        LIGHT_LOOP_BEGIN(uint(GetAdditionalLightsCount()))
            Light light = GetAdditionalLight(lightIndex, i.posWS, shadowCoord);
    #else
        LIGHT_LOOP_BEGIN(uint(_VisibleAddLightCount)) 
            Light light = GetAdditionalPerObjectLight(lightIndex, i.posWS);
    #endif
     
    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            #ifdef DOWNSAMPLE
                light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, i.posWS, light.direction);
            #endif
            col += CalculateLambert(i, light) * light.distanceAttenuation;
        }
    LIGHT_LOOP_END
    
#endif

    return col;
}

half3 CalculateLighting(in Varyings i, half3 col)
{
#ifdef MAIN_LIGHT_CALCULATE_SHADOWS
    float4 shadowCoord = TransformWorldToShadowCoord(i.posWS);
#else
    float4 shadowCoord = float4(0, 0, 0, 1);
#endif
    return col.rgb * (CalculateMainLight(i, shadowCoord) + CalculateAdditionalLights(i, shadowCoord));
}

#endif