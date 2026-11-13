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
using Apply=uintptr_t(__fastcall*)(uintptr_t,uintptr_t,uintptr_t);
static Apply original;
static uintptr_t game;
static SRWLOCK configLock=SRWLOCK_INIT,queueLock=SRWLOCK_INIT;
static bool on=false,excludeHumans=true,installed=false;
static unsigned __int64 lease=0;
static char playerName[64];
static volatile LONG hits=0,lethal=0,dropped=0;



struct Event {uintptr_t attacker,victim;int before,after,raw;unsigned team,vteam;};
static Event events[64],last{};static unsigned readIndex=0,writeIndex=0;
static int Milli(float f){return f>=-1000000&&f<=1000000?(int)(f*1000):-2147483647;}
static void Write(const wchar_t* path,bool append,const char* format,...){char line[1024];va_list a;__va_start(&a,format);int n=wvsprintfA(line,format,a);if(n<0||n>1020)return;line[n++]='\r';line[n++]='\n';HANDLE h=CreateFileW(path,append?FILE_APPEND_DATA:0x40000000,FILE_SHARE_READ|FILE_SHARE_WRITE,nullptr,append?OPEN_ALWAYS:2,FILE_ATTRIBUTE_NORMAL,nullptr);if(h==INVALID_HANDLE_VALUE)return;DWORD done;WriteFile(h,line,n,&done,nullptr);CloseHandle(h);}
static bool Active(){auto now=GetTickCount64();return on&&lease<=now&&now-lease<4000&&playerName[0];}
static bool Before(uintptr_t health,uintptr_t info,Event* e){
 __try {
  if(*(uintptr_t*)health!=game+0x1d82180)return false;
  auto giver=*(uintptr_t*)(info+0xb0);if(!giver)return false;
  auto weak=*(uintptr_t*)giver;if(!weak)return false;
  auto attacker=*(uintptr_t*)weak;if(attacker<0x10008)return false;attacker-=8;
  auto context=*(uintptr_t*)(game+0x2c68148);auto manager=*(uintptr_t*)(context+0x60);
  unsigned count=*(unsigned*)(manager+0x10);if(count==0||count>70)return false;
  auto players=*(uintptr_t**)(manager+0x928);bool found=false;
  for(unsigned i=0;i<count;i++)if(players[i]==attacker){found=true;break;}if(!found)return false;
  auto input=*(uintptr_t*)(attacker+0x14a0);if(input>=0x10010&&*(uintptr_t*)(input-16)==attacker)return false;
  unsigned n=0;for(;n<64;n++){char c=*(char*)(attacker+0x40+n);if(c!=playerName[n])return false;if(!c)break;}if(n==64)return false;
  auto owner=**(uintptr_t**)(health+8);auto victim=*(uintptr_t*)(owner+0x268);
  if(!victim||victim==attacker||*(uintptr_t*)(victim+0x14a8)!=owner||*(uintptr_t*)(owner+0x1f0)!=health)return false;
  found=false;for(unsigned i=0;i<count;i++)if(players[i]==victim){found=true;break;}if(!found)return false;
  input=*(uintptr_t*)(victim+0x14a0);bool bot=input>=0x10010&&*(uintptr_t*)(input-16)==victim;if(excludeHumans&&!bot)return false;
  e->team=*(unsigned*)(attacker+0x13cc);e->vteam=*(unsigned*)(victim+0x13cc);
  if(e->team<1||e->team>2||e->vteam<1||e->vteam>2||e->team==e->vteam)return false;
  e->attacker=*(uintptr_t*)(attacker+0x38);e->victim=*(uintptr_t*)(victim+0x38);
  e->before=Milli(*(float*)(health+0x20));e->raw=Milli(*(float*)(info+0x88));e->after=-2147483647;
  return e->before>0&&e->raw>0;
 }__except(1){return false;}
}
static uintptr_t __fastcall Damage(uintptr_t health,uintptr_t result,uintptr_t info){
 Event e{};bool apply=false;
 AcquireSRWLockExclusive(&configLock);if(Active())apply=Before(health,info,&e);ReleaseSRWLockExclusive(&configLock);
 if(!apply)return original(health,result,info);
 float raw=*(float*)(info+0x88);uintptr_t returned;
 // Change this hit only; keep its shooter, weapon and death/score processing.
 __try {*(float*)(info+0x88)=10000.0f;returned=original(health,result,info);}
 __finally {*(float*)(info+0x88)=raw;}
 __try {e.after=Milli(*(float*)(health+0x20));}__except(1){}
 InterlockedIncrement(&hits);if(e.after<=0&&e.after!=-2147483647)InterlockedIncrement(&lethal);
 if(TryAcquireSRWLockExclusive(&queueLock)){unsigned next=(writeIndex+1)%64;if(next!=readIndex){events[writeIndex]=e;writeIndex=next;}else InterlockedIncrement(&dropped);ReleaseSRWLockExclusive(&queueLock);}else InterlockedIncrement(&dropped);
 return returned;
}
static int Hex(wchar_t c){return c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:-1;}
static void ReadConfig(){wchar_t v[256];char name[64]{};GetPrivateProfileStringW(L"LocalFun",L"PlayerHex",L"",v,256,configPath);bool valid=true;unsigned i=0;
 for(;i<63&&v[i*2];i++){int a=Hex(v[i*2]),b=Hex(v[i*2+1]);if(a<0||b<0||(a==0&&b==0)){valid=false;break;}name[i]=(char)(a*16+b);}if(v[i*2])valid=false;
 GetPrivateProfileStringW(L"LocalFun",L"Tick",L"0",v,256,configPath);unsigned __int64 tick=0;for(unsigned j=0;v[j];j++){if(v[j]<'0'||v[j]>'9'||j>=18){valid=false;break;}tick=tick*10+v[j]-'0';}
 GetPrivateProfileStringW(L"LocalFun",L"OneHitKill",L"0",v,256,configPath);bool active=v[0]=='1'&&v[1]==0&&valid;
 GetPrivateProfileStringW(L"LocalFun",L"ExcludeHumans",L"1",v,256,configPath);bool exclude=!(v[0]=='0'&&v[1]==0);
 AcquireSRWLockExclusive(&configLock);on=active;excludeHumans=exclude;lease=tick;for(unsigned j=0;j<64;j++)playerName[j]=name[j];ReleaseSRWLockExclusive(&configLock);
}
static void Drain(){for(;;){Event e{};bool got=false;AcquireSRWLockExclusive(&queueLock);if(readIndex!=writeIndex){e=events[readIndex];readIndex=(readIndex+1)%64;got=true;}ReleaseSRWLockExclusive(&queueLock);if(!got)return;last=e;Write(logPath,true,R"json({"tick":%I64u,"attacker":%I64u,"victim":%I64u,"team":%u,"victimTeam":%u,"rawMilli":%d,"beforeMilli":%d,"afterMilli":%d})json",GetTickCount64(),e.attacker,e.victim,e.team,e.vteam,e.raw,e.before,e.after);}}
static bool Swap(uintptr_t wanted,uintptr_t next){auto slot=(volatile LONG64*)(game+0x1d82180+0x108);DWORD old,tmp;if(!VirtualProtect((void*)slot,8,4,&old))return false;bool ok=(uintptr_t)InterlockedCompareExchange64(slot,next,wanted)==wanted;VirtualProtect((void*)slot,8,old,&tmp);return ok;}
static DWORD WINAPI Start(void*){
 if(!InitPaths(L"\\local-fun-hit.ini",L"\\local-fun-hit-state.json",L"\\local-fun-hit.jsonl"))return 1;
 game=(uintptr_t)GetModuleHandleW(nullptr);if(game!=0x140000000)return 2;
 if(memcmp((void*)(game+0x255f90),"\x48\x8b\xc4\x48\x89\x50\x10\x55\x56\x57",10))return 3;
 original=(Apply)(game+0x255f90);if(!Swap((uintptr_t)original,(uintptr_t)&Damage)){Write(statePath,false,R"json({"version":2,"pid":%lu,"installed":false,"enabled":false,"error":"Unexpected prior handler"})json",GetCurrentProcessId());return 4;}installed=true;
 for(;;){ReadConfig();Drain();AcquireSRWLockExclusive(&configLock);bool active=Active();ReleaseSRWLockExclusive(&configLock);Write(statePath,false,R"json({"version":2,"pid":%lu,"installed":true,"enabled":%s,"hits":%ld,"lethal":%ld,"dropped":%ld,"lastBeforeMilli":%d,"lastAfterMilli":%d})json",GetCurrentProcessId(),active?"true":"false",hits,lethal,dropped,last.before,last.after);Sleep(500);}
}
#ifndef OHK_TEST
extern "C" BOOL WINAPI DllMain(HINSTANCE module,DWORD reason,void*){if(reason==DLL_PROCESS_ATTACH){selfModule=module;DisableThreadLibraryCalls(module);auto t=CreateThread(nullptr,0,Start,nullptr,0,nullptr);if(t)CloseHandle(t);}return TRUE;}
#endif
