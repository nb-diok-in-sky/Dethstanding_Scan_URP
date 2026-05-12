# Dethstanding_Scan
Unity死亡搁浅扫描效果复刻
开新坑了
事情的起因是某位大神和我说做一个这样类型的案例提升能力会挺大的，于是我就来了，如此简单。

这次基于Unity Shader的一个效果实现
ASE用的应该会少一些

当然这里也会编写一点unity 里glsl的一些常见变量
以后会挪到别的库里面，当作一个记忆点
Unity Scripting API（C# API 查询）：
https://docs.unity3d.com/ScriptReference/
URP 专用 API：
https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/api/
Unity Shader 内置变量/函数：
https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html
供出几尊大神 接下来会往这里面结合我自己的理解搬运信息

这次使用基于UPR的渲染管线，而不是之前普通的unity旧版渲染管线



首先是案例分析
<img width="600" height="337" alt="DepthStanding_Scan1" src="https://github.com/user-attachments/assets/0c8935e3-c8c3-449b-ab9f-3feac8c83f7d" />
<img width="600" height="369" alt="DepthStanding_Scan2" src="https://github.com/user-attachments/assets/123a4af8-f07f-4e71-8823-01f934d0bf32" />


从图里面我们可以看到非常明显的物体描边的效果实现
同时他的描边效果也不是像二次元一样的轮廓线描边，也不是乱描边
他会有明显的方向性：朝向人物的这一边的轮廓他就画出来，别的方向就不画 最后的留存图类似于一个等高线的感觉，并不是所有的线条保留后都会有比较长的生命周期
同时地面上会产生重复的一个网状蓝色细线
再仔细看其实跑的最快的那条线后面还跟着部分压重的效果


所以这就产生了四个效果
第一是扫描效果，然后会产生从近处到远处的一个效果推进  也就是头部扫描线 ——刷的一下就过去了
第二是描边的处理，需要设定好合适的生命周期以及与扫描效果搭配好   这个是描边效果
第三是地面重复线条的效果 需要设定好合适的宽度和间距    这个是平行扫描线的效果 同时这个描边效果肯定是不能到单独跑出来 需要和头部扫描线做一个遮罩 扫描完毕后才会跑出来
第四就是一个压暗的效果 这个比较好实现   跟着头部扫描线来就行
其实还有一个补充就是脚底下一般不会产生扫描线，所以还需要距离函数 和smoothstep 函数来对 脚底下的线条规模进行一个模糊与消除的处理

这是扫描线的实现

下面是标志的实现
考虑使用大量相同材质进行绘制
可以使用GPU instanding 来进行操作 节省性能





这个图帅

<img width="300" height="168" alt="DepthStanding_Scan4" src="https://github.com/user-attachments/assets/588fc4cf-a652-43ba-a4d6-14f437250728" />



同时平面会有蓝色的小点点，斜坡上点位还会有红色的小叉叉
红叉叉出现的位置不固定 但是会出现在同一些物体上
以及黄色的点点 这些是死亡搁浅内部一些标识信号  
考虑在后处理阶段将他们设为不同的层级 不同的层级扫完后就不同的图案显示在表面

<img width="289" height="174" alt="Scan3" src="https://github.com/user-attachments/assets/0cd3a446-bdb2-4b09-a39a-755b15cd91eb" />




实现分析
首先是扫描效果，可能是基于深度图数值进行扫描的效果实现
截取的是按下触发按钮那一帧的屏幕深度图（不然扫描效果会跟着视角转换或者人物移动跑）
同时在后处理阶段进行对特定层级进行特殊处理（给予不同的粒子特效，比如说红叉叉和蓝点点）

还要编写脚本
扫描圈步骤详解
按下指定按钮，朝摄像机y方向发射一道粒子射线，击中地面时间
截取射点对于场景坐标的深度 同时可能需要考虑到调整这个点的y轴让他与摄像机持平 ，之后再考虑
同时要做好cd，某个时间内只能触发一次


获取深度图，使用power




















