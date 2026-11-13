#pragma once
using size_t=unsigned __int64;
using uintptr_t=unsigned __int64;
using int32_t=int;
using DWORD=unsigned long;
using LONG=long;
using LONG64=__int64;
using BOOL=int;
using HANDLE=void*;
using HINSTANCE=void*;
using LPVOID=void*;
using va_list=char*;
extern "C" void __cdecl __va_start(va_list*,...);
#pragma intrinsic(__va_start)
#define WINAPI __stdcall
#define MAX_PATH 260
#define TRUE 1
#define DLL_PROCESS_ATTACH 1
#define MEM_COMMIT 0x1000
#define PAGE_GUARD 0x100
#define PAGE_NOACCESS 1
#define PAGE_EXECUTE_READWRITE 0x40
#define FILE_APPEND_DATA 4
#define FILE_SHARE_READ 1
#define FILE_SHARE_WRITE 2
#define OPEN_ALWAYS 4
#define FILE_ATTRIBUTE_NORMAL 0x80
#define INVALID_HANDLE_VALUE ((HANDLE)-1)
struct MEMORY_BASIC_INFORMATION {void* BaseAddress;void* AllocationBase;DWORD AllocationProtect;unsigned short PartitionId;size_t RegionSize;DWORD State;DWORD Protect;DWORD Type;};
struct SRWLOCK {void* Ptr;};
#define SRWLOCK_INIT {nullptr}
#define API extern "C" __declspec(dllimport)
API HANDLE WINAPI CreateFileW(const wchar_t*,DWORD,DWORD,void*,DWORD,DWORD,HANDLE);
API BOOL WINAPI WriteFile(HANDLE,const void*,DWORD,DWORD*,void*);
API BOOL WINAPI CloseHandle(HANDLE);
API void WINAPI AcquireSRWLockExclusive(SRWLOCK*);
API void WINAPI ReleaseSRWLockExclusive(SRWLOCK*);
API size_t WINAPI VirtualQuery(const void*,MEMORY_BASIC_INFORMATION*,size_t);
API BOOL WINAPI VirtualProtect(void*,size_t,DWORD,DWORD*);
API void* WINAPI VirtualAlloc(void*,size_t,DWORD,DWORD);
API DWORD WINAPI GetModuleFileNameW(HINSTANCE,wchar_t*,DWORD);
API HINSTANCE WINAPI GetModuleHandleW(const wchar_t*);
API DWORD WINAPI GetCurrentProcessId();
API HANDLE WINAPI GetCurrentProcess();
API BOOL WINAPI FlushInstructionCache(HANDLE,const void*,size_t);
API BOOL WINAPI DisableThreadLibraryCalls(HINSTANCE);
API HANDLE WINAPI CreateThread(void*,size_t,DWORD(WINAPI*)(void*),void*,DWORD,DWORD*);
API DWORD WINAPI GetModuleFileNameA(HINSTANCE,char*,DWORD);
API DWORD WINAPI GetPrivateProfileStringA(const char*,const char*,const char*,char*,DWORD,const char*);
API int WINAPI wvsprintfA(char*,const char*,va_list);
API int __cdecl wsprintfW(wchar_t*,const wchar_t*,...);
extern "C" unsigned __int64 __readgsqword(unsigned long);
extern "C" LONG _InterlockedIncrement(volatile LONG*);
extern "C" LONG64 _InterlockedCompareExchange64(volatile LONG64*,LONG64,LONG64);
#pragma intrinsic(__readgsqword,_InterlockedIncrement,_InterlockedCompareExchange64)
#define InterlockedIncrement _InterlockedIncrement
#define InterlockedCompareExchange64 _InterlockedCompareExchange64
static int memcmp(const void* a,const void* b,size_t n){auto x=(const unsigned char*)a;auto y=(const unsigned char*)b;for(size_t i=0;i<n;i++)if(x[i]!=y[i])return x[i]-y[i];return 0;}
static wchar_t* wcsrchr(wchar_t* s,wchar_t c){wchar_t* last=nullptr;while(*s){if(*s==c)last=s;s++;}return last;}
static const wchar_t* wcsstr(const wchar_t* text,const wchar_t* needle){for(;*text;text++){size_t n=0;while(needle[n]&&text[n]==needle[n])n++;if(!needle[n])return text;}return nullptr;}
