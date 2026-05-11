using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;



public class ScanFeature : ScriptableRendererFeature
{
    //首先是要使参数可以进行便利的调节，所以需要一个统一管理的地方
    //于是我们创建setting类 用来在外部可以进行调整参数 类似于propertyes
    [System.Serializable]
    public class Settings
    {
        //用来调整渲染队列位于透明物体之后 也就是后处理阶段
        public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingTransparents;

        public Material scanMaterial;//声明材质球变量名

        //下面是基本参数
        public Color scanColorHead = Color.blue;
        public Color scanColor = Color.blue;
        public float outlineWidth = 0.1f;
        public float scanLineWidth = 1f;
        public float scanLineInterval = 1f;
        public float headScanLineWidth = 1f;


        //然后是比较动态的参数
        public float scanLineBrightness = 1f;
        public float scanRange = 1f;
        public float outlineBrightness = 1f;
        public float headScanLineDistance = 8f;
        public Vector3 scanCenterWS = new Vector3(123.05f, 36.3f, 147.86f);
        public float outlineStarDistance = 30f;


        //下面是四个标志
        public Material markMaterial;
        public GameObject markParticle1;
        public GameObject markParticle2;
        public GameObject markParticle3;


    }

    public Settings settings = new Settings();

    static ScanFeature _instance;


    //新建一个CustomRenderPass 
    CustomRenderPass _myPass;


    readonly static int ScanColorHead = Shader.PropertyToID("scanColorHead");
    readonly static int ScanColor = Shader.PropertyToID("scanColor");



    readonly static int OutlineWidth = Shader.PropertyToID("outlineWidth");
    readonly static int OutlineBrightness = Shader.PropertyToID("outlineBrightness");
    readonly static int OutlineStarDistance = Shader.PropertyToID("outlineStarDistance");


    readonly static int ScanLineWidth = Shader.PropertyToID("scanLineWidth");
    readonly static int ScanLineInterval = Shader.PropertyToID("scanLineInterval");
    readonly static int ScanLineBrightness = Shader.PropertyToID("scanLineBrightness");
    readonly static int ScanRange = Shader.PropertyToID("scanRange");


    readonly static int HeadScanLineWidth = Shader.PropertyToID("headScanLineWidth");
    readonly static int HeadScanLineDistance = Shader.PropertyToID("headScanLineDistance");
    readonly static int HeadScanLineBrightness = Shader.PropertyToID("headScanLineBrightness");
    readonly static int ScanCenterWS = Shader.PropertyToID("scanCenterWS");



    readonly static int ColorAlpha = Shader.PropertyToID("colorAlpha");//这个是用于调节地形标记的参数

    static bool canScan = true;
    static bool showMark = false;
    static Tween markTween;




    public static void ExecuteScan(Transform player)
    {   //  执行扫描的方法
        StartScan(player).Forget();

    }

    static async UniTaskVoid StartScan(Transform player)
    {
        if (!canScan)
        {
            return;
        }
        canScan = false;
        showMark = true;

        // 万一上一个mark还没消失，手动取消
        markTween?.Kill();
        var scanCenter = player.position - player.forward * 2;

        var material = _instance.settings.scanMaterial;
        var markMaterial = _instance.settings.markMaterial;
        material.SetVector(ScanCenterWS, scanCenter);

        // 控制扫描线前进
        material.SetFloat(HeadScanLineDistance, 4);
        material.DOFloat(250, HeadScanLineDistance, 3.5f).SetEase(Ease.InSine).onComplete += () => {
            canScan = true;
        };

        // 随着距离前进，扫描范围变大
        material.SetFloat(ScanRange, 1);
        material.DOFloat(5, ScanRange, 1.5f).SetEase(Ease.InSine).SetDelay(1);

        // 控制扫描线和最前方的扫描线颜色颜色
        material.SetFloat(ScanLineBrightness, 0.3f);
        material.SetFloat(HeadScanLineBrightness, 0);
        material.DOFloat(1, ScanLineBrightness, 0.2f).SetDelay(0.25f);
        material.DOFloat(1, HeadScanLineBrightness, 0.1f).SetDelay(0.25f);
        material.DOFloat(0, ScanLineBrightness, 0.5f).SetDelay(2.25f).SetEase(Ease.Linear);
        material.DOFloat(0, HeadScanLineBrightness, 0.5f).SetDelay(2.25f).SetEase(Ease.Linear);

        // 控制轮廓
        material.SetFloat(OutlineBrightness, 1);
        material.SetFloat(OutlineStarDistance, 0);
        material.DOFloat(0, OutlineBrightness, 0.5f).SetDelay(2.25f).SetEase(Ease.Linear);
        material.DOFloat(30, OutlineStarDistance, 1f).SetEase(Ease.InCubic);

        // 控制地形标记的透明度
        markMaterial.SetFloat(ColorAlpha, 0);
        markMaterial.DOFloat(1, ColorAlpha, 1f);
        markTween = markMaterial.DOFloat(0, ColorAlpha, 1f).SetDelay(7);
        markTween.onComplete += () => {
            showMark = false;
        };

        //生成地形标记
        await GenerateTerrainMarks(player);
    }

