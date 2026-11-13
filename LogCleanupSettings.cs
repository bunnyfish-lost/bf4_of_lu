using System.Text.Json;
namespace BF4Launcher;
public partial class Repository {
 public void SetAutoCleanLogs(bool value){var previous=Config.AutoCleanLogs;Config.AutoCleanLogs=value;try{Atomic(ConfigPath,JsonSerializer.Serialize(Config,Json));}catch{Config.AutoCleanLogs=previous;throw;}}
}
