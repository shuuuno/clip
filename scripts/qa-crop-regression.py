"""띠 제거 UI 검사 직후 기존 크롭·복사·되돌리기 회귀를 확인한다."""
import ctypes,json,sys,time
from pathlib import Path
from PIL import Image,ImageGrab
from pywinauto import Application,keyboard
import win32api
sys.stdout.reconfigure(encoding='utf-8');ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
pid=int(sys.argv[1]);main=next(w for w in Application(backend='win32').connect(process=pid).windows() if w.window_text()=='Clip');main.maximize()
ui=Application(backend='uia').connect(process=pid).window(handle=main.handle)
next(c for c in ui.descendants(control_type='ComboBox') if '50%' in c.texts()).select('100%')
ui.child_window(title='크롭  C',control_type='Button').invoke();time.sleep(.2)
def key(keys):main.set_focus();keyboard.send_keys(keys,pause=.05,vk_packet=False);time.sleep(.2)
def copy():key('^c');return ImageGrab.grabclipboard().convert('RGBA')
before=copy();assert before.size==(800,400)
r=main.rectangle();left=ui.child_window(control_type='List').rectangle().right+10
im=ImageGrab.grab(bbox=(left,r.top,r.right-12,r.bottom-12)).convert('RGB');mask=Image.new('L',im.size)
mask.putdata([255 if p==(255,0,255) else 0 for p in im.get_flattened_data()]);b=mask.getbbox();assert b
l,t=left+b[0],r.top+b[1];sx=(b[2]-b[0])/800;sy=(b[3]-b[1])/400
start=(round(l+100*sx),round(t+80*sy));end=(round(l+500*sx),round(t+280*sy))
win32api.SetCursorPos(start);win32api.mouse_event(2,0,0,0,0);time.sleep(.1)
for i in range(1,11):win32api.SetCursorPos((round(start[0]+(end[0]-start[0])*i/10),round(start[1]+(end[1]-start[1])*i/10)));time.sleep(.02)
win32api.mouse_event(4,0,0,0,0);key('{ENTER}');after=copy();expected=before.crop((100,80,500,280))
assert after.size==expected.size and after.tobytes()==expected.tobytes()
key('^z');restored=copy();assert restored.size==before.size and restored.tobytes()==before.tobytes()
result={'scenario':'띠 제거 후 기존 크롭·클립보드·Undo 회귀','passed':True,'crop_size':list(after.size)}
Path('artifacts/ui/crop-regression-0.4.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False))
