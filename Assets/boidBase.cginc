struct Boid{
	float2 pos;
	float2 velocity;
};

RWStructuredBuffer<Boid> boids;
int boidCount;