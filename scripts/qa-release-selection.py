"""캡처 마우스 해제·역방향·단순 클릭·드래그 중 취소의 실제 UI 검사."""
import argparse,ctypes,json,time
from pathlib import Path
from PIL import Image,ImageGrab
from pywinauto import Application,keyboard
import win32api,win32clipboard,win32con
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
p=argparse.ArgumentParser();p.add_argument('--pid',type=int,required=True);p.add_argument('--report',required=True);a=p.parse_args()
app=Application(backend='win32').connect(process=a.pid);results=[]
def window(title):
    deadline=time.monotonic()+10
    while time.monotonic()<deadline:
        matches=[w for w in app.windows() if w.is_visible() and w.window_text()==title]
        if matches:return matches[0]
        time.sleep(.1)
    raise RuntimeError(title)
def key(w,keys):w.set_focus();keyboard.send_keys(keys,pause=.05,vk_packet=False);time.sleep(.2)
def sentinel():
    win32clipboard.OpenClipboard()
    try:win32clipboard.EmptyClipboard();win32clipboard.SetClipboardData(win32con.CF_UNICODETEXT,'Clip cancel sentinel')
    finally:win32clipboard.CloseClipboard()
def unchanged():
    win32clipboard.OpenClipboard()
    try:assert win32clipboard.GetClipboardData(win32con.CF_UNICODETEXT)=='Clip cancel sentinel'
    finally:win32clipboard.CloseClipboard()
sentinel();key(window('Clip'),'^+2');overlay=window('Clip 영역 선택');overlay.set_focus()
win32api.SetCursorPos((300,300));win32api.mouse_event(2,0,0,0,0);win32api.mouse_event(4,0,0,0,0);time.sleep(.25)
assert overlay.is_visible();unchanged();key(overlay,'{ESC}');window('Clip')
results.append({'scenario':'단순 클릭은 캡처하지 않음·Esc 취소','passed':True})
for mode in ['escape','right']:
    sentinel();key(window('Clip'),'^+2');overlay=window('Clip 영역 선택');overlay.set_focus()
    win32api.SetCursorPos((200,200));win32api.mouse_event(2,0,0,0,0);win32api.SetCursorPos((600,500));time.sleep(.15)
    if mode=='escape':keyboard.send_keys('{ESC}')
    else:win32api.mouse_event(8,0,0,0,0);win32api.mouse_event(16,0,0,0,0)
    win32api.mouse_event(4,0,0,0,0);window('Clip');unchanged()
    results.append({'scenario':'드래그 중 '+mode+' 취소는 복사하지 않음','passed':True})
key(window('Clip'),'^+2');overlay=window('Clip 영역 선택');overlay.set_focus()
win32api.SetCursorPos((921,645));win32api.mouse_event(2,0,0,0,0)
for i in range(1,11):win32api.SetCursorPos((921-i*80,645-i*45));time.sleep(.02)
win32api.mouse_event(4,0,0,0,0);main=window('Clip · 저장 전');time.sleep(.3)
image=ImageGrab.grabclipboard();assert isinstance(image,Image.Image) and image.size==(800,450)
key(main,'^s');results.append({'scenario':'역방향 드래그 해제 즉시 800×450 캡처·복사','passed':True})
Path(a.report).write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS',len(results))
