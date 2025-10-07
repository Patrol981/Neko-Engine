using System.Runtime.InteropServices;
using Dwarf;
using Dwarf.EntityComponentSystem;
using Dwarf.Native;

public static partial class DwarfNativeInterop {

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void DwarfScriptEventNative();

  public static ActionResult Dwarf_Entity_AddTransform(
    string entityId,
    float pX, float pY, float pZ,
    float rX, float rY, float rZ,
    float sX, float sY, float sZ
  ) {
    var target = Application.Instance.GetEntity(Guid.Parse(entityId));
    if (target is null) {
      return ActionResult.Error;
    }
    target.AddTransform([pX, pY, pZ], [rX, rY, rZ], [sX, sY, sZ]);
    return ActionResult.Success;
  }

  public static ActionResult Dwarf_Entity_Script_Add(string entityId) {
    var target = Application.Instance.GetEntity(Guid.Parse(entityId));
    if (target is null) {
      return ActionResult.Error;
    }

    target.AddScript(new DwarfScriptInterpreted());
    return ActionResult.Success;
  }

  // [UnmanagedCallersOnly(EntryPoint = "Dwarf_Entity_Script_SetAwake")]
  // public unsafe static ActionResult Dwarf_Entity_Script_SetAwake(char* entityId, DwarfScriptEvent @event) {
  //   var target = Application.Instance.GetEntity(Guid.Parse(entityId));
  //   if (target is null) {
  //     return ActionResult.Error;
  //   }

  //   var script = target.GetScript<DwarfScriptInterpreted>();
  //   if (script is null) {
  //     return ActionResult.Error;
  //   }

  //   script.OnAwake += @event;
  //   return ActionResult.Success;
  // }
}