#define GOD_TEST
#include "Invincible.cpp"
API void WINAPI ExitProcess(unsigned);
static unsigned passed=0,failed=0;static unsigned char *player,*actor,*healthMem,*input,*mgr;static uintptr_t *arr,*ownerRef;static unsigned callsDamage=0,callsSet=0,callsTick=0;static bool drainTick=false;
static void* Alloc(size_t n){return VirtualAlloc(nullptr,n,0x3000,4);}
static uintptr_t __fastcall NormalDamage(uintptr_t h,uintptr_t r,uintptr_t){callsDamage++;*(float*)(h+0x20)-=25;return r;}
static void __fastcall NormalSet(uintptr_t h,float v){callsSet++;*(float*)(h+0x20)=v;}
static float __fastcall Maximum(uintptr_t h){return *(float*)(h+0x24);}
static bool __fastcall NormalImmortal(uintptr_t){return false;}
static void __fastcall NormalTick(uintptr_t h,uintptr_t){callsTick++;if(drainTick)*(float*)(h+0x20)=10;}
static bool rejectSuicide=false;
static bool __fastcall NormalSuicide(uintptr_t a,bool,bool){if(rejectSuicide)return false;Set(*(uintptr_t*)(a+0x1f0),0);return true;}
static void Expect(bool yes,const char* name){if(yes)passed++;else failed++;Write(L"test-results.txt",true,"%s %s",yes?"PASS":"FAIL",name);}
static void Reset(){on=true;lease=GetTickCount64();playerName[0]='t';playerName[1]=0;player[0x40]='t';player[0x41]=0;player[0x13c9]=0;*(uintptr_t*)input=0;*(unsigned*)(mgr+0x10)=70;arr[0]=(uintptr_t)player;*(uintptr_t*)(player+0x14a8)=(uintptr_t)actor;*(float*)(healthMem+0x20)=100;*(float*)(healthMem+0x24)=100;protectedHealth=protectedOwner=0;callsDamage=callsSet=callsTick=0;drainTick=false;retiringHealth=retiringOwner=0;observedDead=false;rejectSuicide=false;}
extern "C" void main(){game=(uintptr_t)Alloc(0x2c69000);auto ctx=(uintptr_t*)Alloc(0x100);mgr=(unsigned char*)Alloc(0xa00);player=(unsigned char*)Alloc(0x1500);actor=(unsigned char*)Alloc(0x300);healthMem=(unsigned char*)Alloc(0x6500);input=(unsigned char*)Alloc(32);arr=(uintptr_t*)Alloc(70*8);ownerRef=(uintptr_t*)Alloc(8);
 *(uintptr_t*)(game+0x2c68148)=(uintptr_t)ctx;*(uintptr_t*)((uintptr_t)ctx+0x60)=(uintptr_t)mgr;*(uintptr_t*)(mgr+0x928)=(uintptr_t)arr;*(uintptr_t*)healthMem=game+0x1d82180;*ownerRef=(uintptr_t)actor;*(uintptr_t*)(healthMem+8)=(uintptr_t)ownerRef;*(uintptr_t*)actor=game+0x2105df0;*(uintptr_t*)(actor+0x268)=(uintptr_t)player;*(uintptr_t*)(actor+0x1f0)=(uintptr_t)healthMem;*(uintptr_t*)(player+0x14a0)=(uintptr_t)(input+16);
 originalSuicide=NormalSuicide;originalDamage=NormalDamage;originalSet=NormalSet;maximumHealth=Maximum;originalImmortal=NormalImmortal;originalUpdate=NormalTick;
 uintptr_t h=(uintptr_t)healthMem;unsigned char result[0x28],info[0xc0]{};
 Write(L"test-results.txt",false,"Actual god-mode callbacks with synthetic memory");
 Reset();memset(result,0x7f,sizeof(result));Damage(h,(uintptr_t)result,(uintptr_t)info);bool zero=true;for(unsigned i=0;i<0x24;i++)if(result[i])zero=false;Expect(callsDamage==0&&*(float*)(healthMem+0x20)==100&&zero&&result[0x24]==0x7f,"damage rejected with correct result size");
 Reset();*(float*)(info+0x88)=1000000;info[0xbc]=255;Damage(h,(uintptr_t)result,(uintptr_t)info);Expect(callsDamage==0&&*(float*)(healthMem+0x20)==100&&*(float*)(info+0x88)==1000000,"lethal damage and forced flags rejected without altering input");
 Reset();Damage(h,(uintptr_t)result,0);Expect(callsDamage==0,"environmental damage needs no attacker");
 Reset();Set(h,0);Expect(*(float*)(healthMem+0x20)==100&&callsSet==1,"direct zero-health write clamped");
 Reset();Set(h,-1000000);Expect(*(float*)(healthMem+0x20)==100,"extreme negative health write clamped");
 Reset();*(float*)(healthMem+0x20)=37;Tick(h,0);Expect(*(float*)(healthMem+0x20)==100&&callsTick==1,"enabling heals existing living soldier");
 Reset();drainTick=true;Tick(h,0);Expect(*(float*)(healthMem+0x20)==100&&callsTick==1,"full-health upkeep after update");
 Reset();*(float*)(healthMem+0x24)=200;Set(h,0);Expect(*(float*)(healthMem+0x20)==200,"custom maximum health respected");
 Reset();Expect(Immortal(h),"engine immortal query enabled");
 Reset();on=false;Damage(h,(uintptr_t)result,0);Expect(callsDamage==1&&*(float*)(healthMem+0x20)==75&&!Immortal(h),"disabled original behavior");
 Reset();lease=GetTickCount64()-5000;Set(h,0);Expect(*(float*)(healthMem+0x20)==0,"expired control fails closed");
 Reset();player[0x40]='x';Damage(h,(uintptr_t)result,0);Expect(callsDamage==1,"other human unaffected");
 Reset();*(uintptr_t*)input=(uintptr_t)player;Damage(h,(uintptr_t)result,0);Expect(callsDamage==1,"bot unaffected");
 Reset();arr[0]=0;Damage(h,(uintptr_t)result,0);Expect(callsDamage==1,"absent roster member unaffected");
 Reset();*(uintptr_t*)(player+0x14a8)=0;Damage(h,(uintptr_t)result,0);Expect(callsDamage==1,"stale soldier rejected");
 Reset();*(float*)(healthMem+0x20)=0;Tick(h,0);Expect(*(float*)(healthMem+0x20)==0,"existing corpse not resurrected");
 Reset();player[0x13c9]=1;Damage(h,(uintptr_t)result,0);Expect(callsDamage==1,"spectator not protected");
 Reset();Set(h,0);on=false;Set(h,50);Expect(*(float*)(healthMem+0x20)==50,"disable releases health lock");
 Reset();Expect(Redeploy((uintptr_t)actor,false,true)&&*(float*)(healthMem+0x20)==0,"voluntary redeploy can kill retiring soldier");Tick(h,0);Expect(*(float*)(healthMem+0x20)==0&&!Immortal(h),"retiring body not healed by tick or immortal query");
 *(float*)(healthMem+0x20)=100;Tick(h,0);Set(h,0);Expect(*(float*)(healthMem+0x20)==100&&Immortal(h),"new life at reused address regains protection");
 Reset();rejectSuicide=true;Expect(!Redeploy((uintptr_t)actor,false,true),"rejected redeploy preserves original return");Set(h,0);Expect(*(float*)(healthMem+0x20)==100,"spam-guard rejection does not suspend invincibility");
 Reset();Redeploy((uintptr_t)actor,true,false);Expect(*(float*)(healthMem+0x20)==100,"forced non-menu death remains protected");
 Reset();player[0x40]='x';Redeploy((uintptr_t)actor,false,true);Expect(*(float*)(healthMem+0x20)==0&&retiringHealth==0,"other human redeploy unchanged");
 Reset();Redeploy((uintptr_t)actor,false,true);on=false;Set(h,20);Expect(*(float*)(healthMem+0x20)==20,"disable still restores normal set after redeploy");
 Write(L"test-results.txt",true,"Passed=%u Failed=%u",passed,failed);ExitProcess(failed?1:0);
}
