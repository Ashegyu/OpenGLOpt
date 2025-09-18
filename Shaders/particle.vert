#version 430 core

// Particle structure definition (outside buffer block)
struct Particle
{
    vec3 position;
    float life;
    vec3 velocity;
    float size;
    vec4 color;
};

// SSBO layout for particle data
layout(std430, binding = 0) buffer ParticleBuffer
{
    Particle particles[];
};

// Uniforms
uniform mat4 mvpMatrix;
uniform float time;
uniform samplerBuffer gradientTexture; // TBO for color gradient

// Output to fragment shader
out vec4 vertexColor;
out float vertexSize;
out vec2 texCoords;

void main()
{
    // Get particle data from SSBO
    Particle particle = particles[gl_InstanceID];
    
    // Calculate vertex position (billboard quad)
    vec2 quadVertices[4] = vec2[](
        vec2(-1.0, -1.0),
        vec2( 1.0, -1.0),
        vec2(-1.0,  1.0),
        vec2( 1.0,  1.0)
    );
    
    vec2 quadPos = quadVertices[gl_VertexID];
    texCoords = quadPos * 0.5 + 0.5; // Convert to [0,1] range
    
    // Billboard effect - always face camera
    vec3 worldPos = particle.position + vec3(quadPos * particle.size, 0.0);
    
    // Transform to clip space
    gl_Position = mvpMatrix * vec4(worldPos, 1.0);
    
    // Sample color from TBO based on life
    float lifeRatio = clamp(particle.life / 6.0, 0.0, 1.0);
    int gradientIndex = int(lifeRatio * 255.0);
    vertexColor = texelFetch(gradientTexture, gradientIndex);
    
    // Mix with particle's base color
    vertexColor *= particle.color;
    
    // Add some animation based on time
    float pulse = sin(time * 3.0 + particle.position.x) * 0.1 + 0.9;
    vertexColor.rgb *= pulse;
    
    vertexSize = particle.size;
}