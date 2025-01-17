struct Boid{ //define the struct, it is important that all of this is exactly the same as on the cpu
	//if pos is written last here, but first on the cpu (ComputeController.cs), you would get the velocity when you write boid.pos
	float2 pos;
	float2 velocity;
};

RWStructuredBuffer<Boid> boids; //a Structured buffer with all the boids, this is the same as a ComputeBuffer on the cpu
//the RW written in front is so it can be written to as well as read from, on the gpu.
Texture2D<float3> blockTexture;
int boidCount;