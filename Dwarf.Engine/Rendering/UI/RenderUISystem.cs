using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.EntityComponentSystem;
using Dwarf.Extensions.Lists;
using Dwarf.Extensions.Logging;
using Dwarf.Rendering.UI;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf.Rendering;

public class RenderUISystem : SystemBase {
  private DwarfBuffer _uiBuffer = null!;

  public RenderUISystem(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    IDescriptorSetLayout globalSetLayout,
    IPipelineConfigInfo configInfo = null!
  ) : base(allocator, device, renderer, configInfo) {
    _setLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.AllGraphics)
      .Build();

    _textureSetLayout = new VulkanDescriptorSetLayout.Builder(_device)
      .AddBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)
      .Build();

    IDescriptorSetLayout[] descriptorSetLayouts = [
      globalSetLayout,
      _setLayout,
      _textureSetLayout,
    ];

    AddPipelineData<UIUniformObject>(new() {
      RenderPass = renderer.GetSwapchainRenderPass(),
      VertexName = "gui_vertex",
      FragmentName = "gui_fragment",
      PipelineProvider = new PipelineUIProvider(),
      DescriptorSetLayouts = descriptorSetLayouts,
    });

    // CreatePipelineLayout<UIUniformObject>(descriptorSetLayouts);
    // CreatePipeline(_renderer.GetSwapchainRenderPass(), "gui_vertex", "gui_fragment", new PipelineUIProvider());
  }

  /*
  public unsafe void Setup(ref TextureManager textureManager) {
    if (canvas == null) return;

    var entities = canvas.GetUI();

    if (entities.Length < 1) {
      Logger.Warn("Entities that are capable of using UI Rendering are less than 1, thus UI Render System won't be recreated");
      return;
    }

    Logger.Info("Recreating UI Renderer");

    _descriptorPool = new DescriptorPool.Builder((VulkanDevice)_device)
      .SetMaxSets((uint)entities.Length)
      .AddPoolSize(VkDescriptorType.UniformBuffer, (uint)entities.Length)
      .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
      .Build();

    _texturesCount = entities.Length;

    _texturePool = new DescriptorPool.Builder((VulkanDevice)_device)
    .SetMaxSets((uint)_texturesCount)
    .AddPoolSize(VkDescriptorType.CombinedImageSampler, (uint)_texturesCount)
    .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
    .Build();

    _uiBuffer = new DwarfBuffer(
      _allocator,
        _device,
        (ulong)Unsafe.SizeOf<UIUniformObject>(),
        (uint)entities.Length,
        BufferUsage.UniformBuffer,
        MemoryProperty.HostVisible | MemoryProperty.HostCoherent,
        ((VulkanDevice)_device).Properties.limits.minUniformBufferOffsetAlignment
      );
    _descriptorSets = new VkDescriptorSet[entities.Length];
    _textureSets = new();

    for (int x = 0; x < entities.Length; x++) {
      _textureSets.Add(new());
    }

    for (int i = 0; i < entities.Length; i++) {
      var targetUI = entities[i].GetDrawable<IUIElement>();
      BindDescriptorTexture(targetUI.Owner!, ref textureManager, i);

      var bufferInfo = _uiBuffer.GetDescriptorBufferInfo((ulong)Unsafe.SizeOf<UIUniformObject>());
      _ = new VulkanDescriptorWriter(_setLayout, _descriptorPool)
          .WriteBuffer(0, &bufferInfo)
          .Build(out _descriptorSets[i]);
    }
  }

  public unsafe void DrawUI(FrameInfo frameInfo, Canvas? canvas) {
    if (canvas == null) return;

    // _pipeline.Bind(frameInfo.CommandBuffer);
    BindPipeline(frameInfo.CommandBuffer);

    vkCmdBindDescriptorSets(
      frameInfo.CommandBuffer,
      VkPipelineBindPoint.Graphics,
      PipelineLayout,
      0,
      1,
      &frameInfo.GlobalDescriptorSet,
      0,
      null
    );

    var entities = canvas.GetUI();

    for (int i = 0; i < entities.Length; i++) {
      var uiPushConstant = new UIUniformObject {
        UIMatrix = entities[i].GetComponent<RectTransform>().Matrix4
      };

      vkCmdPushConstants(
        frameInfo.CommandBuffer,
        PipelineLayout,
        VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
        0,
        (uint)Unsafe.SizeOf<UIUniformObject>(),
        &uiPushConstant
      );

      var uiComponent = entities[i].GetDrawable<IUIElement>() as IUIElement;
      uiComponent?.Update();
      Descriptor.BindDescriptorSet(_textureSets.GetAt(i), frameInfo, PipelineLayout, 2, 1);
      uiComponent?.Bind(frameInfo.CommandBuffer, 0);
      uiComponent?.Draw(frameInfo.CommandBuffer, 0);
    }
  }

  public bool CheckSizes(ReadOnlySpan<Entity> entities, Canvas canvas) {
    if (_uiBuffer == null) {
      var textureManager = Application.Instance.TextureManager;
      Setup(canvas, ref textureManager);
    }
    if (entities.Length > (uint)_uiBuffer!.GetInstanceCount()) {
      return false;
    } else if (entities.Length < (uint)_uiBuffer.GetInstanceCount()) {
      return true;
    }

    return true;
  }

  public bool CheckTextures(ReadOnlySpan<Entity> entities) {
    var len = entities.Length;
    var sets = _textureSets.Size;
    return len == sets;
  }

  private unsafe void BindDescriptorTexture(Entity entity, ref TextureManager textureManager, int index) {
    var id = entity.GetDrawable<IUIElement>() as IUIElement;
    var texture = textureManager.GetTextureLocal(id!.GetTextureIdReference());

    VkDescriptorImageInfo imageInfo = new() {
      sampler = texture.Sampler,
      imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
      imageView = texture.ImageView
    };
    _ = new VulkanDescriptorWriter(_textureSetLayout, _texturePool)
      .WriteImage(0, &imageInfo)
      .Build(out VkDescriptorSet set);
    _textureSets.SetAt(set, index);
  }
  */

  public override unsafe void Dispose() {
    _device.WaitQueue();
    _uiBuffer?.Dispose();
    // _descriptorPool?.FreeDescriptors(_descriptorSets);
    // _texturePool?.FreeDescriptors(_textureSets);

    base.Dispose();
  }
}