Shader "RealityPlayground/StoryAngelCore"
{
    Properties
    {
        _Intensity("Contained glow", Range(0,3)) = 2.2
        _Voice("Speech energy", Range(0,1)) = 0
        _Accent("Syllable accent", Range(0,1)) = 0
        _PhaseOffset("Speech motion", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Name "ContainedReactiveUniverse"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Intensity, _Voice, _Accent, _PhaseOffset;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            float Ribbon(float phase, float width)
            {
                float footprint = max(fwidth(phase), .018);
                return (1 - smoothstep(width, width + footprint + .3, abs(sin(phase)))) * (1 - smoothstep(1.2, 3.2, footprint));
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 origin = TransformWorldToObject(GetCameraPositionWS());
                float3 direction = SafeNormalize(TransformWorldToObject(input.positionWS) - origin);

                float radius = .46;
                float b = dot(origin, direction);
                float c = dot(origin, origin) - radius * radius;
                float discriminant = b * b - c;
                clip(discriminant);
                float root = sqrt(max(discriminant, 0));
                float nearHit = -b - root;
                float farHit = -b + root;
                clip(farHit);
                float hit = nearHit > 0 ? nearHit : farHit;
                float3 surface = (origin + direction * hit) / radius;
                float longitude = atan2(surface.z, abs(surface.x)+abs(surface.z)<.00001?.00001:surface.x);
                float latitude = asin(clamp(surface.y, -.9999, .9999));
                float time = _Time.y + _PhaseOffset;
                float band = floor(surface.y * 19 + time * 2.3);
                float hash = frac(sin(band * 127.1 + floor(time * 13) * 43.7) * 43758.5453);
                float fault = step(.79, hash) * _Accent;
                longitude += fault * sin(band * 9.2) * .7;
                float twist = longitude + sin(latitude * 5 - time * 1.1) * (.35 + _Voice * .32) + latitude * (2.6 + _Voice * 1.4);
                float folds = sin(twist * 3 - latitude * 4 + time * 1.7);
                float spiral = Ribbon(twist * 5 + latitude * 6 - time * 2.2 + folds * 1.1, .18);
                float other = Ribbon(twist * 3 - latitude * 8 + time * 1.8, .09);
                float broad = smoothstep(-.9, .8, sin(twist * 2 - latitude * 5 + folds + time * .7));
                float3 spectrum = .52 + .48 * cos(6.2831853 * (twist * .12 + latitude * .15 - time * .08 + float3(0, .33, .67)));
                float3 inverse = .52 + .48 * cos(6.2831853 * (latitude * .3 - twist * .14 + time * .06 + float3(.17, .5, .84)));
                float facing = abs(dot(surface, -direction));
                float rim = pow(saturate(1 - facing), 2);
                float alpha = smoothstep(0, max(fwidth(discriminant) * 2, .001), discriminant);
                float3 color = spectrum * (.08 + broad * .25 + spiral * (.5 + _Voice * .15)) + inverse * (other * .38 + rim * .24);
                color = lerp(color, color.brg * .75, fault * .7);

                color *= min(_Intensity, 3) * (1 + _Voice * .12);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
