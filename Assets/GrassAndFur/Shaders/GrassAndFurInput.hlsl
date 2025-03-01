#ifndef GRASS_AND_FUR_INPUT
#define GRASS_AND_FUR_INPUT

struct VertexData
{
    float3 posWS;
    float3 normalOS;
    float2 uv;
    float offset;
};

struct OutTriangleData
{
    VertexData v[3];
};

struct Varyings
{
    float4 posCS : SV_POSITION;     // Clip-space pos
    float2 uv : TEXCOORD0;          // Local UV coords
    float3 normalWS : NORMAL;       // World-space normals
    float3 posWS : TEXCOORD1;       // World-space pos
    float offset : TEXCOORD2;       // Local shell offset
    float infOffset : TEXCOORD3;    // Influenced shell offset based on height
    float fogFactor : TEXCOORD4;    // URP fog factor
#ifdef DOWNSAMPLE
    float4 posSC : TEXCOORD5;       // Screen-space pos
#endif
};

StructuredBuffer<OutTriangleData> _OutTriangles;
#ifdef USE_EXPLOSION
    static const uint MAX_EXPLOSIONS = 8;
    uniform float4 _ExplosionsData0[MAX_EXPLOSIONS];    // uv coords = xy, uv-space explosion radius = z, uv-space explosion intensity
    uniform float4 _ExplosionsData1[MAX_EXPLOSIONS];    // explosion time = x, uv-space explosion radius blend = y, explosion impact cut intensity = z, wave frequency = w
#endif

TEXTURE2D(_AlbedoTex);
SAMPLER(sampler_AlbedoTex);

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

TEXTURE2D(_WindTex);
SAMPLER(sampler_WindTex);

TEXTURE2D(_MaskTex);
SAMPLER(sampler_MaskTex);

TEXTURE2D(_AddColorTex);
SAMPLER(sampler_AddColorTex);

#ifdef USE_STYLE_TEX
    TEXTURE2D(_StyleTex);
    SAMPLER(sampler_StyleTex);
#endif

#ifdef USE_TRACKING_FEATURE
    TEXTURE2D(_TrackingTex);
    SAMPLER(sampler_TrackingTex);
#endif

CBUFFER_START(UnityPerMaterial)
    float4 _AlbedoTex_ST, _MainTex_ST, _WindTex_ST;

    float4 _TopColor, _BotColor, _ColorTint, _ColorAmbient, _AddAlbedoTint;
    half _ColTrans, _AddAlbTrans, _AddAlbOpac, _MainTexCut;

    half _ShdCoverage, _ShdSmh, _ShadowOpac;
    float4 _ShdTint;

    half _ShellMotionIntensity, _ShellOffsetInfluence, _ShellGravity, _ShellCurvatureIntens, _ShellCurvatureFreq;
    half _ExploWaveCount, _ExploWaveOffset;

    half _WindIntens, _WindSpeed, _FadeOutDist, _FadeOutDistSmh;

    float4 _BlitterBrushData0;
    float4 _BlitterBrushData1;
    half _VisibleAddLightCount;
CBUFFER_END

#endif