    static ProfilerMarker _generateTerrainMarks = new ProfilerMarker("GenerateTerrainMarks");

    struct Marks
    {
        public Vector3 markPosition;
        public int markCategory;
    }

    static Marks[] _marks;  //这个存储的是每一个标记的数据
    const int horizentalCount = 70;//标记的最多横向的列数
    const int verticalCount = 50;//标记的最多的向前的方向的列数
    const float gridStep = 0.5f;//两个点之间的距离

    static void ShootParticle(Vector3 position, Vector3 normal, int index = 3)
    {
        float distanceToCamera01 = Vector3.Distance(position, Camera.main.transform.position) / 20 + 0.5f;

        GameObject instance;
        switch (index)
        {
            case 3:
                instance = Instantiate(_instance.settings.markParticle3);
                break;
            case 2:

                instance = Instantiate(_instance.settings.markParticle2);

                break;

            default:



                instance = Instantiate(_instance.settings.markParticle1);

                break;

        }

        instance.transform.position = position;
        instance.transform.localScale = Random.Range(0.5f, 1.5f) * Vector3.one * distanceToCamera01;
        instance.transform.GetChild(0).localScale = Random.Range(2f, 5f) * Vector3.one * distanceToCamera01;

    }

    static async UniTask GenerateTerrainMarks(Transform player)
    {

        //每次扫描之前先清空数组
        Array.Clear(_marks, 0, _marks.Length);
        var forward = player.forward;
        var right = player.right;



        //将撒点的初始位置顶在角色头顶的左后方
        Vector3 position = player.position - forward * 2 + Vector3.up * 100;
        var rayCastPos = position - right * horizentalCount / 2 * gridStep - forward * (3 * gridStep);

        //横向纵向写入两个循环 不断地检测碰撞和写入数组

        for (int i = 0; i < verticalCount; i++)
        {
            _generateTerrainMarks.Begin();
            for (int j = 0; j < horizentalCount; j++)
            {
                Physics.Raycast(rayCastPos, Vector3.down, out RaycastHit hit, 300, LayerMask.GetMask("Scan", "Road"));
                if (hit.collider is null)
                {
                    rayCastPos += right * gridStep;
                    continue;
                }

                var normal = hit.normal;

                if (hit.collider.isTrigger)
                {
                    Physics.Raycast(rayCastPos, Vector3.down, out hit, 300, LayerMask.GetMask("Scan"));
                    _marks[i * horizentalCount + j].markCategory = 0;
                    _marks[i * horizentalCount + j].markPosition = hit.point;
                }
                else if (normal.y < 0.75f)
                {
                    _marks[i * horizentalCount + j].markCategory = 3;
                    // 红叉只有33%的概率出现
                    if (Random.Range(0f, 1f) < 0.3f)
                    {
                        _marks[i * horizentalCount + j].markPosition = hit.point;
                        ShootParticle(hit.point, normal, 3);
                    }
                }
                else if (normal.y < 0.85f)
                {
                    _marks[i * horizentalCount + j].markCategory = 2;
                    _marks[i * horizentalCount + j].markPosition = hit.point;
                    if (Random.Range(0f, 1f) < 0.0003)
                    {
                        ShootParticle(hit.point, normal, 2);  //这里存疑 原文写的是1
                    }
                }
                else
                {
                    _marks[i * horizentalCount + j].markCategory = 1;
                    _marks[i * horizentalCount + j].markPosition = hit.point;
                    if (Random.Range(0f, 1f) < 0.0002)
                    {
                        ShootParticle(hit.point, normal, 1);

                    }

                }


                rayCastPos += right * gridStep;
            }
            _generateTerrainMarks.End();

            rayCastPos -= right * horizentalCount * gridStep;
            rayCastPos += forward * gridStep;


            await UniTask.Yield();





        }


    }


