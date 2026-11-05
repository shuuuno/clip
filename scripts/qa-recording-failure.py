"""저장 폴더 자리에 일반 파일을 둔 격리 설정에서 실패 후 UI 복귀를 확인한다."""
import ctypes,json,sys,time
from pathlib import Path
from pywinauto import Application,keyboard
import win32api
sys.stdout.reconfigure(encoding='utf-8')
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
pid=int(sys.argv[1]);app=Application(backend='win32').connect(process=pid)
def wait(title):
    until=time.monotonic()+15
    while time.monotonic()<until:
        found=[w for w in app.windows() if w.window_text()==title and w.is_visible()]
        if found:return found[0]
        time.sleep(.1)
    raise RuntimeError(title)
main=wait('Clip');main.set_focus();keyboard.send_keys('^+9',vk_packet=False)
w=wait('Clip 영역 선택');w.set_focus();win32api.SetCursorPos((121,195));win32api.mouse_event(2,0,0,0,0);time.sleep(.1)
win32api.SetCursorPos((921,645));time.sleep(.1);win32api.mouse_event(4,0,0,0,0);keyboard.send_keys('{ENTER}',vk_packet=False)
w=wait('Clip 녹화');Application(backend='uia').connect(process=pid).window(handle=w.handle).child_window(title='녹화 시작',control_type='Button').invoke()
main=wait('Clip');texts=[c.window_text() for c in Application(backend='uia').connect(process=pid).window(handle=main.handle).descendants(control_type='Text')]
assert any('녹화 저장 실패' in t for t in texts),texts
data=Path('artifacts/ui/recording-failure-data');assert (data/'blocked').read_text(encoding='utf-8-sig').strip()=='preserve-this-file'
assert not list(data.rglob('*.mp4'))
r={'returned_to_library':True,'existing_file_preserved':True,'mp4_count':0,'error':next(t for t in texts if '녹화 저장 실패' in t)}
Path('artifacts/ui/recording-failure-results.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(r,ensure_ascii=False))
