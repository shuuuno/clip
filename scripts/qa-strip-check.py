"""합성 PNG로 Clip 띠 제거를 실제 UI에서 검증한다. 제품 의존성 아님."""
import argparse,ctypes,hashlib,json,re,sys,time
from pathlib import Path
from PIL import Image,ImageGrab
from pywinauto import Application,keyboard
import win32api

sys.stdout.reconfigure(encoding='utf-8')
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
p=argparse.ArgumentParser();p.add_argument('--pid',type=int,required=True);p.add_argument('--data',required=True);p.add_argument('--report',required=True);a=p.parse_args()
native=Application(backend='win32').connect(process=a.pid)
main=next(w for w in native.windows() if w.window_text()=='Clip')
main.maximize();time.sleep(.3)
ui=Application(backend='uia').connect(process=a.pid).window(handle=main.handle)
folder=Path(a.data)/'captures';original=folder/'qa-strip.png';source=Image.open(original).convert('RGBA');source_hash=hashlib.sha256(original.read_bytes()).hexdigest();results=[]
def key(keys):
    assert main.is_enabled(),[(w.window_text(),w.is_visible()) for w in native.windows()]
    main.set_focus();keyboard.send_keys(keys,pause=.04,vk_packet=False);time.sleep(.15)
def button(name):return ui.child_window(title=name,control_type='Button').wrapper_object()
def click(name):button(name).invoke();time.sleep(.15)
def texts():return [c.window_text() for c in ui.descendants(control_type='Text')]
def dimensions(w,h):assert any(t.startswith(f'{w} × {h} px') for t in texts()),texts()
def copied():
    key('^c');im=ImageGrab.grabclipboard();assert isinstance(im,Image.Image);return im.convert('RGBA')
def same(actual,expected):assert actual.size==expected.size and actual.tobytes()==expected.tobytes(),(actual.size,expected.size)
def bounds():
    main.set_focus();time.sleep(.1);r=main.rectangle();sidebar=ui.child_window(control_type='List').rectangle()
    left=sidebar.right+10;top=r.top;im=ImageGrab.grab(bbox=(left,top,r.right-12,r.bottom-12)).convert('RGB')
    mask=Image.new('L',im.size);mask.putdata([255 if red==255 and green==0 and blue==255 else 0 for red,green,blue in im.get_flattened_data()])
    b=mask.getbbox();assert b is not None,'합성 PNG 경계 미검출'
    return left+b[0],top+b[1],b[2]-b[0],b[3]-b[1]
def drag(x1,y1,x2,y2,w=800,h=500):
    l,t,pw,ph=bounds();start=(round(l+x1*pw/w),round(t+y1*ph/h));end=(round(l+x2*pw/w),round(t+y2*ph/h))
    main.set_focus();win32api.SetCursorPos(start);win32api.mouse_event(2,0,0,0,0);time.sleep(.06)
    for i in range(1,11):win32api.SetCursorPos((round(start[0]+(end[0]-start[0])*i/10),round(start[1]+(end[1]-start[1])*i/10)));time.sleep(.02)
    win32api.mouse_event(4,0,0,0,0);time.sleep(.15)
def save(expected):
    before=set(folder.glob('Clip-*.png'));key('^s');new=set(folder.glob('Clip-*.png'))-before;assert len(new)==1
    file=new.pop();same(Image.open(file).convert('RGBA'),expected);same(copied(),expected);return str(file)
def cut(im,horizontal,start,length):
    w,h=im.size;out=Image.new('RGBA',(w if horizontal else w-length,h-length if horizontal else h))
    if horizontal:
        out.paste(im.crop((0,0,w,start)),(0,0));out.paste(im.crop((0,start+length,w,h)),(0,start))
    else:
        out.paste(im.crop((0,0,start,h)),(0,0));out.paste(im.crop((start+length,0,w,h)),(start,0))
    return out
def shot(name):
    main.set_focus();time.sleep(.15);r=main.rectangle();ImageGrab.grab(bbox=(r.left+12,r.top+12,r.right-12,r.bottom-12)).save(Path('artifacts/ui/screens')/name)

