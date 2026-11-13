#include "PortableScope.h"
extern "C" {int _fltused=0;}
extern "C" void* __cdecl memset(void*,int,size_t);
#pragma function(memset)
extern "C" void* __cdecl memset(void* d,int c,size_t n){auto p=(volatile unsigned char*)d;for(size_t i=0;i<n;i++)p[i]=(unsigned char)c;return d;}
API void WINAPI Sleep(DWORD);API unsigned __int64 WINAPI GetTickCount64();
API void WINAPI GetSystemTimeAsFileTime(void*);API BOOL WINAPI MoveFileExW(const wchar_t*,const wchar_t*,DWORD);
API DWORD WINAPI GetPrivateProfileStringW(const wchar_t*,const wchar_t*,const wchar_t*,wchar_t*,DWORD,const wchar_t*);
using TickFn=uintptr_t(__fastcall*)(uintptr_t,uintptr_t);
static uintptr_t game;static TickFn beforeTick;static volatile LONG pending=0,finished=0;static bool installed=false,limit=true;
static wchar_t command[MAX_PATH],report[MAX_PATH],temp[MAX_PATH];static unsigned __int64 request=0,expires=0,serial=0,lastSpawn=0;static int action=0,result=0;static char player[128]{},model[256]{};
struct Choice{uintptr_t spawner,data,blueprint;char name[256];};static Choice choices[256];static unsigned choiceCount=0;
static uintptr_t directBlueprints[2]{};static const char* directNames[2]={"Gameplay/Vehicles/QuadBike/QUADBIKE","Gameplay/Vehicles/PWC_JetSki/PWC_JetSki"};
struct Vehicle{uintptr_t entity,world,data,spawner;unsigned __int64 serial;unsigned index;bool retiring;};static Vehicle owned[4096];static unsigned count=0;static uintptr_t created=0;static unsigned removed=0;static bool requestedLimit=true;
struct Slots{uintptr_t spawner,data,world;unsigned reserved;};static Slots slots[256];static unsigned slotCount=0;
static bool Readable(uintptr_t a,unsigned n){if(a<0x10000||a>0x7fffffffffffULL-n)return false;MEMORY_BASIC_INFORMATION m{};return VirtualQuery((void*)a,&m,sizeof(m))&&m.State==MEM_COMMIT&&!(m.Protect&(PAGE_GUARD|PAGE_NOACCESS))&&a+n<=(uintptr_t)m.BaseAddress+m.RegionSize;}
static uintptr_t Q(uintptr_t p){return Readable(p,8)?*(uintptr_t*)p:0;}
static bool Eq(const char* a,const char* b){for(unsigned i=0;i<256;i++){if(!Readable((uintptr_t)a+i,1)||a[i]!=b[i])return false;if(!a[i])return true;}return false;}
static void Copy(char* a,const char* b,unsigned cap){unsigned i=0;for(;i+1<cap&&Readable((uintptr_t)b+i,1)&&b[i];i++)a[i]=b[i];a[i]=0;}
static unsigned __int64 Now(){unsigned __int64 t=0;GetSystemTimeAsFileTime(&t);return(t-116444736000000000ULL)/10000;}
static bool Swap(uintptr_t prev,uintptr_t next){auto p=(volatile LONG64*)(game+0x1ff0aa8);DWORD old,tmp;if(!VirtualProtect((void*)p,8,4,&old))return false;bool ok=(uintptr_t)InterlockedCompareExchange64(p,next,prev)==prev;VirtualProtect((void*)p,8,old,&tmp);return ok;}
static void ScanLoadedResources(){
 directBlueprints[0]=directBlueprints[1]=0;uintptr_t address=0;unsigned found=0;
 while(address<0x7fffffffffffULL&&found!=3){MEMORY_BASIC_INFORMATION m{};if(!VirtualQuery((void*)address,&m,sizeof(m)))break;auto end=(uintptr_t)m.BaseAddress+m.RegionSize;if(end<=address)break;auto protect=m.Protect&255;
 if(m.State==MEM_COMMIT&&m.Type==0x20000&&!(m.Protect&PAGE_GUARD)&&(protect==2||protect==4||protect==8||protect==0x20||protect==0x40||protect==0x80)){
 __try{for(auto p=((uintptr_t)m.BaseAddress+7)&~7ULL;p+0x48<=end;p+=8){if(*(uintptr_t*)p!=game+0x2011040)continue;auto name=(char*)Q(p+0x10);for(unsigned j=0;j<2;j++)if(!(found&(1<<j))&&Eq(name,directNames[j])&&Readable(Q(p+0x40),0x20)){directBlueprints[j]=p;found|=1<<j;}}}__except(1){}
 }address=end;}
}
static uintptr_t FindHuman(){auto pm=Q(Q(game+0x2c68148)+0x60);if(!Readable(pm,0x930))return 0;auto n=*(unsigned*)(pm+0x10);auto pp=Q(pm+0x928);if(n>128||!Readable(pp,n*8))return 0;for(unsigned i=0;i<n;i++){auto p=Q(pp+i*8);if(!Readable(p,0x14d0)||!Eq((char*)(p+0x40),player))continue;auto inp=Q(p+0x14a0);if(Q(inp-16)==p)return 0;auto a=Q(p+0x14a8);if(!Readable(a,0x600)||Q(a+0x268)!=p)return 0;auto hp=Q(a+0x1f0);if(!Readable(hp,0x28)||!(*(float*)(hp+0x20)>0))return 0;
 // A controllable other than the soldier means the player is in a vehicle.
 auto controlled=Q(p+0x14c8);if(controlled&&controlled!=a)return 0;return a;}return 0;}
