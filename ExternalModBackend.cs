using System;
using System.Threading.Tasks;
namespace BF4Launcher;
public partial class Backend {
 ExternalModHost? externalMods;
 public void EnsureExternalMods(){externalMods??=new ExternalModHost(repo.Root,s=>Output?.Invoke(s));externalMods.Ensure();}
 public string? ExternalModFeedback(ModItem mod)=>externalMods?.Feedback(mod.Key);
 public async Task StopExternalModsAsync(){if(externalMods!=null)await externalMods.StopAllAsync();}
 public async Task CloseExternalModsAsync(){if(externalMods!=null){await externalMods.CloseAsync();externalMods=null;}}
}
