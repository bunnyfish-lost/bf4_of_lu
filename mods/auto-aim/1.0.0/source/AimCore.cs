using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
public struct V3 {
 public double X,Y,Z;public V3(double x,double y,double z){X=x;Y=y;Z=z;}
 public bool Valid{get{return Good(X)&&Good(Y)&&Good(Z)&&Math.Abs(X)<100000&&Math.Abs(Y)<100000&&Math.Abs(Z)<100000;}}
 public static bool Good(double x){return !double.IsNaN(x)&&!double.IsInfinity(x);}
 public double Distance(V3 b){return Math.Sqrt((X-b.X)*(X-b.X)+(Y-b.Y)*(Y-b.Y)+(Z-b.Z)*(Z-b.Z));}
 public static V3 From(float[] v){return new V3(v[0],v[1],v[2]);}
}
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
public sealed class Candidate{public long Pointer;public string Name;public double X,Y,Angle,Distance;}
public sealed class EspMarker {public double Left,Top,Right,Bottom,Distance;public bool Occluded;}
public sealed class Scene {
 public List<EspMarker> Markers=new List<EspMarker>();public string Name,Status;public int Team,MatchedBots,Enemies,Visible;public bool Ready;
 public V3 Forward,Right,Up;public double FocalX,FocalY;
 public double FovX,FovY,ReadMs;public long Context,LocalSoldier,LocalVehicle;public int VehicleSeat;public List<Candidate> Targets=new List<Candidate>();
}
public static class AimMath {
 public static bool Project(V3 p,float[] vp,float[] proj,out double nx,out double ny,out double angle){
  nx=ny=angle=0;double w=vp[3]*p.X+vp[7]*p.Y+vp[11]*p.Z+vp[15];
  if(!p.Valid||!V3.Good(w)||w<=0.05||proj[0]<=0||proj[5]<=0)return false;
  nx=(vp[0]*p.X+vp[4]*p.Y+vp[8]*p.Z+vp[12])/w;
  ny=(vp[1]*p.X+vp[5]*p.Y+vp[9]*p.Z+vp[13])/w;
  angle=Math.Atan(Math.Sqrt(Math.Pow(nx/proj[0],2)+Math.Pow(ny/proj[5],2)))*180/Math.PI;
  return V3.Good(nx)&&V3.Good(ny)&&V3.Good(angle)&&Math.Abs(nx)<1&&Math.Abs(ny)<1;
 }
 public static bool FullViewport(double radius){return radius==90;}
 public static bool TriggerHeld(string trigger,bool left,bool right){return trigger=="fire"?left:trigger=="ads"&&right;}
 public static Candidate Choose(List<Candidate> all,double radius,long previous){
  if(!V3.Good(radius)||radius<2||radius>90)return null;
  bool full=FullViewport(radius);
  var valid=all.Where(c=>V3.Good(c.Angle)&&c.Angle>=0&&c.Angle<90);
  var current=valid.FirstOrDefault(c=>c.Pointer==previous&&(full||c.Angle<=radius*1.12));
  return current??valid.Where(c=>full||c.Angle<=radius).OrderBy(c=>c.Angle).FirstOrDefault();
 }
 public static int Step(double error,double gain){if(!V3.Good(error)||!V3.Good(gain)||gain<=0)return 0;if(Math.Abs(error)<1.5)return 0;return (int)Math.Round(Math.Max(-12,Math.Min(12,error*gain)));}
}
public sealed class AimReader:IDisposable {
 public Mem M;public AimReader(Process p){M=new Mem(p);}
 bool ThirdCamera(long soldier,V3 position){try{long cam=M.Q(soldier+0x1f8);if(M.Q(cam)!=M.Base+0x1bbf5d0||M.Q(cam+0x138)!=soldier)return false;long begin=M.Q(cam+0x118);if(M.Q(cam+0x120)-begin!=24)return false;long chase=M.Q(begin+16);return M.Q(chase)==M.Base+0x1d213e0&&position.Distance(V3.From(M.V(chase+0x40,3)))<0.5;}catch{return false;}}
 static bool Finite(float[] v){return v.All(x=>V3.Good(x));}
 public string ReadOneHitIdentity(Roster roster,bool allowVehicle=false){
  long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  if(roster==null||!roster.Fresh(now))return "";
  long ctx=M.Q(M.Base+0x2670d80);if(!Mem.Ptr(ctx))return "";long pm=M.Q(ctx+0x60);if(!Mem.Ptr(pm))return "";
  long local=M.Q(pm+0x540);if(!Mem.Ptr(local))return "";string name=M.S(local+0x40,64);
  if(String.IsNullOrEmpty(name)||!roster.players.Any(x=>x.name==name&&!String.IsNullOrEmpty(x.guid)))return "";
  int team=M.I(local+0x13cc);if(team<1||team>2||M.B(local+0x13c9)!=0||(!allowVehicle&&M.Q(local+0x14c0)!=0))return "";
  if(M.Q(M.Base+0x2670d80)!=ctx||M.Q(pm+0x540)!=local)return "";
  return name;
 }
 public Scene Read(bool head,int width,int height,Roster roster,bool esp=false,int espDistance=100,bool excludeHumans=true){
  var sw=Stopwatch.StartNew();var s=new Scene{Status="等待本机士兵部署"};
  long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  if(roster==null||!roster.Fresh(now)){s.Status="等待当前服务器确认";return s;}
  var bots=roster.players.Where(x=>!excludeHumans||string.IsNullOrEmpty(x.guid)).ToDictionary(x=>x.name,x=>x);
  long ctx=M.Q(M.Base+0x2670d80);if(!Mem.Ptr(ctx))return s;long pm=M.Q(ctx+0x60);if(!Mem.Ptr(pm))return s;
  long local=M.Q(pm+0x540);if(!Mem.Ptr(local))return s;string name=M.S(local+0x40,64);s.Name=name;
  if(!roster.players.Any(x=>x.name==name&&!string.IsNullOrEmpty(x.guid))){s.Status="当前角色未在此服务器内";return s;}
  if(M.B(local+0x13c9)!=0){s.Status="观战中，已暂停";return s;}
  long vehicle=M.Q(local+0x14c0);bool inVehicle=Mem.Ptr(vehicle);int seat=inVehicle?M.I(local+0x14c8):-1;if(inVehicle&&(seat<0||seat>32||M.Q(vehicle)<M.Base||M.Q(vehicle)>M.Base+0x4000000))return s;
  int team=M.I(local+0x13cc);s.Team=team;if(team<1||team>2)return s;
  long own=M.Q(local+0x14d0);V3 pos=new V3();if(!inVehicle){if(!Mem.Ptr(own))return s;long health=M.Q(own+0x140),pred=M.Q(own+0x490);if(!Mem.Ptr(health)||!Mem.Ptr(pred))return s;float hp=M.F(health+0x20);if(hp<=0||hp>5000)return s;pos=V3.From(M.V(pred+0x30,3));if(!pos.Valid)return s;}
  long renderer=M.Q(M.Base+0x2672378);if(!Mem.Ptr(renderer))return s;long view=M.Q(renderer+0x60);if(!Mem.Ptr(view))return s;
  var inverse=M.V(view+0x2e0,16);var projection=M.V(view+0x320,16);var vp=M.V(view+0x420,16);
  if(!Finite(inverse)||!Finite(projection)||!Finite(vp))return s;
  V3 camera=new V3(inverse[12],inverse[13],inverse[14]);
  if(!camera.Valid||(!inVehicle&&camera.Distance(pos)>2.5&&(!esp||camera.Distance(pos)>15||!ThirdCamera(own,camera)))||projection[0]<0.1||projection[0]>30||projection[5]<0.1||projection[5]>30||Math.Abs(inverse[15]-1)>0.01){s.Status="镜头不在士兵视角";return s;}
  s.Forward=new V3(-inverse[8],-inverse[9],-inverse[10]);s.Right=new V3(inverse[0],inverse[1],inverse[2]);s.Up=new V3(inverse[4],inverse[5],inverse[6]);s.FocalX=projection[0]*width/2;s.FocalY=projection[5]*height/2;
  s.FovX=2*Math.Atan(1/projection[0])*180/Math.PI;s.FovY=2*Math.Atan(1/projection[5])*180/Math.PI;
  long arr=M.Q(pm+0x548);if(!Mem.Ptr(arr))return s;byte[] pointers=M.R(arr,70*8);
  for(int i=0;i<70;i++){long player=BitConverter.ToInt64(pointers,i*8);if(!Mem.Ptr(player)||player==local)continue;
   try{string n=M.S(player+0x40,64);RosterPlayer bot;if(!bots.TryGetValue(n,out bot))continue;
    int t=M.I(player+0x13cc);if(bot.teamId!=t.ToString())continue;if(string.IsNullOrEmpty(bot.guid))s.MatchedBots++;if(t<1||t>2||t==team)continue;s.Enemies++;
    if(M.B(player+0x13c9)!=0||M.Q(player+0x14c0)!=0)continue;long soldier=M.Q(player+0x14d0);if(!Mem.Ptr(soldier))continue;
    var block=M.R(soldier+0x490,0x122);bool occluded=block[0x121]!=0;
    long h=M.Q(soldier+0x140);if(!Mem.Ptr(h))continue;float targetHp=M.F(h+0x20);if(targetHp<=0||targetHp>5000)continue;
    long prediction=BitConverter.ToInt64(block,0),rag=BitConverter.ToInt64(block,0xf0);if(!Mem.Ptr(prediction)||!Mem.Ptr(rag))continue;
    V3 feet=V3.From(M.V(prediction+0x30,3));long bones=M.Q(rag+0xb0);if(!Mem.Ptr(bones)||!feet.Valid)continue;
    V3 aim=V3.From(M.V(bones+(head?104:7)*32,3));if(!aim.Valid||aim.Distance(feet)>2.5||aim.Distance(feet)<0.1)continue;
    double distance=aim.Distance(camera);if(distance<0.5)continue;
    if(esp&&(espDistance==0||distance<=espDistance)){
     V3 top=V3.From(M.V(bones+104*32,3));if(!top.Valid||top.Distance(feet)>2.5||top.Distance(feet)<0.1)top=new V3(feet.X,feet.Y+1.7,feet.Z);top.Y+=0.15;
     double tx,ty,ta,fx,fy,fa;
     if(AimMath.Project(top,vp,projection,out tx,out ty,out ta)&&AimMath.Project(feet,vp,projection,out fx,out fy,out fa)){
      double bottom=(1-fy)*height/2,upper=(1-ty)*height/2,hh=bottom-upper,xx=(tx+fx+2)*width/4;
      if(espDistance==0&&hh>0&&hh<3){upper-=3-hh;hh=3;}
      if(hh>=3&&hh<height*1.5)s.Markers.Add(new EspMarker{Left=xx-hh*0.25,Top=upper,Right=xx+hh*0.25,Bottom=bottom,Distance=distance,Occluded=occluded});
     }
    }
    if(occluded||distance>150)continue;s.Visible++;
    double nx,ny,angle;if(!AimMath.Project(aim,vp,projection,out nx,out ny,out angle))continue;
    if(M.Q(player+0x14d0)!=soldier||M.B(soldier+0x5b1)!=0)continue;
    s.Targets.Add(new Candidate{Pointer=soldier,Name=n,X=nx*width/2,Y=-ny*height/2,Angle=angle,Distance=distance});
   }catch{}}
  if(M.Q(M.Base+0x2670d80)!=ctx||M.Q(pm+0x540)!=local||M.Q(local+0x14d0)!=own||M.Q(local+0x14c0)!=vehicle||(inVehicle&&M.I(local+0x14c8)!=seat)){s.Targets.Clear();s.Markers.Clear();return s;}
  s.Context=ctx;s.LocalSoldier=own;s.LocalVehicle=vehicle;s.VehicleSeat=seat;s.ReadMs=sw.Elapsed.TotalMilliseconds;
  s.Ready=true;if(!s.Ready){s.Targets.Clear();s.Markers.Clear();s.Status="核对本机 Bot 名单中";}else s.Status=inVehicle?"载具内瞄准与透视已就绪":"当前服务器已确认";return s;
 }
 public void Dispose(){M.Dispose();}
}

