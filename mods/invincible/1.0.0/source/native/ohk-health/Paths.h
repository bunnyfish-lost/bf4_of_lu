#pragma once
static HINSTANCE selfModule;
static wchar_t configPath[1024],statePath[1024],logPath[1024],expectedExe[1024];
static bool CopyPath(wchar_t* to,const wchar_t* from){unsigned n=0;for(;from[n]&&n<1023;n++)to[n]=from[n];to[n]=0;return !from[n];}
static bool AddPath(wchar_t* to,const wchar_t* tail){unsigned n=0;while(to[n]&&n<1023)n++;unsigned i=0;for(;tail[i]&&n<1023;i++)to[n++]=tail[i];to[n]=0;return !tail[i];}
static bool ParentPath(wchar_t* path){auto end=wcsrchr(path,L'\\');if(!end)return false;*end=0;return true;}
static bool EqualPath(const wchar_t* a,const wchar_t* b){for(;*a&&*b;a++,b++){auto x=*a,y=*b;if(x>=L'A'&&x<=L'Z')x+=32;if(y>=L'A'&&y<=L'Z')y+=32;if(x!=y)return false;}return *a==*b;}
static bool InitPaths(const wchar_t* config,const wchar_t* state,const wchar_t* log){
 static wchar_t folder[1024],exe[1024];
 DWORD n=GetModuleFileNameW(selfModule,folder,1024);if(!n||n>=1024)return false;
 for(unsigned i=0;i<3;i++)if(!ParentPath(folder))return false;
 if(!CopyPath(configPath,folder)||!AddPath(configPath,config)||!CopyPath(statePath,folder)||!AddPath(statePath,state))return false;
 if(log&&(!CopyPath(logPath,folder)||!AddPath(logPath,log)))return false;
 for(unsigned i=0;i<2;i++)if(!ParentPath(folder))return false;
 if(!CopyPath(expectedExe,folder)||!AddPath(expectedExe,L"\\runtime\\GameServer\\bf4_Server_Final.exe"))return false;
 n=GetModuleFileNameW(nullptr,exe,1024);return n>0&&n<1024&&EqualPath(exe,expectedExe);
}
