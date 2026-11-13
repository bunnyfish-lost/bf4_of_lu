using System;using System.Linq;
public sealed class RosterPlayer{public string name,guid,teamId,type;}
public sealed class Roster{
 public long at;public bool ownedServer;public string server,endpoint;public RosterPlayer[] players;
 public static bool ValidEndpoint(string value){
  if(value==null)return false;int colon=value.LastIndexOf(':');if(colon<1||colon!=value.IndexOf(':'))return false;
  var parts=value.Substring(0,colon).Split('.');if(parts.Length!=4)return false;int item;foreach(var part in parts)if(part.Length==0||part.Any(ch=>ch<'0'||ch>'9')||!Int32.TryParse(part,out item)||item<0||item>255)return false;
  var port=value.Substring(colon+1);return port.Length>0&&!port.Any(ch=>ch<'0'||ch>'9')&&Int32.TryParse(port,out item)&&item>0&&item<=65535;
 }
 public bool Fresh(long now){return ownedServer&&at<=now&&now-at<=4000&&players!=null&&ValidEndpoint(endpoint);}
}