next(i for i in ui.descendants(control_type='ListItem') if 'qa-strip.png' in i.window_text()).select();time.sleep(.3)
next(c for c in ui.descendants(control_type='ComboBox') if '화면 맞춤' in c.texts()).select('100%');time.sleep(.2)
click('가로 띠 제거  H');drag(350,200,350,100);assert button('띠 제거 적용 · Enter').is_enabled();assert any('→   800 × 400 px' in t for t in texts()),texts()
shot('23-strip-selection.png');key('{ESC}');dimensions(800,500);same(copied(),source);assert not button('띠 제거 적용 · Enter').is_enabled()
results.append({'scenario':'역방향 드래그·결과 크기·Esc 취소','passed':True})
drag(350,100,350,200);key('{ENTER}');expected=cut(source,True,100,100);dimensions(800,400);file=save(expected)
key('^z');dimensions(800,500);same(copied(),source);key('^+z');same(copied(),expected)
results.append({'scenario':'가로 띠 100px 제거·PNG/클립보드·Undo/Redo','passed':True,'file':file})
key('^z');key('j');drag(380,250,300,250);click('띠 제거 적용 · Enter');expected=cut(source,False,300,80);dimensions(720,500);file=save(expected)
results.append({'scenario':'세로 띠 80px 제거·버튼 적용·PNG/클립보드','passed':True,'file':file})
key('^z');key('h');drag(350,0,350,500);assert not button('띠 제거 적용 · Enter').is_enabled();key('{ENTER}');dimensions(800,500);key('{ESC}')
drag(350,100,350,100);assert not button('띠 제거 적용 · Enter').is_enabled();key('{ESC}');assert main.is_visible() and main.is_enabled()
results.append({'scenario':'전체 이미지·길이 0 선택 차단','passed':True})
key('n');drag(100,50,100,50);assert any('주석 1개' in t for t in texts());key('h');drag(350,100,350,200);key('^z');assert not button('띠 제거 적용 · Enter').is_enabled();key('^+z')
annotated=copied();drag(350,100,350,200);key('{ENTER}');assert any('주석 0개' in t for t in texts());file=save(cut(annotated,True,100,100));key('^z');same(copied(),annotated);assert any('주석 1개' in t for t in texts());key('^+z')
results.append({'scenario':'주석 합성·주석 복원·Undo 시 미완료 선택 정리','passed':True,'file':file})
key('h');drag(350,50,350,80,w=800,h=400);key('j');assert not button('띠 제거 적용 · Enter').is_enabled();key('{ENTER}');dimensions(800,400)
results.append({'scenario':'도구 전환 시 미완료 선택 취소','passed':True})
shot('24-strip-result.png')
# 최소 창 크기에서도 주요 조작이 창 내부에 있고 서로 겹치지 않아야 한다.
dpi=ctypes.windll.user32.GetDpiForWindow(main.handle);scale=dpi/96;main.restore();main.move_window(width=round(960*scale),height=round(640*scale));time.sleep(.4)
controls=[button(n) for n in ['가로 띠 제거  H','세로 띠 제거  J','띠 제거 적용 · Enter','복사 · Ctrl+C','PNG 저장 · Ctrl+S']]
r=main.rectangle()
for c in controls:
    b=c.rectangle();assert c.is_visible() and r.left<=b.left<b.right<=r.right and r.top<=b.top<b.bottom<=r.bottom,(c.window_text(),b,r)
for first,second in zip(controls,controls[1:]):
    x,y=first.rectangle(),second.rectangle();assert min(x.right,y.right)<=max(x.left,y.left) or min(x.bottom,y.bottom)<=max(x.top,y.top)
next(c for c in ui.descendants(control_type='ComboBox') if '100%' in c.texts()).select('50%');click('가로 띠 제거  H');drag(350,50,350,80,w=800,h=400)
assert button('띠 제거 적용 · Enter').is_enabled()
preview=next(t for t in texts() if t.startswith('800 × 400 px'));match=re.search(r'→\s+800 × (\d+) px',preview);assert match,preview
# 50% × 175%에서는 화면 1px이 이미지 약 1.14px이므로 드래그 위치 반올림을 허용한다.
preview_height=int(match.group(1));assert 368<=preview_height<=372,preview
shot('25-strip-minimum-window.png');key('{ENTER}');dimensions(800,preview_height);assert copied().size==(800,preview_height);key('^z')
results.append({'scenario':'최소 창 960×640 DIP·50% 선택·예고 크기와 실제 출력 일치','passed':True,'dpi':dpi,'preview_height':preview_height})
assert hashlib.sha256(original.read_bytes()).hexdigest()==source_hash
Path(a.report).write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(results,ensure_ascii=False,indent=2))
