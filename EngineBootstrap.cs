using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace BF4Launcher;
public static class EngineBootstrap {
 public static void Ensure(string root) {
  using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BF4Launcher.EnginePayload");if(stream==null)return;
  using var memory=new MemoryStream();stream.CopyTo(memory);var bytes=memory.ToArray();var fingerprint=Convert.ToHexString(SHA256.HashData(bytes));
  var marker=Path.Combine(root,"data","engine-state.json");
  using var zip=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);
  // Verify shipped components even when the version marker is unchanged.
  foreach(var entry in zip.Entries){if(entry.FullName.StartsWith("engine/builtin-mods/",StringComparison.OrdinalIgnoreCase)||entry.FullName.Equals("engine/builtin-mod-catalog.json",StringComparison.OrdinalIgnoreCase)||entry.FullName.StartsWith("mods/",StringComparison.OrdinalIgnoreCase))throw new Exception("模组文件应放在独立的 mods 目录，不随程序组件解压。");if(entry.FullName.EndsWith('/'))continue;var relative=entry.FullName.Replace('/',Path.DirectorySeparatorChar);var file=Path.GetFullPath(Path.Combine(root,relative));if(!file.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("程序组件路径不正确");if(!entry.FullName.StartsWith("host/",StringComparison.Ordinal)||entry.FullName.StartsWith("host/runtime/",StringComparison.Ordinal)||entry.FullName.StartsWith("host/settings/",StringComparison.Ordinal)){if(!entry.FullName.StartsWith("engine/",StringComparison.Ordinal))throw new Exception("程序组件目录不正确");}
   if(File.Exists(file)){using var existing=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);if(existing.Length==entry.Length){using var packed=entry.Open();if(SHA256.HashData(existing).SequenceEqual(SHA256.HashData(packed)))continue;}}
   Directory.CreateDirectory(Path.GetDirectoryName(file)!);var temp=file+".component.tmp";using(var input=entry.Open())using(var output=File.Create(temp))input.CopyTo(output);File.Move(temp,file,true);
  }
  Directory.CreateDirectory(Path.GetDirectoryName(marker)!);File.WriteAllText(marker,JsonSerializer.Serialize(new{fingerprint,version="0.3.0-1107",at=DateTime.UtcNow}));
 }
}



