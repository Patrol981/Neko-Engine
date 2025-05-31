if not exist CompiledShaders mkdir CompiledShaders
if not exist CompiledShaders/Vulkan mkdir CompiledShaders/Vulkan
if not exist TranspiledShaders mkdir TranspiledShaders
if not exist TranspiledShaders/Vulkan mkdir TranspiledShaders/Vulkan

cd ./Dwarf.ShaderLanguage/
call cargo run ../Shaders/Vulkan ../TranspiledShaders/Vulkan
cd ..
for %%i in (TranspiledShaders\Vulkan\*.frag TranspiledShaders\Vulkan\*.vert) do glslang --target-env vulkan1.4 --glsl-version 460 %%i -o CompiledShaders\Vulkan\%%~ni.spv