    //自定义渲染pass
    class CustomRenderPass : ScriptableRenderPass    //ScriptableRenderPass就是自定义的渲染步骤  可以通过继承这个类来自由插入自己的渲染步骤
    {
        //使用RTHandle 来存储相机的颜色和深度缓冲区

        RTHandle _cameraColor;  //RT的意思是RenderTexture   而Handle是句柄 RTHandle就是一个可以被渲染的纹理的引用 而非纹理本身   URP会自动重新分配纹理大小 所以不用RenderTexrure
        RTHandle _cameraDepth;
        RTHandle _tempTex;

        //纹理描述器

        RenderTextureDescriptor m_Descriptor;

        string _passName;
        Settings settings;

        GraphicsBuffer _graphicsBuffer;
        GraphicsBuffer.IndirectDrawIndexedArgs[] _commandData;
        GraphicsBuffer _computeBuffer; //这个是一块GPU显存上面的缓冲区域，是沟通CPU和GPU之间的桥梁
        //初始化类的时候传入材质

        Mesh mesh;


        public CustomRenderPass(Settings settings)//构造函数在此
        {
            _graphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            _computeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, horizentalCount * verticalCount, sizeof(float) * 4);


            mesh = new Mesh   //mesh类就是一堆顶点组成的网格形状
            {
                vertices = new Vector3[6],
                uv = new[] {
               new Vector2(0, 0),
               new Vector2(1,1),
               new Vector2(0,1),
               new Vector2(0,0),
               new Vector2(1,0),
               new Vector2(1,1),
                }//这个是在画一个普通的四边形 quad   他们的第三个分量会通过DarkMark.shader的顶点着色器里面通过GPU Instaning 里面的markBuffer里面统一进行设置



            };

            var scanMaterial = settings.scanMaterial;

            scanMaterial.SetColor(ScanColorHead, settings.scanColorHead);
            scanMaterial.SetColor(ScanColor, settings.scanColor);
            scanMaterial.SetFloat(OutlineWidth, settings.outlineWidth);
            scanMaterial.SetFloat(OutlineBrightness, settings.outlineBrightness);
            scanMaterial.SetFloat(OutlineStarDistance, settings.outlineStarDistance);


            scanMaterial.SetFloat(ScanLineWidth, settings.scanLineWidth);
            scanMaterial.SetFloat(ScanLineInterval, settings.scanLineInterval);
            scanMaterial.SetFloat(ScanLineBrightness, settings.scanLineBrightness);
            scanMaterial.SetFloat(ScanRange, settings.scanRange);

            scanMaterial.SetFloat(HeadScanLineDistance, settings.headScanLineDistance);
            scanMaterial.SetFloat(HeadScanLineWidth, settings.headScanLineWidth);

            scanMaterial.SetVector(ScanCenterWS, settings.scanCenterWS);
            _passName = "ScanEffect";
            this.settings = settings;
        }

        //在执行Pass之前执行的方法  用来构造渲染目标和清除状态
        //同样用来创建临时RT
        //如果为空 则会渲染到激活的rt上

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            //获得颜色缓冲区 存储到——cameraColor里面
            _cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
            _cameraDepth = renderingData.cameraData.renderer.cameraDepthTargetHandle;