public sealed class MouseScale {
 readonly List<double> samples=new List<double>();
 public double Value{get;private set;}public int Count{get{return samples.Count;}}
 public void Clear(){samples.Clear();Value=0;}
 public void Add(int input,double radians){
  if(Math.Abs(input)<3||!V3.Good(radians)||Math.Abs(radians)<0.0003||Math.Abs(radians)>0.6||Math.Sign(input)!=Math.Sign(radians))return;
  double ratio=input/radians;if(ratio<20||ratio>50000)return;
  samples.Add(ratio);if(samples.Count>7)samples.RemoveAt(0);
  if(samples.Count<3)return;
  var recent=samples.Skip(Math.Max(0,samples.Count-5)).OrderBy(x=>x).ToArray();double median=recent[recent.Length/2];
  int consistent=recent.Count(x=>Math.Abs(x/median-1)<0.25);if(consistent<3)return;
  Value=median;
 }
}
public sealed class AimDrive {
 public readonly MouseScale X=new MouseScale(),Y=new MouseScale();
 Scene sentScene;int sentX,sentY;long sentAt,lastStep;double fov;
 public bool Calibrated{get{return X.Value>0||Y.Value>0;}}
 public void Reset(){X.Clear();Y.Clear();sentScene=null;lastStep=0;fov=0;}
 public void CancelPending(){sentScene=null;lastStep=0;}
 static double Dot(V3 a,V3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
 public void Observe(Scene scene,long now,bool firing){
  if(fov>0&&Math.Abs(scene.FovX-fov)>0.35){X.Clear();Y.Clear();sentScene=null;}fov=scene.FovX;
  if(sentScene!=null&&now-sentAt>=15&&now-sentAt<=120&&scene.Context==sentScene.Context&&scene.LocalSoldier==sentScene.LocalSoldier&&scene.LocalVehicle==sentScene.LocalVehicle&&scene.VehicleSeat==sentScene.VehicleSeat){
   double forward=Dot(scene.Forward,sentScene.Forward),right=Dot(scene.Forward,sentScene.Right),up=Dot(scene.Forward,sentScene.Up);
   if(!firing||Math.Abs(sentX)>=8)X.Add(sentX,Math.Atan2(right,forward));if(!firing)Y.Add(sentY,Math.Atan2(-up,Math.Sqrt(forward*forward+right*right)));
  }
  sentScene=null;
 }
 public static double Duration(int speed){return speed>=100?0:15+600*Math.Pow(1-(Math.Max(5,Math.Min(99,speed))-5)/94.0,2);}
 public static double Blend(int speed,double elapsedMs){if(speed>=100)return 1;return 1-Math.Exp(-3*Math.Max(1,Math.Min(100,elapsedMs))/Duration(speed));}
 public static int Counts(double angle,double scale,double blend){if(!V3.Good(angle)||!V3.Good(scale)||!V3.Good(blend)||scale<=0)return 0;return (int)Math.Round(Math.Max(-4096,Math.Min(4096,angle*scale*blend)));}
 public void Step(Candidate target,Scene scene,int speed,long now,out int dx,out int dy){
  double dt=lastStep==0?25:now-lastStep;lastStep=now;
  if(!Calibrated){dx=AimMath.Step(target.X,Math.Max(0.08,speed/200.0));dy=AimMath.Step(target.Y,Math.Max(0.08,speed/200.0));return;}
  double sx=X.Value>0?X.Value:Y.Value,sy=Y.Value>0?Y.Value:X.Value,blend=Blend(speed,dt);
  double right=target.X/scene.FocalX,down=target.Y/scene.FocalY;
  dx=Math.Abs(target.X)<1.5?0:Counts(Math.Atan(right),sx,blend);
  dy=Math.Abs(target.Y)<1.5?0:Counts(Math.Atan2(down,Math.Sqrt(1+right*right)),sy,blend);
 }
 public void Sent(Scene scene,long now,int dx,int dy){sentScene=scene;sentAt=now;sentX=dx;sentY=dy;}
}
