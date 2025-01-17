float hash(uint x, int seed){
    seed *= x;
    seed = (seed ^ 61) ^ (seed >> 16);
    seed *= 9;
    seed = seed ^ (seed >> 4);
    seed *= 0x27d4eb2d;
    seed = seed ^ (seed >> 15);
    float outVal = seed/10000000.0f;
    outVal -= (int)outVal;
    return outVal;
}

float2 randomVector(uint2 id){
    return float2(hash(id.x,1.0), hash(id.y,1.0));
}

float2 randomVector(uint id){
    return float2(hash(id.x,1.0), hash(id.x*4372.123,1.0));
}