            //然后是获得屏幕纹理的描述器
            m_Descriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0)
            {
                depthBufferBits = 0//不需要深度缓冲区 因为这个是用来中转颜色数据的 ，已经有了深度数据存储在cameradepth里面
            };

            RenderingUtils.ReAllocateIfNeeded(ref _tempTex, m_Descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "TempTex");
            //这个用来在blit的时候指定目标RT  如果不指定则默认为激活1的RT

            ConfigureTarget(_tempTex);//这个pass的就在这个临时rt上了
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {

            if (renderingData.cameraData.camera.cameraType != CameraType.Game) return;
            if (settings.scanMaterial == null) return;

            //新建一个  CommandBuffer
            //CommandBufferPool.Get()  会从一个池子里面获取 CommandBuffer，如果池里面没有可用的CommandBuffer 就会重新建一个
            CommandBuffer cmd = CommandBufferPool.Get(name: _passName);//这个cmd就是一个清单 接下来会往里面塞入CPU要GPU做的事情



            //创建一个frame debugger的作用域
            using (new ProfilingScope(cmd, new ProfilingSampler(cmd.name)))
            {
                Blitter.BlitCameraTexture(cmd, _cameraDepth, _cameraColor, settings.scanMaterial, 0);//往cmd里面塞深度图 和颜色缓冲 然后使用scanmaterial 也就是shader scan 里面的 第0个pass来处理
                // 具体意思是给cmd(CommandBuffer）写入如下指令： 将传入的cameraDepth传进scanMaterial(Scan材质) 调用scan的第0个Pass 然后把里面片段着色器的color返回值拿过来，然后赋值给_cameraColor
                //实际上啥也没干 只是给GPU的待做清单写上了这样一条指令
                if (showMark)
                {
                    cmd.SetRenderTarget(_cameraColor, _cameraDepth);//给cmd写入一个这样的指令：
                    //给cmd添加指令： 颜色数据就传入_cameraColor,深度数据就传入_CameraDepth

                    var matProp = new MaterialPropertyBlock();//这里是创建了一个材质属性的数据包 里面即将存储一大批不同的数据
                    _computeBuffer.SetData(_marks);//将需要存进刚刚的数据包里面的数据拷贝，把他们从CPU内存拷贝到GPU显存
                    //这些数据包含的是  每个标志的具体位置 和 他们标志的类型
                    matProp.SetBuffer("markBuffer", _computeBuffer);//将_computerBuffer里面的数据输入到DrawMark.shader这个shader里面的一个叫markBuffer的变量里面  
                    _commandData[0].indexCountPerInstance = 6;
                    _commandData[0].instanceCount = horizentalCount * verticalCount;
                    _graphicsBuffer.SetData(_commandData);  //告诉图像缓冲区 每次渲染一个单位需要使用6个顶点 而总共需要渲染3500个这样的单位
                    cmd.DrawMeshInstancedIndirect(mesh, 0, settings.markMaterial, 0, _graphicsBuffer, 0, matProp);
                }
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);

        }

        ~CustomRenderPass()
        {  //析构函数 当使用了GPU资源的时候就需要使用析构函数手动回收  要不然会内存泄露
            _graphicsBuffer.Dispose();
            _computeBuffer.Dispose();
        }


    }
    /*************************************************************************/






    //当RendererFeature被创建、激活、改变参数时调用create函数
    public override void Create()
    {
        if (settings.scanMaterial == null) return;//如果
        if (!Application.isPlaying) return;//如果不是游戏模式就不会执行


        _marks = new Marks[horizentalCount * verticalCount];//因为有 这么多的需要渲染的单位 所以建立了这么多个数组

        _myPass = new CustomRenderPass(settings);
        _instance = this;

        //create函数里面应该放置的是一些需要声明的Pass实例  这里放置的是自己手搓的一个渲染Pass
        //
        //以及只需要制作一次的准备活动 比如说做一个数组
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (settings.scanMaterial == null) return;

        if (!Application.isPlaying) return;

        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            _myPass.renderPassEvent = settings.renderEvent;//这个是用来调整渲染队列的
            //声明一下自己需要使用颜色 法线 和深度缓冲区
            _myPass.ConfigureInput(ScriptableRenderPassInput.Color);
            _myPass.ConfigureInput(ScriptableRenderPassInput.Normal);
            _myPass.ConfigureInput(ScriptableRenderPassInput.Depth);

        }

    }


    //这个方法是对每一个相机调用一次 用来注入ScriptableRenderPass
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.scanMaterial == null) return;
        if (!Application.isPlaying) return;

        //注入CustomRenderPass 这样每一帧就会调用CustomRenderPass的Execute()方法
        renderer.EnqueuePass(_myPass);   //CustomRenderPass会自带Execute方法 之前把他拿去重写了 每一帧自动去调用它



    }

}
//URP管线的完整生命周期———— create - SetupRenderPasses -  AddRenderPasses  -  每一帧执行Pass
