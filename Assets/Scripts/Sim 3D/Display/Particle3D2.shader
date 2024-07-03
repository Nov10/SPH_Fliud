Shader"Raymarching/Particle3D2" {
    Properties {
        _StepSize("Ray Step Size", Range(0.01, 1.0)) = 0.1
        _ParticleRadius("Particle Radius", Range(0.01, 1.0)) = 0.1
        _BoundsSize("Bounds Size", Vector) = (10, 10, 10)
        _ColourMap("Colour Map", 2D) = "white" {}
        _VelocityMax("Max Velocity", Float) = 1.0
        _Scale("Scale", Float) = 1.0
    }
    SubShader {
        //LOD200  
        Tags { "RenderType" = "Opaque" }

        Pass {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "UnityCG.cginc"

            struct Particle
            {
                float3 pos;
                float3 vel;
            };

            StructuredBuffer<Particle> particles;
            float _StepSize;
            float _ParticleRadius;
            float3 _BoundsSize;
            float4x4 _CamToWorld;
            float4x4 _CamInvProj;
            sampler2D _ColourMap;
            float _VelocityMax;
            float _Scale;

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float4 projPos : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.projPos = v.vertex;
                return o;
            }

            float sphereSDF(float3 p, float3 c, float r)
            {
                return length(p - c) - r;
            }

            float map(float3 p)
            {
                float d = 1000.0;
                for (int i = 0; i < particles.Length; i++)
                {
                    d = min(d, sphereSDF(p, particles[i].pos, _ParticleRadius));
                }
                return d;
            }

            float rayMarch(float3 ro, float3 rd)
            {
                float dO = 0.0;
                for (int i = 0; i < 100; i++)
                {
                    float3 p = ro + rd * dO;
                    float dS = map(p);
                    dO += dS;
                    if (dO > 100.0 || dS < 0.001)
                        break;
                }
                return dO;
            }

            float3 Unproject(float2 uv)
            {
                float4 proj = float4(uv * 2.0 - 1.0, 0.5, 1.0);
                float4 unproj = mul(_CamInvProj, proj);
                return unproj.xyz / unproj.w;
            }
            float4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.projPos.xy / i.projPos.w * 0.5 + 0.5;
                float4 ro = mul(_CamToWorld, float4(0, 0, 0, 1)); // Ray origin in world space
                float3 rd = normalize(mul((float3x3) _CamToWorld, Unproject(i.projPos.xy)));
                float d = rayMarch(ro.xyz, rd);
                if (d > 100.0)
                    discard;
                float3 p = ro.xyz + rd * d;
                float3 normal = normalize(p); // Approximation

                float3 col = float3(0.5, 0.5, 0.5) + normal * 0.5;
                return float4(col, 1.0);
            }


            ENDCG
        }
    }
FallBack"Diffuse"
}
