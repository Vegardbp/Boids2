using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//this script is attatched to the "rawImage" object in the scene

public class ComputeController : MonoBehaviour
{
    public ComputeShader rendererCompute; //compute shader that renders every boid
    public ComputeShader blurCompute; //compute shader that blurs the rendered result, and decays it over time
    public ComputeShader physicsCompute; //compute shader that updates every boids position based on velocity
    public ComputeShader brainCompute; //compute shader that runs boid behavior on every boid
    public RenderTexture texture = null; //rendered reult, this doesn't need to be public, but can be nice for debugging
    //the data in this texture will only ever exist on the gpu, so compute shaders are used to update it
    public RawImage imageTarget; //the raw image that will display the result

    public bool run;
    public bool setUp;
    //perameters
    public int boidCount;

    public float blurFallOff;
    public float searchDistance;
    public float searchInfluence;
    public float speed;
    public float targetFramerate = 60;

    ComputeBuffer boidBuffer; //buffer that holds every boid

    const int sizeOfBoid = sizeof(float) * 4; //how big the Boid steruct is in actual bits
    struct Boid //boid struct that will be used in the boid buffer
    {
        Vector2 pos;
        Vector2 velocity;
        public Boid(Vector2 pos,Vector2 velocity) //constructor
        {
            this.pos = pos;
            this.velocity = velocity;
        }
    }

    //runs on start
    void Start()
    {
        SetUp(); //set up when started
    }

    //loops every frame
    void Update()
    {
        if (setUp)
            SetUp(); //set up if specified
        if (run)
            Run(); //run the code
    }

    void Run()
    {
        Application.targetFrameRate = (int)targetFramerate;
        RunPhysics(); //first applies velocity to every boids position
        Render(); //renders the result
        Blur(); //blurs the new result. the texture never gets cleared,
                //so old pixels will get blurred again, making them decay over time
        RunBrain(); //run the boid behavior, this can be reprogramed for different boid behaviours, like gravity, or collision
    }

    void RunBrain()
    {
        SetBoidBuffer(ref brainCompute); //this sets the boid buffer to the correct buffer for any compute shader that has #include boidBase.cginc
        //set all parameters that are used to define the behaviour
        brainCompute.SetTexture(0, "Render", texture);
        brainCompute.SetFloat("speed", speed);
        brainCompute.SetFloat("searchDistance", searchDistance);
        brainCompute.SetFloat("searchInfluence", searchInfluence);
        int threadGroupCount = boidCount / 1023 + 1; //calculate how many thread groups are needed
        //since the compute shaders thread group size is 1023x1x1, it needs at least 1x1x1 thread groups to run 1023 boids,
        //or at least 10x1x1 thread groups to run 10230 boids
        brainCompute.Dispatch(0, threadGroupCount, 1, 1); //tell the gpu to run the compute shader
    }

    void RunPhysics()
    {
        SetBoidBuffer(ref physicsCompute);//this sets the boid buffer to the correct buffer for any compute shader that has #include boidBase.cginc
        //set all parameters that are used to define the behaviour
        physicsCompute.SetFloat("dt", 1.0f/targetFramerate); //set the delta time, so velocity is framerate independent
        //however, for this, its using a fixed delta time, because a lag spike could cause large skips in position
        int threadGroupCount = boidCount / 1023 + 1;//calculate how many thread groups are needed
        //since the compute shaders thread group size is 1023x1x1, it needs at least 1x1x1 thread groups to run 1023 boids,
        //or at least 10x1x1 thread groups to run 10230 boids
        physicsCompute.Dispatch(0, threadGroupCount, 1, 1); //tell the gpu to run the compute shader
    }