static void Enumerate(uintptr_t manager){choiceCount=0;auto begin=Q(manager+0x28),end=Q(manager+0x30);if(end<begin||end-begin>8192||!Readable(begin,(unsigned)(end-begin)))return;
 for(auto p=begin;p<end&&choiceCount<256;p+=8){auto s=Q(p);if(!Readable(s,0x280)||Q(s)!=game+0x1fef880||!*(unsigned char*)(s+0x24c))continue;auto d=Q(s+0x30);if(!Readable(d,0x1c0))continue;uintptr_t level=0,world=0;
 auto bp=((uintptr_t(__fastcall*)(uintptr_t,uintptr_t*,uintptr_t*))(game+0xc40390))(s,&level,&world);if(!bp)bp=Q(d+0x18);
 if(!Readable(bp,0x48)||Q(bp)!=game+0x2011040)continue;auto name=(char*)Q(bp+0x10);if(!Readable((uintptr_t)name,128))continue;
 auto& c=choices[choiceCount++];c.spawner=s;c.data=d;c.blueprint=bp;Copy(c.name,name,256);
 }
 for(unsigned j=0;j<2;j++){auto bp=directBlueprints[j];if(Q(bp)!=game+0x2011040||!Eq((char*)Q(bp+0x10),directNames[j]))continue;bool present=false;for(unsigned k=0;k<choiceCount;k++)if(Eq(choices[k].name,directNames[j])){present=true;break;}if(!present&&choiceCount<256){auto& c=choices[choiceCount++];c.spawner=0;c.blueprint=bp;c.data=Q(bp+0x40);Copy(c.name,directNames[j],256);}}
}
static bool Live(const Vehicle& v){if(!v.spawner){bool loaded=false;for(unsigned j=0;j<choiceCount;j++)if(!choices[j].spawner&&choices[j].data==v.data){loaded=true;break;}return loaded&&Readable(v.entity,0x490)&&Q(v.entity)==game+0x1fe0b70&&Q(v.entity+0x28)==v.world&&Q(v.entity+0x30)==v.data&&!Q(v.entity+0x1e0); }bool sp=false;for(unsigned j=0;j<choiceCount;j++)if(choices[j].spawner==v.spawner){sp=true;break;}if(!sp||!Readable(v.entity,0x490)||Q(v.entity+0x28)!=v.world||Q(v.entity+0x30)!=v.data||Q(v.entity+0x1e0)!=v.spawner)return false;
 auto b=Q(v.spawner+0x148),e=Q(v.spawner+0x150);if(e<b||e-b>65536||!Readable(b,(unsigned)(e-b)))return false;for(auto p=b;p<e;p+=8)if(Q(p)==v.entity)return true;return false;}
