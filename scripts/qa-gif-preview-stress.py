"""900프레임 GIF 한 바퀴 재생 중 프로세스 메모리와 프레임 진행을 확인한다."""
import ctypes,json,sys,time,subprocess
from pathlib import Path
from pywinauto import Application,keyboard
sys.stdout.reconfigure(encoding='utf-8');pid=int(sys.argv[1]);app=Application(backend='uia').connect(process=pid)
native=Application(backend='win32').connect(process=pid)
main=app.window(title='Clip');opening=time.monotonic();next(c for c in main.descendants(control_type='ListItem') if '.gif' in c.window_text()).select()
deadline=time.monotonic()+20
while time.monotonic()<deadline:
    found=[w for w in native.windows() if w.window_text().startswith('Clip GIF ·') and w.is_visible()]
    if found:break
    time.sleep(.1)
assert found,'20초 안에 GIF 창이 열리지 않았습니다.'
viewer=app.window(handle=found[0].handle).wrapper_object();open_seconds=time.monotonic()-opening
class Memory(ctypes.Structure):
    _fields_=[('cb',ctypes.c_ulong),('faults',ctypes.c_ulong)]+[(name,ctypes.c_size_t) for name in ['peak_working','working','peak_paged','paged','peak_nonpaged','nonpaged','pagefile','peak_pagefile','private']]
k=ctypes.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[ctypes.c_ulong,ctypes.c_int,ctypes.c_ulong];k.OpenProcess.restype=ctypes.c_void_p;k.CloseHandle.argtypes=[ctypes.c_void_p]
ps=ctypes.WinDLL('psapi',use_last_error=True);ps.GetProcessMemoryInfo.argtypes=[ctypes.c_void_p,ctypes.POINTER(Memory),ctypes.c_ulong]
handle=k.OpenProcess(0x410,False,pid);assert handle
samples=[];started=time.monotonic();over_limit=False
try:
    while time.monotonic()-started<32:
        value=Memory();value.cb=ctypes.sizeof(value);assert ps.GetProcessMemoryInfo(handle,ctypes.byref(value),value.cb)
        rows=[c.window_text() for c in viewer.descendants(control_type='Text')]
        samples.append({'seconds':round(time.monotonic()-started,2),'working':value.working,'private':value.private,'status':rows[0] if rows else ''})
        if value.private>768*1024*1024:over_limit=True;break
        time.sleep(.5)
finally:
    k.CloseHandle(handle);viewer.set_focus();keyboard.send_keys('%{F4}',vk_packet=False);time.sleep(.2)
report={'open_seconds':round(open_seconds,3),'peak_working':max(s['working'] for s in samples),'peak_private':max(s['private'] for s in samples),'within_768_mib':not over_limit,'samples':samples}
Path('artifacts/ui/gif-preview-stress-results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in report.items() if k!='samples'},ensure_ascii=False))
subprocess.run([sys.executable,'scripts/qa-close-clip.py',str(pid)],check=True)
assert not over_limit
