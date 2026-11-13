using System;using System.Collections.Generic;
class RecoveryTests{
 static int count;static void Check(bool ok,string name){count++;if(!ok)throw new Exception("FAIL "+name);}
 static bool Same(byte[] a,byte[] b){return BitConverter.ToString(a)==BitConverter.ToString(b);}
 static int Main(){try{
  var bindings=new[]{new DeathFastRecovery.Binding(100,new byte[]{1,2},new byte[]{3,4}),new DeathFastRecovery.Binding(200,new byte[]{5,6},new byte[]{7,8})};
  var mem=new Dictionary<long,byte[]>{{100,new byte[]{1,2}},{200,new byte[]{7,8}}};
  var writes=new List<long>();Func<long,int,byte[]> read=(a,n)=>mem[a];Action<long,byte[],byte[]> patch=(a,old,next)=>{if(!Same(mem[a],old))throw new Exception("signature");writes.Add(a);mem[a]=next;};
  DeathFastRecovery.Restore(bindings,read,patch);Check(writes.Count==1&&writes[0]==200&&Same(mem[200],bindings[1].Original),"partial install restores only owned bytes");
  writes.Clear();DeathFastRecovery.Restore(bindings,read,patch);Check(writes.Count==0,"repeat restore is harmless");
  mem[100]=new byte[]{3,4};mem[200]=new byte[]{99,99};writes.Clear();bool refused=false;try{DeathFastRecovery.Restore(bindings,read,patch);}catch{refused=true;}Check(refused&&writes.Count==0,"conflict is checked before any restoration");
  mem[100]=new byte[]{3,4};mem[200]=new byte[]{7,8};writes.Clear();bool failed=false;try{DeathFastRecovery.Restore(bindings,read,(a,old,next)=>{writes.Add(a);if(a==100)throw new Exception("write failure");mem[a]=next;});}catch{failed=true;}Check(failed&&writes.Count==2&&Same(mem[200],bindings[1].Original),"one failure does not suppress other restoration");
  Check(DeathFastRecovery.Recoverable("prepared")&&DeathFastRecovery.Recoverable("restore-failed")&&DeathFastRecovery.Recoverable("installed"),"interrupted states accepted");
  Check(!DeathFastRecovery.Recoverable("unknown"),"unknown state not accepted");
  Console.WriteLine(count+" recovery cases; failures=0");return 0;
 }catch(Exception e){Console.WriteLine(e.Message);return 1;}}
}
