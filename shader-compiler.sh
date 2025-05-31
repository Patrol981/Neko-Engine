#!/bin/bash

# Create directories if they do not exist
mkdir -p CompiledShaders
mkdir -p CompiledShaders/Vulkan
mkdir -p TranspiledShaders
mkdir -p TranspiledShaders/Vulkan

# Navigate to the Neko.ShaderLanguage directory
cd ./Neko.ShaderLanguage/ || exit

# Run cargo with the given arguments
cargo run ../Shaders/Vulkan ../TranspiledShaders/Vulkan

# Return to the original directory
cd ..

# Compile GLSL shaders to SPIR-V
for i in TranspiledShaders/Vulkan/*.frag TranspiledShaders/Vulkan/*.vert; do
    base_name=$(basename "$i")  # Extract the file name
    output_name="CompiledShaders/Vulkan/${base_name%.*}.spv"  # Change extension to .spv

    # Compile shader
    glslang --target-env vulkan1.4 --glsl-version 460 "$i" -o "$output_name"
done