static void Prune(){unsigned n=0;for(unsigned i=0;i<count;i++)if(Live(owned[i]))owned[n++]=owned[i];count=n;}
static bool RetireFirst(){if(!count)return true;auto& v=owned[0];if(!Live(v))return false;if(!v.retiring){((void(__fastcall*)(uintptr_t,uintptr_t))(game+0x936a90))(v.entity,v.world);v.retiring=true;removed++;}return false;}
static uintptr_t SpawnDirect(uintptr_t actor,Choice& c){
 if(!Eq(c.name,directNames[0])&&!Eq(c.name,directNames[1]))return 0;
 if(Q(c.blueprint)!=game+0x2011040||Q(c.blueprint+0x40)!=c.data)return 0;
 __declspec(align(16)) unsigned char desc[0x120]{};__declspec(align(16)) float t[16]{};uintptr_t out[2]{};
 ((void(__fastcall*)(uintptr_t,float*))(game+0xbcd700))(actor,t);float fx=t[8],fz=t[10],len=fx*fx+fz*fz;if(!(len>0.5f&&len<1.5f))return 0;
 t[12]+=fx*8.f;t[13]+=3.f;t[14]+=fz*8.f;t[15]=1.f;for(unsigned i=0;i<12;i++)t[i]=i==0||i==5||i==10?1.f:0.f;auto world=Q(actor+0x28);if(!Readable(world,0x90))return 0;
 ((uintptr_t(__fastcall*)(void*,uintptr_t,uintptr_t,float*))(game+0xc216f0))(desc,0,world,t);*(unsigned*)(desc+0x28)=2;
 ((uintptr_t(__fastcall*)(uintptr_t*,void*,uintptr_t,uintptr_t,uintptr_t))(game+0xc269e0))(out,desc,c.blueprint,0,0);auto entity=out[0];
 if(entity&&Readable(entity,0x200))((void(__fastcall*)(uintptr_t))(game+0xbd3780))(entity);if(out[1])((void(__fastcall*)(uintptr_t))(game+0x96fdf0))(out[1]);
 if(!Readable(entity,0x490)||Q(entity)!=game+0x1fe0b70)return 0;
 __declspec(align(16)) unsigned char spawnDesc[0x40]{};((uintptr_t(__fastcall*)(void*,uintptr_t))(game+0xc21780))(spawnDesc,0);
 ((void(__fastcall*)(uintptr_t,void*))(game+0x935e90))(entity,spawnDesc);((void(__fastcall*)(uintptr_t))(game+0xbd3780))(entity);
 owned[count++]={entity,Q(entity+0x28),Q(entity+0x30),0,++serial,0,false};return entity;
}
static uintptr_t Spawn(uintptr_t actor,Choice& c){if(!c.spawner)return SpawnDirect(actor,c);auto s=c.spawner,d=c.data;
 unsigned oldCount=*(unsigned*)(s+0xfc);auto first=Q(s+0x148),end=Q(s+0x150);if(end<first||end-first>65536)return 0;
 Slots* slot=nullptr;for(unsigned j=0;j<slotCount;j++)if(slots[j].spawner==s&&slots[j].data==d&&slots[j].world==Q(s+0x28)){slot=&slots[j];break;}
 if(!slot){if(slotCount>=256)return 0;slot=&slots[slotCount++];*slot={s,d,Q(s+0x28),oldCount};}
 bool used[128]{};for(unsigned j=0;j<slot->reserved&&j<128;j++)used[j]=true;
 for(unsigned j=0;j<count;j++)if(owned[j].spawner==s&&owned[j].index<128)used[owned[j].index]=true;
 unsigned index=0;while(index<128&&used[index])index++;if(index==128)return 0;
 __declspec(align(16)) unsigned char params[0x70]{};auto t=(float*)params;
 ((void(__fastcall*)(uintptr_t,float*))(game+0xbcd700))(actor,t);float fx=t[8],fz=t[10],len=fx*fx+fz*fz;if(!(len>0.5f&&len<1.5f))return 0;
 t[12]+=fx*15.f;t[13]+=4.f;t[14]+=fz*15.f;t[15]=1.f;for(unsigned i=0;i<12;i++)t[i]=i==0||i==5||i==10?1.f:0.f;
 *(int*)(params+0x50)=-1;*(unsigned*)(params+0x54)=index;params[0x59]=1;
 MEMORY_BASIC_INFORMATION m{};if(!VirtualQuery((void*)(d+0x15b),&m,sizeof(m))||((m.Protect&255)!=4&&(m.Protect&255)!=0x40))return 0;
 auto replace=*(unsigned char*)(d+0x15b);float cooldown=*(float*)(s+0xf8);uintptr_t entity=0;
 __try{*(unsigned char*)(d+0x15b)=0;*(unsigned*)(s+0xfc)=0;*(float*)(s+0xf8)=0;entity=((uintptr_t(__fastcall*)(uintptr_t,void*))(game+0xc4b340))(s,params);}
 __finally{*(unsigned char*)(d+0x15b)=replace;*(unsigned*)(s+0xfc)=oldCount+(entity?1:0);if(!entity)*(float*)(s+0xf8)=cooldown;}
 if(entity&&Readable(entity,0x490)){owned[count++]={entity,Q(entity+0x28),Q(entity+0x30),s,++serial,index,false};return entity;}return 0;
}
static uintptr_t __fastcall Tick(uintptr_t manager,uintptr_t ctx){auto r=beforeTick(manager,ctx);if(!pending)return r;
 __try{if(Now()>expires){result=-2;pending=0;finished=1;return r;}Enumerate(manager);Prune();
  if(action==2){limit=requestedLimit;if(limit&&count>20){RetireFirst();return r;}}
  if(action==3){if(count){RetireFirst();return r;}}
  if(action==1){auto actor=FindHuman();if(!actor){result=-3;pending=0;finished=1;return r;}Choice* selected=nullptr;for(unsigned i=0;i<choiceCount;i++)if(Eq(choices[i].name,model)){selected=&choices[i];break;}
   if(!selected){result=-4;pending=0;finished=1;return r;}if(count>=4096){result=-5;pending=0;finished=1;return r;}
   if(limit&&count>=20){RetireFirst();return r;}if(GetTickCount64()-lastSpawn<2000){result=-6;pending=0;finished=1;return r;}
   created=Spawn(actor,*selected);if(!created)result=-7;else {result=1;lastSpawn=GetTickCount64();}}
  pending=0;finished=1;
 }__except(1){result=-9;pending=0;finished=1;}
 return r;
}
static unsigned __int64 Num(const wchar_t* key){wchar_t b[64];GetPrivateProfileStringW(L"Vehicle",key,L"0",b,64,command);unsigned __int64 x=0;for(unsigned i=0;b[i];i++){if(i>18||b[i]<'0'||b[i]>'9')return 0;x=x*10+b[i]-'0';}return x;}
static int Hex(wchar_t c){return c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:-1;}
static bool HexText(const wchar_t* key,char* out,unsigned cap){wchar_t b[1024]{};GetPrivateProfileStringW(L"Vehicle",key,L"",b,1024,command);unsigned i=0;for(;b[i*2]&&i+1<cap;i++){int a=Hex(b[i*2]),c=Hex(b[i*2+1]);if(a<0||c<0)return false;out[i]=(char)((a<<4)|c);if(!out[i])return false;}out[i]=0;return !b[i*2];}
static char json[262144];static unsigned pos=0;
static void Add(const char* s){for(unsigned i=0;s[i]&&pos+1<sizeof(json);i++)json[pos++]=s[i];json[pos]=0;}
static void N(unsigned __int64 n){char b[32];unsigned l=0;do{b[l++]=(char)('0'+n%10);n/=10;}while(n);while(l&&pos+1<sizeof(json))json[pos++]=b[--l];json[pos]=0;}
static void Str(const char* s){Add("\"");for(unsigned i=0;s[i]&&i<256;i++){unsigned char c=s[i];if(c=='"'||c=='\\'){Add("\\");char z[2]={(char)c,0};Add(z);}else if(c>=32){char z[2]={(char)c,0};Add(z);}}Add("\"");}
static void Write(){pos=0;Add("{\"version\":1,\"pid\":");N(GetCurrentProcessId());Add(",\"at\":");N(Now());Add(",\"requestId\":\"");N(request);Add("\",\"installed\":");Add(installed?"true":"false");Add(",\"limitEnabled\":");Add(limit?"true":"false");Add(",\"count\":");N(count);Add(",\"result\":");if(result<0)Add("-");N(result<0?-result:result);Add(",\"created\":\"");N(created);Add("\",\"removed\":");N(removed);Add(",\"catalog\":[");for(unsigned i=0;i<choiceCount;i++){if(i)Add(",");Str(choices[i].name);}Add("],\"vehicles\":[");for(unsigned i=0;i<count;i++){if(i)Add(",");Add("{\"id\":");N(owned[i].serial);Add(",\"entity\":\"");N(owned[i].entity);Add("\",\"retiring\":");Add(owned[i].retiring?"true":"false");Add("}");}Add("]}");
 auto f=CreateFileW(temp,0x40000000,3,nullptr,2,0x80,nullptr);if(f!=INVALID_HANDLE_VALUE){DWORD written=0;WriteFile(f,json,pos,&written,nullptr);CloseHandle(f);if(written==pos)MoveFileExW(temp,report,9);}}
