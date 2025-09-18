#version 430 core

// Input from vertex shader
in vec4 vertexColor;
in float vertexSize;
in vec2 texCoords;

// TBO for additional noise/pattern data
uniform samplerBuffer noiseTexture;
uniform float time;

// Output
out vec4 FragColor;

void main()
{
    // Calculate distance from center for circular particles
    vec2 center = vec2(0.5, 0.5);
    float dist = length(texCoords - center);
    
    // Create soft circular fade
    float alpha = 1.0 - smoothstep(0.3, 0.5, dist);
    
    // Sample noise from TBO for particle texture variation
    int noiseIndex = int((texCoords.x + texCoords.y + time * 0.1) * 127.0) % 512;
    vec4 noise = texelFetch(noiseTexture, noiseIndex);
    
    // Apply noise to create more interesting particle appearance
    vec3 finalColor = vertexColor.rgb;
    finalColor += noise.xyz * 0.1; // Subtle noise overlay
    
    // Add some sparkle effect near the edges
    float sparkle = pow(1.0 - dist, 3.0) * (sin(time * 10.0 + noise.w * 50.0) * 0.5 + 0.5);
    finalColor += vec3(sparkle * 0.3);
    
    // Combine alpha
    float finalAlpha = vertexColor.a * alpha;
    
    // Discard completely transparent fragments
    if (finalAlpha < 0.01)
        discard;
    
    FragColor = vec4(finalColor, finalAlpha);
}