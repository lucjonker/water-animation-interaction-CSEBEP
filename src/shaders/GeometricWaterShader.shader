Shader "Custom/GeometricWaterShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _RenderTexture ("RenderTexture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        //Doesn't just affect speed but also frequency a bit
        _SineSpeed("Sine Speed", Range(0, 1)) = 0.5
        _Amplitude("Universal Amplitude", Range(0, 1)) = 1
        _WaveA ("Wave A (Direction, Steepness, Wave Number)", Vector) = (1,0,0.5,0.5)
        _WaveB ("Wave B (Direction, Steepness, Wave Number)", Vector) = (1,0.6,0.5,0.5)
        _WaveC ("Wave C (Direction, Steepness, Wave Number)", Vector) = (1,1.4,0.5,0.5)

        [PerRendererData] uvxOffset ("uvxOffset", float) = 0
        [PerRendererData] uvyOffset ("uvyOffset", float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque"  "UniversalMaterialType" = "Lit"}
        LOD 100
        Pass
        {
            HLSLPROGRAM
            //Define Imports and pragmas
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            //passed in from clipmapmanager
            float trackingOffsetTime;

            //Attribute struct for the input for the shader
            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float3 tangent: TANGENT;
                float2 uv : TEXCOORD0;
            };

            //Varyings struct for the output of the shader
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal: NORMAL;
                float3 tangent: TEXCOORD1;
                float4 sampledVal: TEXCOORD3;
            };

            //Add properties to CBUFFER
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _SineSpeed;
                float _Amplitude;
                float4 _WaveA;
                float4 _WaveB;
                float4 _WaveC;

                float totalPlayAreaLength;
                float uvxOffset;
                float uvyOffset;
            CBUFFER_END

            //Define texture and sampler
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Texture2D _RenderTexture;
            SamplerState sampler_RenderTexture;

            //function to calculate a Gerstner Wave based on wavedata passed as parameter (switch to uvs  later?)
            float3 calculateGerstnerWave(float2 pos, float4 waveData){
                //Direction
                float2 d = normalize(waveData.xy);
                //Wave Number
                float k = (2 *  3.14159265f) / waveData.w;
                //Amplitude 
                float a = _Amplitude * (waveData.z / k);
                //Sine speed
                float c = _SineSpeed * (sqrt(9.8 / k));
                //Value passed to wave function
                float f = ((dot(d,pos * totalPlayAreaLength)) - (trackingOffsetTime * c)) * k;
                //Return calculated position data
                return float3(d.x * (a * cos(f)), 
                                     a * sin(f), 
                              d.y * (a * cos(f)));
            }

            float3 calculateOffset(float3 pos, float2 uv){
                //Add Waves based on input wave data
                pos += calculateGerstnerWave(uv, _WaveA);
                pos += calculateGerstnerWave(uv, _WaveB);
                pos += calculateGerstnerWave(uv, _WaveC);
                pos.y += _RenderTexture.SampleLevel(sampler_RenderTexture, uv, 0).x;
                return pos;
            }

            float3 calculateNormal(Attributes IN, float4 modifiedPos){

                //Calculate position at tangential point
                float3 posPlusTangent = IN.position.xyz + IN.tangent * 0.001;
                float2 tanUv = IN.uv;
                tanUv.x += ((posPlusTangent.x - IN.position.x)/totalPlayAreaLength);
                tanUv.y += ((posPlusTangent.z - IN.position.z)/totalPlayAreaLength);
                posPlusTangent = calculateOffset(posPlusTangent.xyz, tanUv);
                //Calculate position at bi -tangential point
                float3 bitangent = cross(IN.normal, IN.tangent);
                float3 posPlusBitangent = IN.position.xyz + bitangent * 0.001;
                float2 biTanUv = IN.uv;
                biTanUv.x += ((posPlusBitangent.x - IN.position.x)/totalPlayAreaLength);
                biTanUv.y += ((posPlusBitangent.z - IN.position.z)/totalPlayAreaLength);
                posPlusBitangent = calculateOffset(posPlusBitangent.xyz, biTanUv);
                //Modify vectors based on current vertex position
                float3 modifiedTangent = posPlusTangent - modifiedPos;
                float3 modifiedBitangent = posPlusBitangent - modifiedPos;
                //Calculate normal for Gerstner waves
                float3 modifiedNormal = cross(modifiedTangent, modifiedBitangent);
                return normalize (modifiedNormal);
            }

            //The vertex shader definition.
            Varyings vert(Attributes IN)
            { 
                //Calculate modified vertex position
                float2 uvOffset = float2(uvxOffset, uvyOffset);
                float2 newV = IN.uv + uvOffset;
                IN.uv = newV;

                // UV based calculation
                float4 modifiedPos = IN.position;
                modifiedPos.xyz = calculateOffset( IN.position.xyz, IN.uv);
                // Use modified position in normal calculation
                IN.normal = calculateNormal(IN, modifiedPos);
                // Set vertex position
                IN.position = modifiedPos;

                Varyings OUT;
                OUT.position = TransformObjectToHClip(IN.position.xyz);
                OUT.uv = IN.uv;
                OUT.normal = mul((float3x3)unity_ObjectToWorld, IN.normal);
                OUT.tangent = IN.tangent;
                return OUT;
            }

            // The fragment shader definition.
            half4 frag(Varyings IN) : SV_Target
            {
                if(IN.uv.x < 0 || IN.uv.x > 1 || IN.uv.y < 0 || IN.uv.y > 1){
                    discard;
                }

                float4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float renderTex = SAMPLE_TEXTURE2D(_RenderTexture, sampler_RenderTexture, IN.uv).x * 500;

                return baseTex + renderTex;
            }
            ENDHLSL
        }
    }
}
// Based on HLSL tutorial found at https://danielilett.com/2021-04-02-basics-3-shaders-in-urp/ and unity shaders video https://www.youtube.com/watch?v=flI6fRJzN_M
// Gertsner wave method inspired by https://catlikecoding.com/unity/tutorials/flow/waves/
// Normal recalculation method based on https://www.ronja-tutorials.com/post/015-wobble-displacement/