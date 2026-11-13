#define OHK_TEST
#include "OneHit.cpp"
API void WINAPI ExitProcess(unsigned);
static unsigned passed=0,failed=0;static const wchar_t* report=L"test-results.txt";
static unsigned char *att,*vic,*mgr,*damageInfo,*healthMem,*actor,*inputA,*inputV;static uintptr_t *rosterMem,*ownerRef,*weakRef,*giverRef;static unsigned callsOriginal=0;
static uintptr_t __fastcall Normal(uintptr_t h,uintptr_t result,uintptr_t info){callsOriginal++;*(float*)(h+0x20)-=*(float*)(info+0x88);return result;}
static void* Alloc(size_t n){return VirtualAlloc(nullptr,n,0x3000,4);}
static void Reset(){on=true;lease=GetTickCount64();excludeHumans=true;playerName[0]='t';playerName[1]=0;att[0x40]='t';att[0x41]=0;*(unsigned*)(mgr+0x10)=70;*(unsigned*)(att+0x13cc)=1;*(unsigned*)(vic+0x13cc)=2;*(uintptr_t*)inputA=0;*(uintptr_t*)inputV=(uintptr_t)vic;rosterMem[0]=(uintptr_t)att;rosterMem[1]=(uintptr_t)vic;*(uintptr_t*)(vic+0x14a8)=(uintptr_t)actor;*(float*)(healthMem+0x20)=100;*(float*)(damageInfo+0x88)=25;callsOriginal=0;}
static void Case(const char* name,bool expected){LONG before=hits;uintptr_t out=Damage((uintptr_t)healthMem,0x1234,(uintptr_t)damageInfo);bool ok=(hits-before==(expected?1:0))&&callsOriginal==1&&out==0x1234&&*(float*)(damageInfo+0x88)==25&&*(float*)(healthMem+0x20)==(expected?-9900:75);if(ok)passed++;else failed++;Write(report,true,"%s %s",ok?"PASS":"FAIL",name);}
extern "C" void main(){
 game=(uintptr_t)Alloc(0x2c69000);auto ctx=(uintptr_t*)Alloc(0x100);mgr=(unsigned char*)Alloc(0xa00);att=(unsigned char*)Alloc(0x1500);vic=(unsigned char*)Alloc(0x1500);actor=(unsigned char*)Alloc(0x300);healthMem=(unsigned char*)Alloc(0x80);damageInfo=(unsigned char*)Alloc(0xc0);inputA=(unsigned char*)Alloc(32);inputV=(unsigned char*)Alloc(32);rosterMem=(uintptr_t*)Alloc(70*8);ownerRef=(uintptr_t*)Alloc(8);weakRef=(uintptr_t*)Alloc(8);giverRef=(uintptr_t*)Alloc(8);
 *(uintptr_t*)(game+0x2c68148)=(uintptr_t)ctx;*(uintptr_t*)((uintptr_t)ctx+0x60)=(uintptr_t)mgr;*(uintptr_t*)(mgr+0x928)=(uintptr_t)rosterMem;*(uintptr_t*)healthMem=game+0x1d82180;*ownerRef=(uintptr_t)actor;*(uintptr_t*)(healthMem+8)=(uintptr_t)ownerRef;*(uintptr_t*)(actor+0x268)=(uintptr_t)vic;*(uintptr_t*)(actor+0x1f0)=(uintptr_t)healthMem;
 *(uintptr_t*)(att+0x14a0)=(uintptr_t)(inputA+16);*(uintptr_t*)(vic+0x14a0)=(uintptr_t)(inputV+16);*weakRef=(uintptr_t)att+8;*giverRef=(uintptr_t)weakRef;*(uintptr_t*)(damageInfo+0xb0)=(uintptr_t)giverRef;original=Normal;
 Write(report,false,"Actual Before/Damage integration tests (synthetic memory)");
 Reset();Case("selected human enemy bot with 70-slot roster",true);
 Reset();on=false;Case("disabled",false);
 Reset();lease=GetTickCount64()-5000;Case("expired lease",false);
 Reset();lease=GetTickCount64()+5000;Case("future lease",false);
 Reset();playerName[0]='x';Case("different human",false);
 Reset();*(uintptr_t*)inputA=(uintptr_t)att;Case("bot shooter",false);
 Reset();*(unsigned*)(vic+0x13cc)=1;Case("same team",false);
 Reset();*(uintptr_t*)inputV=0;Case("excluded human victim",false);
 Reset();*(uintptr_t*)inputV=0;excludeHumans=false;Case("explicit human opt-in",true);
 Reset();rosterMem[0]=0;Case("attacker not in roster",false);
 Reset();rosterMem[1]=0;Case("victim not in roster",false);
 Reset();*(unsigned*)(mgr+0x10)=71;Case("unexpected roster count",false);
 Reset();*(uintptr_t*)(vic+0x14a8)=0;Case("stale soldier owner",false);
 Reset();*(uintptr_t*)(damageInfo+0xb0)=1;Case("invalid weak pointer",false);
 Write(report,true,"Passed=%u Failed=%u",passed,failed);ExitProcess(failed?1:0);
}