static DWORD WINAPI Start(void* module){wchar_t exe[MAX_PATH];GetModuleFileNameW(nullptr,exe,MAX_PATH);if(!HostExecutable(exe,L"bf4_Server_Final.exe"))return 1;game=(uintptr_t)GetModuleHandleW(nullptr);if(game!=0x140000000)return 2;
 if(memcmp((void*)(game+0xc4b340),"\x48\x89\x5c\x24\x08\x57\x48\x83\xec\x50",10))return 3;
 wchar_t dir[MAX_PATH];GetModuleFileNameW((HINSTANCE)module,dir,MAX_PATH);auto slash=wcsrchr(dir,L'\\');if(!slash)return 4;*slash=0;wsprintfW(command,L"%s\\vehicle-command-v2.ini",dir);wsprintfW(report,L"%s\\vehicle-state-v2.json",dir);wsprintfW(temp,L"%s\\vehicle-state-v2.tmp",dir);
 request=Num(L"Sequence");beforeTick=*(TickFn*)(game+0x1ff0aa8);if((uintptr_t)beforeTick!=game+0xc47b20){MEMORY_BASIC_INFORMATION old{};wchar_t oldPath[MAX_PATH]{};if(!VirtualQuery((void*)beforeTick,&old,sizeof(old))||!GetModuleFileNameW((HINSTANCE)old.AllocationBase,oldPath,MAX_PATH)||!SameModuleDirectory(oldPath,command)||!HostExecutable(oldPath,L"BF4VehicleSpawner.dll"))return 5;}installed=Swap((uintptr_t)beforeTick,(uintptr_t)&Tick);if(!installed)return 6;Write();
 for(;;){if(!pending&&!finished){auto id=Num(L"Sequence");if(id&&id!=request){request=id;expires=Num(L"Expires");action=(int)Num(L"Action");requestedLimit=Num(L"Limit")!=0;created=0;removed=0;result=0;if(action<0||action>3||expires<Now()||expires-Now()>30000||!HexText(L"PlayerHex",player,128)||!HexText(L"ModelHex",model,256)){result=-1;Write();}else {if(action==0)ScanLoadedResources();pending=1;}}}
 if(finished){Write();finished=0;}Sleep(100);}
}
extern "C" BOOL WINAPI DllMain(HINSTANCE m,DWORD r,void*){if(r==1){DisableThreadLibraryCalls(m);auto t=CreateThread(nullptr,0,Start,m,0,nullptr);if(t)CloseHandle(t);}return TRUE;}
