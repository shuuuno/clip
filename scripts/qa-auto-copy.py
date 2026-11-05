"""합성 fixture가 열린 상태에서 캡처 즉시 복사와 점유 실패 복구를 확인한다."""
import argparse,ctypes,json,sys,time
from pathlib import Path
import win32api,win32clipboard,win32con
from PIL import Image,ImageGrab
from pywinauto import Application,keyboard
sys.stdout.reconfigure(encoding='utf-8');ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
p=argparse.ArgumentParser();p.add_argument('--pid',type=int,required=True);p.add_argument('--data',required=True);p.add_argument('--report',required=True);a=p.parse_args()
native=Application(backend='win32').connect(process=a.pid);ui=Application(backend='uia').connect(process=a.pid);folder=Path(a.data)/'captures';results=[]
def window(title):
    deadline=time.monotonic()+10
    while time.monotonic()<deadline:
        found=[w for w in native.windows() if w.window_text()==title and w.is_visible()]
        if found:return found[0]
        time.sleep(.1)
    raise RuntimeError(title)
def key(w,keys):w.set_focus();keyboard.send_keys(keys,pause=.05,vk_packet=False);time.sleep(.2)
def texts(w):return [c.window_text() for c in ui.window(handle=w.handle).descendants(control_type='Text')]
def select():
    key(window('Clip'),'^+2');w=window('Clip 영역 선택');w.set_focus();win32api.SetCursorPos((121,195));win32api.mouse_event(2,0,0,0,0);time.sleep(.06)
    for i in range(1,11):win32api.SetCursorPos((121+i*80,195+i*45));time.sleep(.02)
    win32api.mouse_event(4,0,0,0,0);return w
win32clipboard.OpenClipboard()
try:win32clipboard.EmptyClipboard();win32clipboard.SetClipboardData(win32con.CF_UNICODETEXT,'Clip clipboard sentinel')
finally:win32clipboard.CloseClipboard()
w=select();key(w,'{ENTER}');main=window('Clip · 저장 전');time.sleep(.3)
auto=ImageGrab.grabclipboard();assert isinstance(auto,Image.Image) and auto.size==(800,450),type(auto)
assert any('캡처를 클립보드에 복사' in text for text in texts(main)),texts(main)
before=set(folder.glob('*.png'));key(main,'^s');new=set(folder.glob('*.png'))-before;assert len(new)==1
saved=new.pop();assert Image.open(saved).convert('RGBA').tobytes()==auto.convert('RGBA').tobytes()
results.append({'scenario':'Enter 캡처 확정 즉시 복사·PNG와 픽셀 일치','passed':True,'size':[800,450],'file':str(saved)})
w=select();win32clipboard.OpenClipboard()
try:
    key(w,'{ENTER}');main=window('Clip · 저장 전');deadline=time.monotonic()+12
    while time.monotonic()<deadline and not any('클립보드가 사용 중' in text for text in texts(main)):time.sleep(.2)
    assert any('클립보드가 사용 중' in text for text in texts(main)),texts(main)
finally:win32clipboard.CloseClipboard()
key(main,'^c');im=ImageGrab.grabclipboard();assert isinstance(im,Image.Image) and im.size==(800,450)
key(main,'^s');results.append({'scenario':'클립보드 점유 실패 후 캡처 유지·복사 재시도','passed':True})
Path(a.report).write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(results,ensure_ascii=False,indent=2))
