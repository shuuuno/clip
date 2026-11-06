"""합성 PNG에서 주석·키보드 이동·삭제·Undo/Redo·출력 일치를 확인한다."""
import argparse, ctypes, hashlib, json, sys, time
from pathlib import Path
from PIL import Image, ImageGrab
from pywinauto import Application, keyboard, mouse

sys.stdout.reconfigure(encoding='utf-8')
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
p=argparse.ArgumentParser(); p.add_argument('--pid',type=int,required=True); p.add_argument('--data',required=True); p.add_argument('--report',required=True); a=p.parse_args()
native=Application(backend='win32').connect(process=a.pid)
main=next(w for w in native.windows() if w.window_text()=='Clip' and w.is_visible()); main.maximize(); time.sleep(.3)
ui=Application(backend='uia').connect(process=a.pid).window(handle=main.handle)
folder=Path(a.data)/'captures'; source=folder/'qa-strip.png'; original_hash=hashlib.sha256(source.read_bytes()).hexdigest(); results=[]
listing=ui.child_window(control_type='List').wrapper_object()
for attempt in range(20):
    found=[c for c in ui.descendants(control_type='ListItem') if 'qa-strip.png' in c.window_text()]
    if found:break
    listing.scroll('down','page');time.sleep(.1)
assert found,'qa-strip.png';found[0].select();time.sleep(.2)
next(c for c in ui.descendants(control_type='ComboBox') if c.window_text()=='확대 비율').select('100%')
ui.child_window(title='선택  V',control_type='Button').invoke();time.sleep(.1)

def key(value):main.set_focus(); keyboard.send_keys(value,pause=.04,vk_packet=False); time.sleep(.2)
def copied():
    key('^c'); image=ImageGrab.grabclipboard(); assert isinstance(image,Image.Image); return image.convert('RGBA')
def same(first,second):return first.size==second.size and first.tobytes()==second.tobytes()
def count(value):assert any(f'주석 {value}개' in c.window_text() for c in ui.descendants(control_type='Text'))
def point(x,y):
    r=main.rectangle(); left=ui.child_window(control_type='List').rectangle().right+100
    screen=ImageGrab.grab(bbox=(left,r.top,r.right-12,r.bottom-12)).convert('RGB'); mask=Image.new('L',screen.size)
    mask.putdata([255 if pixel==(255,0,255) else 0 for pixel in screen.get_flattened_data()]); b=mask.getbbox(); assert b
    return round(left+b[0]+x*(b[2]-b[0])/800),round(r.top+b[1]+y*(b[3]-b[1])/500)
def draw(x,y,xx,yy):
    start=point(x,y);end=point(xx,yy);mouse.press(coords=start)
    for i in range(1,11):mouse.move(coords=(round(start[0]+(end[0]-start[0])*i/10),round(start[1]+(end[1]-start[1])*i/10)));time.sleep(.02)
    mouse.release(coords=end);time.sleep(.2)
def passed(name):results.append({'scenario':name,'passed':True})

before=copied(); key('r');draw(100,100,300,200);count(1);box=copied();assert not same(before,box);passed('R 박스 생성·출력 반영')
key('n');mouse.click(coords=point(400,100));time.sleep(.2);count(2);number=copied();assert not same(number,box);passed('N 번호 생성·출력 반영')
key('m');draw(400,220,600,320);count(3);mosaic=copied();assert not same(mosaic,number);passed('M 모자이크 생성·출력 반영')
key('v');mouse.click(coords=point(400,100));time.sleep(.2);key('{RIGHT}');moved=copied();assert not same(moved,mosaic)
key('^z');assert same(copied(),mosaic);key('^+z');assert same(copied(),moved);passed('V 선택·방향키 1px 이동·Ctrl+Z/Ctrl+Shift+Z')
key('+{DOWN}');shifted=copied();assert not same(shifted,moved);key('^z');assert same(copied(),moved);key('^y');assert same(copied(),shifted);passed('Shift+방향키 10px·Ctrl+Y')
key('{DELETE}');count(2);deleted=copied();assert not same(deleted,shifted);key('^z');count(3);assert same(copied(),shifted);passed('Delete 주석 삭제·Undo 복원')
before_files=set(folder.glob('Clip-*.png'));key('^s');new=set(folder.glob('Clip-*.png'))-before_files;assert len(new)==1
assert same(Image.open(new.pop()).convert('RGBA'),copied());assert hashlib.sha256(source.read_bytes()).hexdigest()==original_hash;passed('Ctrl+S/Ctrl+C 픽셀 일치·원본 보존')
Path(a.report).write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(results,ensure_ascii=False,indent=2))
