#ifndef GRASS_AND_FUR_VERT_FRAG
#define GRASS_AND_FUR_VERT_FRAG

Varyings VertexStage(uint id : SV_VertexID)
{
    Varyings o = (Varyings)0;

    OutTriangleData currTris = _OutTriangles[id / 3];
    VertexData currVertex = currTris.v[id % 3];

    o.uv = currVertex.uv;

#ifdef SHELL_ADVANCED
    float shellOffset = pow(abs(currVertex.offset), _ShellOffsetInfluence);
    currVertex.posWS.xz += float2(sin(currVertex.offset * _ShellCurvatureFreq), cos(currVertex.offset * _ShellCurvatureFreq)) * _ShellCurvatureIntens;
    currVertex.posWS.y += _ShellGravity * shellOffset;
#endif
    o.posWS = currVertex.posWS;
    o.posCS = TransformWorldToHClip(currVertex.posWS);

#ifdef DOWNSAMPLE
    o.posSC = ComputeScreenPos(o.posCS);
#endif
    o.offset = currVertex.offset;
    o.infOffset = pow(abs(o.offset), _ShellOffsetInfluence);
    o.normalWS = TransformObjectToWorldNormal(currVertex.normalOS);
#ifdef _FOG_FRAGMENT
    o.fogFactor = 0;
#else
    o.fogFactor = ComputeFogFactor(o.posCS.z);
#endif

    return o;
}

#ifdef USE_EXPLOSION
    float4 CreateExplosion(in float2 uv, in float offset, in uint index)
    {
        float2 exploDiff = uv - _ExplosionsData0[index].xy;
        float exploRadius = _ExplosionsData0[index].z;
        float wigglerIntens = _ExplosionsData0[index].w;

        float exploDiffL = length(exploDiff);
        float exploCurrentHeightInfl = offset * wigglerIntens;
        float2 exploDir = exploDiff * exploCurrentHeightInfl 
            * sin((_Time.y - _ExplosionsData1[index].x) // Time
                * _ExplosionsData1[index].w - (exploDiffL * _ExploWaveCount) + _ExploWaveOffset); // Wave freq
        float exploCurrentRadius = (1 - smoothstep(exploRadius - abs(_ExplosionsData1[index].y), exploRadius, exploDiffL)) / exploDiffL; // Radius blend smoothness

        return float4(exploDir * exploCurrentRadius, 
            exploCurrentHeightInfl * exploCurrentRadius,
            wigglerIntens * max(0, _ExplosionsData1[index].z) * exploCurrentRadius); // Explo impact cut intensity
    }

    void SimulateExplosions(inout float2 outUV, in float offset, inout float exploRadius, inout float exploHeightInfl)
    {
        float2 iuv = outUV;
        [unroll(MAX_EXPLOSIONS)]
        for (uint x = 0; x < MAX_EXPLOSIONS; x++)
        {
            float4 explosion = CreateExplosion(iuv, offset, x);
            outUV += explosion.xy;
            exploRadius += explosion.z;
            exploHeightInfl += explosion.w;
        }
    }
#endif

float4 FragStage(Varyings i) : SV_Target
{
    float2 uv = i.uv;

    // Explosions Feature
#ifdef USE_EXPLOSION
    float exploRadius = 0;
    float exploHeightInfl = 0;
    SimulateExplosions(uv, i.infOffset, exploRadius, exploHeightInfl);
#else
    float exploRadius = 0;
    float exploHeightInfl = 0;
#endif

    // Style & Dynamic Tracking Textures
#if defined(USE_STYLE_TEX) || defined(USE_TRACKING_FEATURE)
    float motionIntens = i.infOffset * _ShellMotionIntensity;
#endif

#ifdef USE_STYLE_TEX
    uv += (SAMPLE_TEXTURE2D(_StyleTex, sampler_StyleTex, uv).rg - 0.5) * motionIntens;
#endif

#ifdef USE_TRACKING_FEATURE
    half3 trackingTex = SAMPLE_TEXTURE2D(_TrackingTex, sampler_TrackingTex, uv).rgb;
    uv += (trackingTex.rg - 0.5) * motionIntens;
#else
    half3 trackingTex = half3(0., 0., 1.);
#endif

    // Wind Texture
    float2 windTex = (SAMPLE_TEXTURE2D(_WindTex, sampler_WindTex,
        TCoords(i.uv, _WindTex_ST) + half2(1, 1) * (_Time.y * _WindSpeed)).rg * _WindIntens) * pow(abs(i.offset), _ShellOffsetInfluence);

    // Texture Setup
    half3 albedoTex = SAMPLE_TEXTURE2D(_AlbedoTex, sampler_AlbedoTex, TCoords(uv, _AlbedoTex_ST) + windTex).rgb;
    half mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TCoords(uv, _MainTex_ST) + windTex).r;
    half maskTex = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv).r;
    half3 addTex = SAMPLE_TEXTURE2D(_AddColorTex, sampler_AddColorTex, uv + windTex).rgb;

    // Blitter Brush Setup
    half bBrush = (1 - smoothstep(_BlitterBrushData0.z, _BlitterBrushData0.z + _BlitterBrushData0.w,
        length(i.uv - _BlitterBrushData0.xy)));

    // Mask Cut
#ifdef CUT_EXPO
    half alphaCutout = pow(abs(mainTex), max(_MainTexCut, EPSILON) * i.offset);
#else
    half alphaCutout = mainTex * _MainTexCut;
#endif
#ifdef DISTANCE_FADE
    alphaCutout *= 1 - smoothstep(_FadeOutDist - _FadeOutDistSmh, _FadeOutDist, length(_WorldSpaceCameraPos - i.posWS));
#endif
    clip(alphaCutout - (i.offset / trackingTex.b) - maskTex - (exploRadius * exploHeightInfl) + bBrush * min(_BlitterBrushData1.w, maskTex));

    // Albedo Setup
    half colorTransition = smoothstep(0.0, _ColTrans, mainTex * i.offset);
    half addAlbedoByOffset = smoothstep(0.0, _AddAlbTrans, mainTex * i.offset);
#ifdef ADD_ALBEDO_INVERT
    addAlbedoByOffset = 1.0 - addAlbedoByOffset;
#endif
    half4 col = lerp(_BotColor, _TopColor, colorTransition);

    // Albedo Mixing
    col.rgb = lerp(albedoTex * _BotColor.rgb, col.rgb, colorTransition);
#ifdef ADD_ALBEDO_MULTI
    col.rgb *= lerp(half3(1., 1., 1.), addTex * _AddAlbedoTint.rgb, addAlbedoByOffset * _AddAlbOpac);
#else
    col.rgb = lerp(col.rgb, addTex * _AddAlbedoTint.rgb, addAlbedoByOffset * _AddAlbOpac);
#endif

    // Lighting
    col.rgb = CalculateLighting(i, col.rgb) * _ColorTint.rgb;
    col.rgb += _ColorAmbient.rgb;

    // URP Fog
    col.rgb = MixFog(col.rgb, InitializeInputDataFog(float4(i.posWS, 1.), i.fogFactor));

    // Blitter Brush Visuals
    col.rgb = lerp(col.rgb, _BlitterBrushData1.rgb, bBrush * _BlitterBrushData1.w / 4.0);

#ifdef DOWNSAMPLE
    // Downscaling feature - depth difference sampling
    col.a = step(SampleDepth(i.posSC), i.posCS.z);
#endif

    return col;
}

#endif