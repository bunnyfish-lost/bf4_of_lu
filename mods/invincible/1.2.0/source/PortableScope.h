#pragma once
#include "WinApi.h"
static bool HostExecutable(const wchar_t* path,const wchar_t* name){const wchar_t* base=path;for(auto p=path;*p;p++)if(*p==L'\\'||*p==L'/')base=p+1;unsigned n=0;while(base[n]&&name[n]){auto a=base[n],b=name[n];if(a>=L'A'&&a<=L'Z')a+=32;if(b>=L'A'&&b<=L'Z')b+=32;if(a!=b)return false;n++;}return !base[n]&&!name[n];}
static bool SameModuleDirectory(const wchar_t* a,const wchar_t* b){const wchar_t* ae=a,*be=b;for(auto p=a;*p;p++)if(*p==L'\\')ae=p;for(auto p=b;*p;p++)if(*p==L'\\')be=p;if(ae-a!=be-b)return false;for(unsigned i=0;i<(unsigned)(ae-a);i++){auto x=a[i],y=b[i];if(x>=L'A'&&x<=L'Z')x+=32;if(y>=L'A'&&y<=L'Z')y+=32;if(x!=y)return false;}return true;}
