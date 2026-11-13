#include "WinApi.h"
#include "Paths.h"
extern "C" {int _fltused=0;}
extern "C" void* __cdecl memset(void*,int,size_t);
#pragma function(memset)
extern "C" void* __cdecl memset(void* d,int c,size_t n){auto p=(volatile unsigned char*)d;for(size_t i=0;i<n;i++)p[i]=(unsigned char)c;return d;}
API void WINAPI Sleep(DWORD);
API unsigned __int64 WINAPI GetTickCount64();
API DWORD WINAPI GetPrivateProfileStringW(const wchar_t*,const wchar_t*,const wchar_t*,wchar_t*,DWORD,const wchar_t*);
API BOOL WINAPI TryAcquireSRWLockExclusive(SRWLOCK*);

using Suicide=bool(__fastcall*)(uintptr_t,bool,bool);
static Suicide originalSuicide;
using Apply=uintptr_t(__fastcall*)(uintptr_t,uintptr_t,uintptr_t);
using SetHealth=void(__fastcall*)(uintptr_t,float);
using GetHealth=float(__fastcall*)(uintptr_t);
using Query=bool(__fastcall*)(uintptr_t);
using Update=void(__fastcall*)(uintptr_t,uintptr_t);
static Apply originalDamage;static SetHealth originalSet;static Query originalImmortal;static Update originalUpdate;
static GetHealth maximumHealth;
static uintptr_t game,protectedHealth=0,protectedOwner=0;
static uintptr_t retiringHealth=0,retiringOwner=0;static bool observedDead=false;static unsigned __int64 retireAt=0;static volatile LONG redeploys=0;
static SRWLOCK configLock=SRWLOCK_INIT;
static bool on=false,installed=false;static unsigned __int64 lease=0;static char playerName[64];
static volatile LONG blocked=0,clamped=0,refilled=0,updates=0;
static volatile LONG lastHealth=0,lastMaximum=0;


static void Write(const wchar_t* path,bool append,const char* format,...){char line[1024];va_list a;__va_start(&a,format);int n=wvsprintfA(line,format,a);if(n<0||n>1020)return;line[n++]='\r';line[n++]='\n';HANDLE h=CreateFileW(path,append?FILE_APPEND_DATA:0x40000000,FILE_SHARE_READ|FILE_SHARE_WRITE,nullptr,append?OPEN_ALWAYS:2,FILE_ATTRIBUTE_NORMAL,nullptr);if(h==INVALID_HANDLE_VALUE)return;DWORD done;WriteFile(h,line,n,&done,nullptr);CloseHandle(h);}

