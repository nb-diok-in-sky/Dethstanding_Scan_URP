    Shader "TerrianMarks"
    {
        Properties
        {   //这一整个脚本就是用来进行一个 判定扫描过后应该是什么图标的东西
             //一切围绕这个来展开
            _IconSize("Icon Size", Float) = 1   //这里是扫描后得到东西的大小
            [HDR] _SafeColor("Safe Color",Color) = (1,1,1,1)
            [HDR] _WarningColor("Warning Color" ,Color) = (1,1,0,1)
            [HDR] _DangerColor("Danger Color",Color) = (1,0,0,1)
        }
        SubShader
        {
            Tags { "RenderType"="Opaque"
                   "RenderPipeline" = "UniversalPipeline"
            }

            Pass
            {Tags{

            "LightMode" = "UniversalForward"
               }


               ZWrite Off
               ZTest LEqual
               Cull Back
               Blend SrcAlpha OneMinusSrcAlpha

               HLSLPROGRAM

                #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

                #pragma shader_feature _RECEIVE_SHADOWS_OFF

                #pragma multi_compile_instancing



                CBUFFER_START(UnityPerMaterial)
                float _IconSize;
                float4 _SafeColor;
                float4 _WarningColor;
                float4 _DangerColor;
                CBUFFER_END

                float colorAlpha;

                struct Attributes{
                float2 uv : TEXCOORD0;  
                uint instanceID :SV_InstanceID;  //id是用来分辨是哪种图标
            
                };

                struct Varyings{
                float2 uv :TEXCOORD0;
                float4 positionCS :SV_POSITION;    
                float3 positionWS :TEXCOORD1;
                uint instanceID : SV_InstanceID;   //哪种图标
                };


                #pragma vertex PassVertex
                #pragma fragment PassFragment


                struct Marks{       //标记的结构体，内含 位置与类型信息
                float3 position;
                int type;

            
                };

                StructuredBuffer<Marks> markBuffer;

                Varyings PassVertex(Attributes input)   //顶点着色器
                {
                Varyings output;
                float2 uv = input.uv;

                uint instanceID = input.instanceID;     

                float3 posCenterWS = markBuffer[instanceID].position;   

                float3 dirToCam = GetWorldSpaceNormalizeViewDir(posCenterWS);
                float3 xAxis = normalize(cross(float3(0,1,0),dirToCam));   
                float3 yAxis = normalize(cross(dirToCam,xAxis));

                float3 posWS = posCenterWS;
                posWS += xAxis *(uv.x *2 -1) * 0.05 *_IconSize;
                posWS += yAxis * (uv.y *2-1) *0.05 *_IconSize;

                output.positionCS = TransformWorldToHClip(posWS);
                output.uv = uv;
                output.positionWS = posWS;

                output.instanceID = instanceID;

                return output;
            
            
            
                }

          half4 DrawPattern(int type,float2 uv){//这个方法用来调整不同的图标来绘制不同的标志颜色
            
                  half center = length(uv-0.5);
                  half2 centered = uv - 0.5;

                    switch(type){
                        case 0 : {
                        half circle1 = step(0.2,center);
                        half circle2 = step(center,0.3);
                        return circle1 * circle2 * _SafeColor;  //这个画了个圈圈
                }
                case 1:{
                
                return step(center,0.1) *_SafeColor;
                }
                case 2:{
                   //这个12 都是画了一个形状的圆
                return step(center,0.1) *_WarningColor;
                }

                case 3:{
                half strip1 = step(abs(centered.x - centered.y), 0.1);
                half strip2 = step(abs(centered.x + centered.y), 0.1);
                half cross = max(strip1,strip2);//这是那个叉叉
                half squad = step(max(abs(centered.x), abs(centered.y)), 0.4);//这个会绘制方形
                //然后画圆环
                half glow = saturate((center - 0.25) * (center - 0.25) * 50 + 0.2); //这个圆环是颜色稍微暗盖在叉叉上面的
            
                return cross * squad * glow *_DangerColor;
                }
                default:
                return 0 ;

             }
            
                }

                half4 PassFragment (Varyings input,out float depth :SV_DEPTH) : SV_Target  //这里是片段着色器 
                {
                float3 dirToCam = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half4 color = DrawPattern(markBuffer[input.instanceID].type , input.uv);//给予不同的图标不同的颜色
                color.a *= colorAlpha;

                half4 posNDC4Depth = TransformWorldToHClip(input.positionWS +dirToCam *_IconSize * 0.1);
                //这一段是减少所有标记的深度，使得所有标记的深度比地面小那么一点点 以确保他们完全绘制在地面前面 
                depth = posNDC4Depth.z/posNDC4Depth.w; //这个深度值会被out出去 会被拿给sv：target


                return color;
            
                }
                ENDHLSL 
               
                    }     
                    }
     FallBack "Hidden/Universal Render Pipeline/FallbackError"
 
     }

