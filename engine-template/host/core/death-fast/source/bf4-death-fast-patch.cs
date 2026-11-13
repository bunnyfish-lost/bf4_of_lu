using System;using System.Collections.Generic;
public static class DeathFastPatch{
 sealed class Code{
  public List<byte> B=new List<byte>();Dictionary<string,int> labels=new Dictionary<string,int>();List<KeyValuePair<int,string>> fixups=new List<KeyValuePair<int,string>>();
  public void Add(params byte[] bytes){B.AddRange(bytes);}public void I(int n){Add(BitConverter.GetBytes(n));}public void Label(string s){labels.Add(s,B.Count);}public void Jump(byte op,string s){Add(op,0);fixups.Add(new KeyValuePair<int,string>(B.Count-1,s));}public void Tail(long address){Add(0x49,0xbb);Add(BitConverter.GetBytes(address));Add(0x41,0xff,0xe3);}public byte[] Finish(){foreach(var f in fixups){int d=labels[f.Value]-f.Key-1;if(d< -128||d>127)throw new InvalidOperationException("branch too far");B[f.Key]=unchecked((byte)(sbyte)d);}return B.ToArray();}
 }
 // Win64 scheduler: RCX manager, RDX entity, XMM2 duration, R9 event.
 public static byte[] Delay(long original){var c=new Code();c.Add(0x48,0x8b,0x42,0x28,0x48,0x85,0xc0);c.Jump(0x74,"tail");c.Add(0x81,0x78,0x10);c.I(440549240);c.Jump(0x75,"tail");c.Add(0x81,0x78,0x18);c.I(0x41100000);c.Jump(0x75,"tail");c.Add(0xb8);c.I(0x3f800000);c.Add(0x66,0x0f,0x6e,0xd0);c.Label("tail");c.Tail(original);return c.Finish();}
 // Preserve all other gates. Timer In1 advances this gate; server In2 is ignored.
 public static byte[] Gate(long original,long fire){var c=new Code();c.Add(0x48,0x8b,0x41,0x28,0x48,0x85,0xc0);c.Jump(0x74,"tail");c.Add(0x81,0x78,0x10);c.I(36312304);c.Jump(0x75,"tail");c.Add(0x81,0x7a,0x08);c.I(0x0b87d373);c.Jump(0x74,"forward");c.Add(0x81,0x7a,0x08);c.I(0x0b87d370);c.Jump(0x74,"return");c.Label("tail");c.Tail(original);c.Label("forward");c.Add(0x48,0x8b,0x51,0x20,0x49,0x89,0xc0,0x41,0xb9);c.I(0x0b87df4b);c.Tail(fire);c.Label("return");c.Add(0xc3);return c.Finish();}
 public static byte[] Call(long site,long target){long d=target-site-5;if(d<int.MinValue||d>int.MaxValue)throw new ArgumentOutOfRangeException("target");byte[] b=new byte[5];b[0]=0xe8;Array.Copy(BitConverter.GetBytes((int)d),0,b,1,4);return b;}
}
