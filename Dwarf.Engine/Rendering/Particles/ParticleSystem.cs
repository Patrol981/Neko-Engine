using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dwarf.AbstractionLayer;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.Particles;
using Dwarf.Utils;
using Dwarf.Vulkan;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering.Particles;

public class ParticleSystem : SystemBase {
  public const int ParticleMargin = 100;
  private int _currentCapacity = 0;
  private int _requiredCapacity = 0;
  private static HashSet<Guid> s_textures = [];
  private static List<Particle> s_particles = [];
  private readonly unsafe ParticlePushConstant* _particlePushConstant =
    (ParticlePushConstant*)Marshal.AllocHGlobal(Unsafe.SizeOf<ParticlePushConstant>());
  private VulkanDescriptorSetLayout _textureLayout = null!;

  private TextureManager _textureManager = null!;

  public ParticleSystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    IDescriptorSetLayout globalSetLayout,
    IPipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, configInfo) {
    _textureLayout = new VulkanDescriptorSetLayout.Builder(device)
      .AddBinding(0, DescriptorType.SampledImage, ShaderStageFlags.Fragment)
      .AddBinding(1, DescriptorType.Sampler, ShaderStageFlags.Fragment)
      .Build();

    IDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
    ];

    IDescriptorSetLayout[] texturedSetLayouts = [
      _textureLayout,
      globalSetLayout
    ];

    AddPipelineData<ParticlePushConstant>(new() {
      RenderPass = renderer.GetPostProcessingPass(),
      VertexName = "particle_vertex",
      FragmentName = "particle_fragment",
      PipelineProvider = new ParticlePipelineProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
      PipelineName = "BaseParticle"
    });

    AddPipelineData<ParticlePushConstant>(new() {
      RenderPass = renderer.GetPostProcessingPass(),
      VertexName = "particle_textured_vertex",
      FragmentName = "particle_textured_fragment",
      PipelineProvider = new ParticlePipelineProvider(),
      DescriptorSetLayouts = texturedSetLayouts,
      PipelineName = "TexturedParticle"
    });
  }

  public void Setup(ref TextureManager textureManager) {
    _textureManager ??= textureManager;

    int requiredCapacity = s_particles.Count + ParticleMargin;

    if (_currentCapacity >= requiredCapacity) {
      Logger.Info("Particle capacity is sufficient, no need to recreate resources.");
      return;
    }

    if (s_particles.Count < 1) {
      Logger.Warn("Particles that are capable of using particle renderer are less than 1, thus Particle Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating Particle System");

    _currentCapacity = requiredCapacity;

    _descriptorPool = new VulkanDescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets((uint)s_particles.Count)
      .AddPoolSize(DescriptorType.UniformBuffer, (uint)s_particles.Count)
      .SetPoolFlags(DescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _requiredCapacity = requiredCapacity;
    _texturesCount = s_textures.Count;

    if (_texturesCount > 0) {
      _texturePool = new VulkanDescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets((uint)_texturesCount)
      .AddPoolSize(DescriptorType.SampledImage, (uint)_texturesCount)
      .AddPoolSize(DescriptorType.Sampler, (uint)_texturesCount)
      .SetPoolFlags(DescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

      foreach (var textureId in s_textures) {
        var targetTexture = textureManager.GetTextureLocal(textureId);
        targetTexture.BuildDescriptor(_textureLayout, _texturePool);
      }
    }
  }

  public void Update() {
    for (int i = 0; i < s_particles.Count; i++) {
      if (!s_particles[i].Update()) {
        s_particles[i].MarkToDispose();
      }
    }
  }

  public void Render(FrameInfo frameInfo) {
    var tmp = s_particles.ToArray();
    var basic = tmp.Where(x => !x.HasTexture).ToArray();
    var textured = tmp.Where(x => x.HasTexture).ToArray();
    RenderBasic(frameInfo, basic);
    RenderTextured(frameInfo, textured);
  }

  private void RenderTextured(FrameInfo frameInfo, Particle[] particles) {
    if (particles.Length < 1) return;

    ITexture? prevTexture = null;

    BindPipeline(frameInfo.CommandBuffer, "TexturedParticle");
    Descriptor.BindDescriptorSet(
      frameInfo.GlobalDescriptorSet,
      frameInfo,
      _pipelines["TexturedParticle"].PipelineLayout,
      1,
      1
    );

    bool validDesc = true;
    for (int i = 0; i < particles.Length; i++) {
      unsafe {
        _particlePushConstant->Color = Vector4.One;
        _particlePushConstant->Position = new Vector4(particles[i].Position, 1.0f);
        _particlePushConstant->Radius = particles[i].Scale;
        _particlePushConstant->Rotation = particles[i].Rotation;

        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          _pipelines["TexturedParticle"].PipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<ParticlePushConstant>(),
          _particlePushConstant
        );
      }

      if (prevTexture != particles[i].ParticleTexture) {
        prevTexture = particles[i].ParticleTexture;
        ;
        // if (vkTexture.VkTextureDescriptor.IsNull) {

        var vkTexture = prevTexture!;
        if (vkTexture.TextureDescriptor == 0) {
          validDesc = false;
        } else {
          Descriptor.BindDescriptorSet(
            prevTexture!.TextureDescriptor,
            frameInfo,
            _pipelines["TexturedParticle"].PipelineLayout,
            0,
            1
          );
        }
      }

      if (validDesc) {
        vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
      }
    }
  }

  private void RenderBasic(FrameInfo frameInfo, Particle[] particles) {
    if (particles.Length < 1) return;

    BindPipeline(frameInfo.CommandBuffer, "BaseParticle");
    Descriptor.BindDescriptorSet(
      frameInfo.GlobalDescriptorSet,
      frameInfo,
      _pipelines["BaseParticle"].PipelineLayout,
      0,
      1
    );

    for (int i = 0; i < particles.Length; i++) {
      unsafe {
        _particlePushConstant->Color = Vector4.One;
        _particlePushConstant->Position = new Vector4(particles[i].Position, 1.0f);
        _particlePushConstant->Radius = particles[i].Scale;
        _particlePushConstant->Rotation = particles[i].Rotation;

        vkCmdPushConstants(
          frameInfo.CommandBuffer,
          _pipelines["BaseParticle"].PipelineLayout,
          VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
          0,
          (uint)Unsafe.SizeOf<ParticlePushConstant>(),
          _particlePushConstant
        );
      }

      vkCmdDraw(frameInfo.CommandBuffer, 6, 1, 0, 0);
    }
  }

  public void AddParticle(Particle particle) {
    s_particles.Add(particle);
    CreateTextureResources(particle);
  }

  public void AddParticles(ParticleBatch batch) {
    s_particles.AddRange(batch.Particles);
    CreateTextureResources(batch.Particles[0]);
  }

  public void CreateTextureResources(Particle particle) {
    if (!particle.HasTexture) return;

    var targetId = _textureManager.GetTextureIdLocal(particle.ParticleTexture!.TextureName);
    if (s_textures.Add(targetId)) {
      Application.Instance.Systems.ReloadParticleSystem = true;
    }
  }

  public bool ValidateTextures() {
    return _texturesCount >= s_particles.Count;
  }

  public bool Validate() {
    return _requiredCapacity >= s_particles.Count;
  }

  public void Collect() {
    for (int i = 0; i < s_particles.Count; i++) {
      if (s_particles[i].CanBeDisposed) {
        s_particles.RemoveAt(i);
      }
    }
  }

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _device.WaitDevice();

    _textureLayout?.Dispose();

    MemoryUtils.FreeIntPtr<ParticlePushConstant>((nint)_particlePushConstant);

    base.Dispose();
  }
}