    void Render()
    {
        SetBoidBuffer(ref rendererCompute);//this sets the boid buffer to the correct buffer for any compute shader that has #include boidBase.cginc
        //set all parameters that are used to define the behaviour
        rendererCompute.SetTexture(0, "Render", texture); //set the texture so it can be rendered to
        int threadGroupCount = boidCount / 1023 + 1;//calculate how many thread groups are needed
        //since the compute shaders thread group size is 1023x1x1, it needs at least 1x1x1 thread groups to run 1023 boids,
        //or at least 10x1x1 thread groups to run 10230 boids
        rendererCompute.Dispatch(0, threadGroupCount, 1, 1); //tell the gpu to run the compute shader
        imageTarget.texture = texture; //update the raw image texture with the new rendered texture, idk if this has to happen every frame
    }

    void Blur()
    {
        blurCompute.SetTexture(0, "Render", texture); //sets the texture to blur it
        //set parameters to define how much it decays
        blurCompute.SetFloat("dt", 1.0f / targetFramerate);
        blurCompute.SetFloat("blurFallOff", blurFallOff);

        blurCompute.Dispatch(0, 1920/8+1, 1080/8+1, 1);//tell the gpu to run the code
        //here, the thread group size is 8x8x1, so it needs at least (1920/8+1)x(1080/8+1)x1 to blur a 1920x1080 resolution texture
        //for this speciffic resolution, the +1 is not really needed, but it almost doesn't affect performance to have
    }

    void SetUpBoidBuffer() //note, this only runs in setUp, so it only happens once, and not every frame
    {
        boidBuffer?.Release(); //release the buffer if it is not already released( != null)
        //this tells the gpu to delete the data in it. unity will do this for you, but releasing is good practice anyway
        boidBuffer = new ComputeBuffer(boidCount, sizeOfBoid); //create the buffer with "boidCount" elements that are "sizeOfBoid" bits big
        List<Boid> boidList = new List<Boid>(); //create a list of boids to fill the buffer with
        for(int i = 0; i < boidCount; i++) //fill the list
        {
            Vector2 pos = new Vector2(1920/2, 1080/2);
            Vector2 velocity = new Vector2(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
            boidList.Add(new Boid(pos, velocity));
        }
        boidBuffer.SetData(boidList.ToArray()); //put the list into the buffer. This isnt neccesary, as it can be done in the compute shader instead, but I'm lazy
        //note, this can only be done like this because boidList is "blittable", meaning it has a predictable size and structure.
        //if, for example, the Boid struct contained both floats, ints, and arrays, it wouldn't be blittable, and it'd have to be
        //manually "flattened" into an array or a list of ints or bytes. Not that every variable has to be converted into ints, but
        //the actual bit content of the variable would be placed directly into the int, so a float (0101) would be converted into an
        //int (0101), and the actual value of the number is no longer the same on the cpu, but since the gpu recieves it as a float,
        //it will have the correct value on the GPU. Ideally however, for performance, just using multiple buffers is usually better
        //than flattening.
    }
    void SetBoidBuffer(ref ComputeShader shader)
    {
        shader.SetBuffer(0, "boids", boidBuffer); //set the buffer "boids" to boidBuffer
        shader.SetInt("boidCount", boidCount); //tell the gpu how many boids there are
        //this works for any compute shader that has #include boidBase.cginc
    }

    void CreateRenderTexture(ref RenderTexture tex, Vector2Int resolution, RenderTextureFormat format)
    {
        DestroyImmediate(tex); //destroy the render texture just in case you already had one
        tex = new RenderTexture(resolution.x, resolution.y, 1, format); //create the texture
        tex.enableRandomWrite = true; //enable random write, which allows you to write directly to it on the gpu
        tex.Create(); //create it
        //we have to do this because unity doesnt allow you to enable random write on a texture that has already been created
    }

    void SetUp()
    {
        setUp = false; //set setUp to false so it only happens once
        //using a custom editor script would be better, but I'm lazy
        SetUpBoidBuffer(); //create the boid buffer and fill it with data
        CreateRenderTexture(ref texture, new Vector2Int(1920, 1080), RenderTextureFormat.ARGBFloat);
        //create the render texture, RenderTextureFormat is set to ARGBFloat, meaning it has 4 floats, aka, 
        //the gpu has a float4 variable for every pixel.
        //this texture is currently empty, and only exists on the gpu, where it will stay until it fucking dies
    }
}
