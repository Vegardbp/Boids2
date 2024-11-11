using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ComputeController : MonoBehaviour
{
    public ComputeShader rendererCompute;
    public ComputeShader blurCompute;
    public ComputeShader physicsCompute;
    public ComputeShader brainCompute;
    public RenderTexture texture = null;
    public RawImage imageTarget;
    public bool run;
    public bool setUp;
    public int boidCount;

    public float blurFallOff;
    public float searchDistance;
    public float searchInfluence;
    public float speed;
    public float targetFramerate = 60;

    ComputeBuffer boidBuffer;

    const int sizeOfBoid = sizeof(float) * 4;
    struct Boid
    {
        Vector2 pos;
        Vector2 velocity;
        public Boid(Vector2 pos,Vector2 velocity)
        {
            this.pos = pos;
            this.velocity = velocity;
        }
    }
    void Start()
    {
        SetUp();
    }

    
    void Update()
    {
        if (setUp)
            SetUp();
        if (run)
            Run();
    }

    void Run()
    {
        Application.targetFrameRate = (int)targetFramerate;
        RunPhysics();
        Render();
        Blur();
        RunBrain();
    }

    void RunBrain()
    {
        SetBoidBuffer(ref brainCompute);
        brainCompute.SetTexture(0, "Render", texture);
        brainCompute.SetFloat("speed", speed);
        brainCompute.SetFloat("searchDistance", searchDistance);
        brainCompute.SetFloat("searchInfluence", searchInfluence);
        int threadGroupCount = boidCount / 1023 + 1;
        brainCompute.Dispatch(0, threadGroupCount, 1, 1);
    }

    void RunPhysics()
    {
        SetBoidBuffer(ref physicsCompute);
        physicsCompute.SetFloat("dt", 1.0f/targetFramerate);
        int threadGroupCount = boidCount / 1023 + 1;
        physicsCompute.Dispatch(0, threadGroupCount, 1, 1);
    }

    void Render()
    {
        SetBoidBuffer(ref rendererCompute);
        rendererCompute.SetTexture(0, "Render", texture);
        int threadGroupCount = boidCount / 1023 + 1;
        rendererCompute.Dispatch(0, threadGroupCount, 1, 1);
        imageTarget.texture = texture;
    }

    void Blur()
    {
        blurCompute.SetTexture(0, "Render", texture);
        blurCompute.SetFloat("dt", 1.0f / targetFramerate);
        blurCompute.SetFloat("blurFallOff", blurFallOff);
        blurCompute.Dispatch(0, 1920/8+1, 1080/8+1, 1);
    }

    void SetUpBoidBuffer()
    {
        boidBuffer?.Release();
        boidBuffer = new ComputeBuffer(boidCount, sizeOfBoid);
        List<Boid> boidList = new List<Boid>();
        for(int i = 0; i < boidCount; i++)
        {
            Vector2 pos = new Vector2(1920/2, 1080/2);
            Vector2 velocity = new Vector2(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
            boidList.Add(new Boid(pos, velocity));
        }
        boidBuffer.SetData(boidList.ToArray());
    }
    void SetBoidBuffer(ref ComputeShader shader)
    {
        shader.SetBuffer(0, "boids", boidBuffer);
        shader.SetInt("boidCount", boidCount);
    }

    void CreateRenderTexture(ref RenderTexture tex, Vector2Int resolution, RenderTextureFormat format)
    {
        DestroyImmediate(tex);
        tex = new RenderTexture(resolution.x, resolution.y, 1, format);
        tex.enableRandomWrite = true;
        tex.Create();
    }

    void SetUp()
    {
        setUp = false;
        SetUpBoidBuffer();
        CreateRenderTexture(ref texture, new Vector2Int(1920, 1080), RenderTextureFormat.ARGBFloat);
    }
}
