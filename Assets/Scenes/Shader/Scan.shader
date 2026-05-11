Shader "Unlit/Scan"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "SeparableGlassBlur"
             ZTest Always   //深度测试永远通过，不需要进行遮挡判断 ，不需要进行遮挡判断，摄像机帮忙做好了
             Cull Off  //剔除关闭  不需要进行剔除 因为只是描绘一张图 没有正背面区分
             ZWrite Off  //深度写入关闭   只是描绘一张图 不需要深度判断，已经帮忙弄好了
             Blend SrcAlpha OneMinusSrcAlpha   // 透明混合模式   意思是：最终颜色 = 当前像素颜色 × 自身Alpha + 屏幕原有颜色 × (1 - Alpha)


             HLSLPROGRAM
             //hlsl引用
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Blit.hlsl 提供 vertex shader (Vert), input structure (Attributes) and output strucutre (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"



            #pragma vertex vert
            #pragma fragment frag

            #define centerFadeoutDistance1 1
            #define centerFadeoutDistance2 6 //这两个控制中心渐变的范围
            
           float3 scanColorHead; //头部扫描颜色
           float3 scanColor;//第二个扫描的颜色
           float outlineWidth;//描边宽度
           float outlineBrightness;// 描边的软硬度
           float outlineStarDistance;//

           float scanLineInterval;//第二个扫描线
           float scanLineWidth;//第二个扫描线的宽度
           float scanLineBrightness;//第二个扫描线的软硬度
           float scanRange;//第二个扫描线的范围
           

           float4 scanCenterWS; //扫描中心的ws值
           float headScanLineDistance; //第一条扫描线距离
           float headScanLineWidth;  //第一条扫描线的宽度
           float headScanLineBrightness; //第一条扫描线的软硬值

          
            sampler2D _Pic; //普通的一个texture2D的纹理



            struct v2f
            {
                float2 uvs[9] : TEXCOORD0; //建立了uv数组
                float4 vertex : SV_POSITION;
            };

            v2f vert (Attributes v)//这里必须使用Attributefarpos
            //Attributes是HLSL里面内置的一个结构体  里面只会存储顶点索引
            //在当前这个项目里面只需要获取顶点信息 不需要获取其其他的，所以就是用这个Attribute就行
            {
                v2f o;
                float4 pos  = GetFullScreenTriangleVertexPosition(v.vertexID);
                float2 uv = GetFullScreenTriangleTexCoord(v.vertexID);
                
                o.vertex = pos;//这个使用场景的话拿到的图片的顶点就会等于屏幕顶点
                
                
                //这九个uv是用来配合Sobel描边的算法的
                //Sobel描边对于屏幕上方的每一个像素都需要采样他和自己周围的8个点的深度值 才能算出深度
               
               o.uvs[0] = uv + _ScreenSize.zw * half2(-1, 1) * outlineWidth;
                o.uvs[1] = uv + _ScreenSize.zw * half2(0, 1) * outlineWidth;
                o.uvs[2] = uv + _ScreenSize.zw * half2(1, 1) * outlineWidth;
                o.uvs[3] = uv + _ScreenSize.zw * half2(-1, 0) * outlineWidth;
                o.uvs[4] = uv;
                o.uvs[5] = uv + _ScreenSize.zw * half2(1, 0) * outlineWidth;
                o.uvs[6] = uv + _ScreenSize.zw * half2(-1, -1) * outlineWidth;
                o.uvs[7] = uv + _ScreenSize.zw * half2(0, -1) * outlineWidth;
                o.uvs[8] = uv + _ScreenSize.zw * half2(1, -1) * outlineWidth;//这么多half是用来表示偏移的 为了配合Sobel描边 Sobel描边和周围的点的距离越大 他们的描边就会越粗当然，也会受到oulineWidth的影响

                return o;
            }

            //获取世界坐标的方法  这个是通过你现在屏幕上的坐标来获取这些顶点的世界坐标
            float3 GetPixelWorldPosition(float2 uv, float depth01){//要求传入深度和uv
            //使用一个NDC反透视除法    
            float3 farPosCS = float3(uv.x * 2 -1,uv.y * 2 - 1,1) * _ProjectionParams.z;
            float3 farPosVS = mul(unity_CameraInvProjection,farPosCS.xyzz).xyz;//反投影空间除法
            float3 PosVS = farPosVS * depth01;  //变回裁剪空间坐标   
            float3 posWS = TransformViewToWorld(PosVS);  //把裁剪空间坐标变成世界坐标
            return posWS;
            }
            half calculateVerticalOutline(float2 uvs[9]){
            half color = 0 ;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[0]).x, _ZBufferParams) * -1;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[1]).x, _ZBufferParams) * -2;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[2]).x, _ZBufferParams) * -1;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[6]).x, _ZBufferParams) * 1;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[7]).x, _ZBufferParams) * 2;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[8]).x, _ZBufferParams) * 1;

            return color;
               //这个是截取的一个横着的节点
               //-1 -2 -1
               //0 0 0 
               //1 2 1

            }


            half calculateHorizontalOutline(float2 uvs[9]){
            half color = 0 ;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[0]).x, _ZBufferParams) * -1;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[3]).x, _ZBufferParams) * -2;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[6]).x, _ZBufferParams) * -1;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[2]).x, _ZBufferParams) * 1;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[5]).x, _ZBufferParams) * 2;
            color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[8]).x, _ZBufferParams) * 1;

            //这里截取的是竖着的节点
            //-1 0 1
            //-2 0 2
            //-1 0 1   
            
            //-1 -2 -1 和 1 2 1 是前人验证出来的比较好用的一组数据 并不是什么特别的 换成别的组合也可以算 但是可能会效果不好，出现一些噪点
            return color;
            }

             
             

            half4 frag (v2f i) : SV_Target
            { 
               float depth01 = Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture,sampler_PointClamp,i.uvs[4]),_ZBufferParams);
               float3 posWS = GetPixelWorldPosition(i.uvs[4],depth01);

               float distanceToCenter = distance(scanCenterWS,posWS);  //这个是比较核心的点，他是屏幕坐标转译成世界坐标下的点到扫描中心点的距离 反应在视图上应该就是一张各个点到扫描中心的一个深度图 不过他不会变化 一直都是这些值 不会根据摄像机的移动又去移动
               //同时 这个也是为了脚底下不生成扫描线的效果做准备
                 
               float scanHeadLine1 = smoothstep(headScanLineDistance +0.5 *distanceToCenter * 0.03,headScanLineDistance,distanceToCenter);  //这是头部扫描线最前面那一条
               float scanHeadLine2 = smoothstep(headScanLineDistance - headScanLineWidth *distanceToCenter * 0.2 , headScanLineDistance,distanceToCenter);  //这是头部扫描线后面那一条
               float scanHeadLine = scanHeadLine1 * scanHeadLine2* scanHeadLine2* scanHeadLine2*headScanLineBrightness; //后面那段做一个power3 处理 让他断的更干净一些
               float4 scanHeadLineColor = float4(scanColorHead * scanHeadLine,scanHeadLine);

               float scanHeadLine3 = smoothstep(headScanLineDistance - headScanLineWidth *distanceToCenter*0.3 ,headScanLineDistance,distanceToCenter);
               float scanHeadLineBlack = scanHeadLine1 * scanHeadLine3 * scanHeadLine3 *scanHeadLine3 *headScanLineBrightness;
               float4 scanHeadLineColorBlack = float4(0,0,0,scanHeadLineBlack/2);//Line2和Line2的区别在于他们的值不腰痛 一个是0.3 一个是0.2 这意味着3的范围会更大一些
               //最终呈现的效果是正常的蓝光line1 和line2 的后面 也就是靠近视角的这一面 会多一点点黑色的 用来进行一个蓝光和正常场景的过渡 显得不那么生硬

                
               //下面是平行扫面线范围遮罩
               float scanLineRange2 = smoothstep(headScanLineDistance - distanceToCenter *2.5*scanRange,headScanLineDistance,distanceToCenter);
               float scanLineRange = scanHeadLine1 * scanLineRange2 *scanLineRange2; 

               //这里是中心渐变 没啥好说的 只要控制好min和max值在distanceToCenter的范围内就行
               float centerFadeout = smoothstep(3,6,distanceToCenter);

               //下面是平行扫描线的部分
               float wave = frac(distanceToCenter/scanLineInterval);
               float scanLine1 = smoothstep(0.5 - scanLineWidth * distanceToCenter *0.003,0.5,wave);
               float scanLine2 = smoothstep(0.5 +scanLineWidth *distanceToCenter *0.003,0.5,wave);//这里是生成一个圆值和scanLineWidth有关的圆 之后把他位移到0.5，0.5的uv点上，
               float scanLine = scanLine1 *scanLine2; //靠着和之前的line12的原理一正一负形成一个环
               scanLine *= scanLineRange * scanLineBrightness * centerFadeout;//这两个是写了两个渐变值
               float4 scanLineColor = float4(scanColor*scanLine,scanLine);  //这个以scanLine做渐变值是想要每个点做不同的变化 生成的时候每个点会有不同的scanline值 他们对应着不同的透明度
               

               half outlineV = calculateVerticalOutline(i.uvs);
               half outlineH = calculateHorizontalOutline(i.uvs);
               half outline = sqrt(outlineV *outlineV +outlineH*outlineH);//上面这三个是外部描边 就是sobel的轮廓线效果
               //这里用sqrt的原因是寻常用加法算一算也就罢了，差别不大 一旦涉及到不同的斜度 加法的数值就会非常不精确  这个方法可以类比二维坐标系求线段长 获取的结果比较精准

               float depthMask = saturate(1-distanceToCenter *0.01); //最终的结果应该是一个白色的圆圈被黑色包裹，距离扫描中心越远，值越小越趋近于0 这个是拿着那个距离场做了一个缩小的范围 拿去之后遮罩
              depthMask *= depthMask; //进一步缩减范围 让小的更小 做一个power 由线性减淡变成快速淡出 近处强描边
              half outLineDistanceMask = smoothstep(outlineStarDistance - 10,outlineStarDistance,distanceToCenter);//控制描边从哪里开始出现  控制描边的范围及给予一个比较平滑的过渡 等到通过了outlineStarDistance就会展现出完整的姿态
              outline *= 1000*depthMask;
               outline = step(1,outline)*outlineBrightness *scanHeadLine1 *outLineDistanceMask;//硬描边， 直接把描边强硬的描绘出来 不符合就不做打算，直接不要
               float4 outlineColor = float4(scanColor *outline,outline);//这个是正常给颜色

              float4 color = scanHeadLineColor + scanHeadLineColorBlack +scanLineColor +outlineColor;

  
                return color;
            }
               ENDHLSL
        }
    }
}