static bool Active(){auto now=GetTickCount64();return on&&lease<=now&&now-lease<4000&&playerName[0];}
static bool Protected(uintptr_t health,float* maximum){
 __try {
  if(*(uintptr_t*)health!=game+0x1d82180)return false;
  auto owner=**(uintptr_t**)(health+8);auto player=*(uintptr_t*)(owner+0x268);
  if(!player||*(uintptr_t*)(player+0x14a8)!=owner||*(uintptr_t*)(owner+0x1f0)!=health)return false;
  auto context=*(uintptr_t*)(game+0x2c68148);auto manager=*(uintptr_t*)(context+0x60);
  unsigned count=*(unsigned*)(manager+0x10);if(!count||count>70)return false;
  auto players=*(uintptr_t**)(manager+0x928);bool found=false;for(unsigned i=0;i<count;i++)if(players[i]==player){found=true;break;}if(!found)return false;
  auto input=*(uintptr_t*)(player+0x14a0);if(input>=0x10010&&*(uintptr_t*)(input-16)==player)return false;
  unsigned n=0;for(;n<64;n++){char c=*(char*)(player+0x40+n);if(c!=playerName[n])return false;if(!c)break;}if(n==64)return false;
  if(*(unsigned char*)(player+0x13c9))return false;
  float max=maximumHealth(health);if(!(max>0&&max<=100000))return false;
  float hp=*(float*)(health+0x20);
  if(retiringHealth==health&&retiringOwner==owner){
   if(hp<=0){observedDead=true;return false;}
   if(!observedDead&&GetTickCount64()-retireAt<5000)return false;
   retiringHealth=retiringOwner=0;observedDead=false;protectedHealth=protectedOwner=0;
  }else if(retiringHealth){retiringHealth=retiringOwner=0;observedDead=false;}
  // An existing corpse is not revived by enabling the switch.
  if(health!=protectedHealth||owner!=protectedOwner){if(!(hp>0&&hp<=100000))return false;protectedHealth=health;protectedOwner=owner;}
  *maximum=max;return true;
 }__except(1){return false;}
}
static bool Check(uintptr_t health,float* maximum){AcquireSRWLockExclusive(&configLock);bool yes=Active()&&Protected(health,maximum);if(!Active()){protectedHealth=0;protectedOwner=0;}ReleaseSRWLockExclusive(&configLock);return yes;}
static void Fill(uintptr_t health,float maximum){
 if(*(float*)(health+0x20)!=maximum){originalSet(health,maximum);InterlockedIncrement(&refilled);}
 lastHealth=(LONG)(*(float*)(health+0x20)*1000);lastMaximum=(LONG)(maximum*1000);
}
static uintptr_t __fastcall Damage(uintptr_t health,uintptr_t result,uintptr_t info){
 float max;if(!Check(health,&max))return originalDamage(health,result,info);
 Fill(health,max);InterlockedIncrement(&blocked);
 // Same empty result as the game's rejected-damage return path: empty vector + zero damage.
 memset((void*)result,0,0x24);return result;
}
static void __fastcall Set(uintptr_t health,float value){float max;if(Check(health,&max)){if(value!=max)InterlockedIncrement(&clamped);value=max;}originalSet(health,value);}
static bool __fastcall Immortal(uintptr_t health){float max;return Check(health,&max)||originalImmortal(health);}
static void __fastcall Tick(uintptr_t health,uintptr_t context){
 float max;bool yes=Check(health,&max);if(yes){Fill(health,max);InterlockedIncrement(&updates);}originalUpdate(health,context);if(yes&&Check(health,&max))Fill(health,max);
}
static bool __fastcall Redeploy(uintptr_t actor,bool force,bool spamGuard){
 uintptr_t h=0;bool allow=false;
 __try {if(!force&&spamGuard&&*(uintptr_t*)actor==game+0x2105df0){h=*(uintptr_t*)(actor+0x1f0);float max;allow=Check(h,&max);}}__except(1){}
 if(!allow)return originalSuicide(actor,force,spamGuard);
 AcquireSRWLockExclusive(&configLock);retiringHealth=h;retiringOwner=actor;observedDead=false;retireAt=GetTickCount64();protectedHealth=protectedOwner=0;ReleaseSRWLockExclusive(&configLock);
 bool accepted=originalSuicide(actor,force,spamGuard);
 AcquireSRWLockExclusive(&configLock);
 if(accepted){InterlockedIncrement(&redeploys);__try{if(*(float*)(h+0x20)<=0)observedDead=true;}__except(1){}}
 else {retiringHealth=retiringOwner=0;observedDead=false;}
 ReleaseSRWLockExclusive(&configLock);return accepted;
}
static int Hex(wchar_t c){return c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:-1;}
static void ReadConfig(){wchar_t v[256];char name[64]{};GetPrivateProfileStringW(L"LocalFun",L"PlayerHex",L"",v,256,configPath);bool valid=true;unsigned i=0;
 for(;i<63&&v[i*2];i++){int a=Hex(v[i*2]),b=Hex(v[i*2+1]);if(a<0||b<0||(a==0&&b==0)){valid=false;break;}name[i]=(char)(a*16+b);}if(v[i*2])valid=false;
 GetPrivateProfileStringW(L"LocalFun",L"Tick",L"0",v,256,configPath);unsigned __int64 tick=0;for(unsigned j=0;v[j];j++){if(v[j]<'0'||v[j]>'9'||j>=18){valid=false;break;}tick=tick*10+v[j]-'0';}
 GetPrivateProfileStringW(L"LocalFun",L"GodMode",L"0",v,256,configPath);bool active=v[0]=='1'&&v[1]==0&&valid;
 AcquireSRWLockExclusive(&configLock);if(!active||memcmp(name,playerName,64)){protectedHealth=0;protectedOwner=0;retiringHealth=retiringOwner=0;observedDead=false;}on=active;lease=tick;for(unsigned j=0;j<64;j++)playerName[j]=name[j];ReleaseSRWLockExclusive(&configLock);
}
struct Hook {unsigned slot;uintptr_t before,after;};
static bool Swap(const Hook& h,bool restore){auto slot=(volatile LONG64*)(game+h.slot);DWORD old,tmp;if(!VirtualProtect((void*)slot,8,4,&old))return false;bool ok=(uintptr_t)InterlockedCompareExchange64(slot,restore?h.before:h.after,restore?h.after:h.before)==(restore?h.after:h.before);VirtualProtect((void*)slot,8,old,&tmp);return ok;}
static DWORD WINAPI Start(void*){
 if(!InitPaths(L"\\local-god-v2.ini",L"\\local-god-v2-state.json",nullptr))return 1;
 game=(uintptr_t)GetModuleHandleW(nullptr);if(game!=0x140000000)return 2;
 if(memcmp((void*)(game+0x255f90),"\x48\x8b\xc4\x48\x89\x50\x10\x55\x56\x57",10))return 3;
 originalDamage=*(Apply*)(game+0x1d82180+0x108);originalSet=*(SetHealth*)(game+0x1d82180+0xf8);originalImmortal=*(Query*)(game+0x1d82180+0x150);originalUpdate=*(Update*)(game+0x1d82180+0x238);maximumHealth=(GetHealth)(game+0x11d3850);originalSuicide=(Suicide)(game+0x11c7960);
 if(memcmp((void*)originalSuicide,"\x48\x8b\xc4\x55\x48\x8d\xa8\x48\xff\xff\xff",11))return 4;
 Hook hooks[]={{0x1d82288,(uintptr_t)originalDamage,(uintptr_t)&Damage},{0x1d82278,(uintptr_t)originalSet,(uintptr_t)&Set},{0x1d822d0,(uintptr_t)originalImmortal,(uintptr_t)&Immortal},{0x1d823b8,(uintptr_t)originalUpdate,(uintptr_t)&Tick},{0x21060b8,(uintptr_t)originalSuicide,(uintptr_t)&Redeploy}};
 auto fun=GetModuleHandleW(L"BF4LocalFun.dll");uintptr_t normal[]={game+0x255f90,game+0x2542e0,game+0x2544d0,game+0x264a70,game+0x11c7960};
 for(unsigned i=0;i<5;i++){MEMORY_BASIC_INFORMATION mbi{};auto addr=hooks[i].before;bool native=addr==normal[i];bool known=VirtualQuery((void*)addr,&mbi,sizeof(mbi))&&(fun&&mbi.AllocationBase==fun&&i==0);if(!native&&!known)return 5;if(*(uintptr_t*)(game+hooks[i].slot)!=addr)return 6;}
 unsigned done=0;for(;done<5;done++)if(!Swap(hooks[done],false))break;
 if(done!=5){while(done)Swap(hooks[--done],true);return 6;}installed=true;
 for(;;){ReadConfig();AcquireSRWLockExclusive(&configLock);bool active=Active();bool ready=active&&protectedHealth!=0;ReleaseSRWLockExclusive(&configLock);
 Write(statePath,false,R"json({"version":2,"pid":%lu,"installed":true,"enabled":%s,"protected":%s,"blockedDamage":%ld,"clampedHealthWrites":%ld,"refills":%ld,"ticks":%ld,"redeploys":%ld,"healthMilli":%ld,"maximumMilli":%ld})json",GetCurrentProcessId(),active?"true":"false",ready?"true":"false",blocked,clamped,refilled,updates,redeploys,lastHealth,lastMaximum);Sleep(500);}
}
#ifndef GOD_TEST
extern "C" BOOL WINAPI DllMain(HINSTANCE module,DWORD reason,void*){if(reason==DLL_PROCESS_ATTACH){selfModule=module;DisableThreadLibraryCalls(module);auto t=CreateThread(nullptr,0,Start,nullptr,0,nullptr);if(t)CloseHandle(t);}return TRUE;}
#endif
