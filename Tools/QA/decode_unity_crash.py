import ctypes as c, re
from ctypes import wintypes as w
from pathlib import Path

root=Path('E:/UnityWork/CapstoneProjectData/Artifacts/ui-r3-fixes-2026-10-20')
log=(root/'after-standalone/player.log').read_text(encoding='utf-8',errors='replace')
module=re.search(r'UnityPlayer\.dll:UnityPlayer\.dll \(([0-9A-Fa-f]+)\)',log)
base=int(module.group(1),16)
dbg=c.WinDLL('dbghelp',use_last_error=True); kernel=c.WinDLL('kernel32',use_last_error=True)
kernel.GetCurrentProcess.restype=w.HANDLE
process=kernel.GetCurrentProcess()
dbg.SymInitializeW.argtypes=[w.HANDLE,w.LPCWSTR,w.BOOL]
dbg.SymInitializeW.restype=w.BOOL
dbg.SymLoadModuleExW.argtypes=[w.HANDLE,w.HANDLE,w.LPCWSTR,w.LPCWSTR,c.c_ulonglong,w.DWORD,c.c_void_p,w.DWORD]
dbg.SymLoadModuleExW.restype=c.c_ulonglong
dbg.SymFromAddr.argtypes=[w.HANDLE,c.c_ulonglong,c.POINTER(c.c_ulonglong),c.c_void_p]
dbg.SymFromAddr.restype=w.BOOL
class SYMBOL(c.Structure):
    _fields_=[('SizeOfStruct',w.ULONG),('TypeIndex',w.ULONG),('Reserved',c.c_ulonglong*2),('Index',w.ULONG),('Size',w.ULONG),('ModBase',c.c_ulonglong),('Flags',w.ULONG),('Value',c.c_ulonglong),('Address',c.c_ulonglong),('Register',w.ULONG),('Scope',w.ULONG),('Tag',w.ULONG),('NameLen',w.ULONG),('MaxNameLen',w.ULONG),('Name',c.c_char*1)]
symbols='D:/VTC/6000.3.16f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/Variations/win64_player_development_mono'
if not dbg.SymInitializeW(process,symbols,False): raise c.WinError(c.get_last_error())
dll=str(root/'builds/after-mono/standalone/UnityPlayer.dll')
loaded=dbg.SymLoadModuleExW(process,None,dll,None,base,0,None,0)
lines=[f'Loaded symbols base={loaded:x}; error={c.get_last_error()}']
for address in re.findall(r'^(0x[0-9A-Fa-f]+) \(UnityPlayer\)',log,re.M):
    buffer=c.create_string_buffer(c.sizeof(SYMBOL)+2048); info=c.cast(buffer,c.POINTER(SYMBOL))
    info.contents.SizeOfStruct=c.sizeof(SYMBOL);info.contents.MaxNameLen=2048
    displacement=c.c_ulonglong()
    if dbg.SymFromAddr(process,int(address,16),c.byref(displacement),buffer):
        name=c.string_at(c.addressof(buffer)+SYMBOL.Name.offset,info.contents.NameLen).decode(errors='replace')
        lines.append(f'{address} {name}+{displacement.value:x}')
    else: lines.append(f'{address} unresolved error={c.get_last_error()}')
(root/'after-standalone/crash-symbols.txt').write_text('\n'.join(lines),encoding='utf-8')
print('\n'.join(lines))
