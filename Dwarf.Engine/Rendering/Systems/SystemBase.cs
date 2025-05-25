
using System.Runtime.CompilerServices;

using Dwarf.AbstractionLayer;
using Dwarf.Rendering;
using Dwarf.Vulkan;

using Vortice.Vulkan;

using static Vortice.Vulkan.Vulkan;

namespace Dwarf;

public class PipelineData {
  public VkPipelineLayout PipelineLayout;
  public Pipeline Pipeline = null!;

  public unsafe void Dispose(IDevice device) {
    Pipeline.Dispose();

    if (PipelineLayout.IsNotNull) {
      vkDestroyPipelineLayout(device.LogicalDevice, PipelineLayout);
    }
  }
}

public class PipelineInputData<T> where T : struct {
  public T PushConstantType { get; } = default;
  public string PipelineName { get; set; } = SystemBase.DefaultPipelineName;
  public VkDescriptorSetLayout[] DescriptorSetLayouts = [];
  public string VertexName = string.Empty;
  public string FragmentName = string.Empty;
  public VkPipelineProvider PipelineProvider { get; set; } = null!;
  public VkRenderPass RenderPass { get; set; } = VkRenderPass.Null;
}

public class PipelineInputData {
  public string PipelineName { get; set; } = SystemBase.DefaultPipelineName;
  public VkDescriptorSetLayout[] DescriptorSetLayouts = [];
  public string VertexName = string.Empty;
  public string FragmentName = string.Empty;
  public VkPipelineProvider PipelineProvider { get; set; } = null!;
  public VkRenderPass RenderPass { get; set; } = VkRenderPass.Null;
}


public abstract class SystemBase {
  public const string DefaultPipelineName = "main";

  protected readonly IDevice _device = null!;
  protected readonly nint _allocator = IntPtr.Zero;
  protected readonly IRenderer _renderer = null!;
  protected VkPipelineConfigInfo _pipelineConfigInfo;
  protected Dictionary<string, PipelineData> _pipelines = [];

  protected DescriptorPool _descriptorPool = null!;
  protected DescriptorPool _texturePool = null!;
  protected DescriptorSetLayout _setLayout = null!;
  protected DescriptorSetLayout _textureSetLayout = null!;
  protected VkDescriptorSet[] _descriptorSets = [];

  protected int _texturesCount = 0;

  public SystemBase(
    nint allocator,
    IDevice device,
    IRenderer renderer,
    VkPipelineConfigInfo configInfo = null!
  ) {
    _allocator = allocator;
    _device = device;
    _renderer = renderer;

    _pipelineConfigInfo = configInfo ?? null!;
  }

  #region Pipeline

  protected unsafe void CreatePipelineLayout<T>(
    VkDescriptorSetLayout[] layouts,
    out VkPipelineLayout pipelineLayout
  ) {
    CreatePipelineLayoutBase(layouts, out var pipelineInfo);
    var push = CreatePushConstants<T>();
    pipelineInfo.pushConstantRangeCount = 1;
    pipelineInfo.pPushConstantRanges = &push;
    FinalizePipelineLayout(&pipelineInfo, out pipelineLayout);
  }

  protected unsafe void CreatePipelineLayout(
    VkDescriptorSetLayout[] layouts,
    out VkPipelineLayout pipelineLayout
  ) {
    CreatePipelineLayoutBase(layouts, out var pipelineInfo);
    FinalizePipelineLayout(&pipelineInfo, out pipelineLayout);
  }

  protected unsafe void CreatePipelineLayoutBase(
    VkDescriptorSetLayout[] layouts,
    out VkPipelineLayoutCreateInfo pipelineInfo
  ) {
    pipelineInfo = new() {
      setLayoutCount = (uint)layouts.Length
    };
    fixed (VkDescriptorSetLayout* ptr = layouts) {
      pipelineInfo.pSetLayouts = ptr;
    }
  }

  protected unsafe void FinalizePipelineLayout(
    VkPipelineLayoutCreateInfo* pipelineInfo,
    out VkPipelineLayout pipelineLayout
  ) {
    vkCreatePipelineLayout(
      _device.LogicalDevice,
      pipelineInfo,
      null,
      out pipelineLayout
    ).CheckResult();
  }

  protected unsafe VkPushConstantRange CreatePushConstants<T>() {
    VkPushConstantRange pushConstantRange = new() {
      stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
      offset = 0,
      size = (uint)Unsafe.SizeOf<T>()
    };

    return pushConstantRange;
  }

  protected unsafe void CreatePipeline(
    VkRenderPass renderPass,
    string vertexName,
    string fragmentName,
    VkPipelineProvider pipelineProvider,
    VkPipelineLayout pipelineLayout,
    out Pipeline pipeline
  ) {
    _pipelineConfigInfo ??= new VkPipelineConfigInfo();
    var pipelineConfig = _pipelineConfigInfo.GetConfigInfo();
    var colorFormat = _renderer.DynamicSwapchain.ColorFormat;
    var depthFormat = _renderer.DepthFormat;
    pipelineConfig.RenderPass = VkRenderPass.Null;
    pipelineConfig.PipelineLayout = pipelineLayout;
    pipeline = new Pipeline(_device, vertexName, fragmentName, pipelineConfig, pipelineProvider, depthFormat, colorFormat);
  }

  protected void AddPipelineData<T>(PipelineInputData<T> pipelineInput) where T : struct {
    _pipelines.TryAdd(
      pipelineInput.PipelineName,
      new()
    );

    CreatePipelineLayout<T>(
      pipelineInput.DescriptorSetLayouts,
      out _pipelines[pipelineInput.PipelineName].PipelineLayout
    );

    CreatePipeline(
      pipelineInput.RenderPass,
      pipelineInput.VertexName,
      pipelineInput.FragmentName,
      pipelineInput.PipelineProvider,
      _pipelines[pipelineInput.PipelineName].PipelineLayout,
      out _pipelines[pipelineInput.PipelineName].Pipeline
    );
  }

  protected void AddPipelineData(PipelineInputData pipelineInput) {
    _pipelines.TryAdd(
      pipelineInput.PipelineName,
      new()
    );

    CreatePipelineLayout(
      pipelineInput.DescriptorSetLayouts,
      out _pipelines[pipelineInput.PipelineName].PipelineLayout
    );

    CreatePipeline(
      pipelineInput.RenderPass,
      pipelineInput.VertexName,
      pipelineInput.FragmentName,
      pipelineInput.PipelineProvider,
      _pipelines[pipelineInput.PipelineName].PipelineLayout,
      out _pipelines[pipelineInput.PipelineName].Pipeline
    );
  }

  protected void BindPipeline(VkCommandBuffer commandBuffer, string pipelineName = DefaultPipelineName) {
    _pipelines[pipelineName].Pipeline?.Bind(commandBuffer);
  }

  #endregion

  public virtual unsafe void Dispose() {
    _device.WaitQueue();
    _setLayout?.Dispose();
    _textureSetLayout?.Dispose();
    _descriptorPool?.Dispose();
    _texturePool?.Dispose();
    foreach (var p in _pipelines) {
      p.Value.Dispose(_device);
    }
    _pipelines.Clear();
  }

  public VkPipelineLayout PipelineLayout => _pipelines.FirstOrDefault().Value.PipelineLayout;
}
