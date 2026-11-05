"""합성 fixture가 열린 상태에서 Clip 녹화의 실제 UI를 검증한다. 제품 의존성 아님."""
import argparse, ctypes, json, sys, time
from pathlib import Path
import win32api
from pywinauto import Application, keyboard

sys.stdout.reconfigure(encoding='utf-8')
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
p=argparse.ArgumentParser();p.add_argument('--pid',type=int,required=True);p.add_argument('--data',required=True);p.add_argument('--report',default='artifacts/ui/recording-ui-results.json');a=p.parse_args()
native=Application(backend='win32').connect(process=a.pid)
ui=Application(backend='uia').connect(process=a.pid)
folder=Path(a.data)/'captures';folder.mkdir(parents=True,exist_ok=True)
results=[]

def wait_window(title,timeout=10):
    until=time.monotonic()+timeout
    while time.monotonic()<until:
        for w in native.windows():
            if w.window_text()==title and w.is_visible():return w
        time.sleep(.1)
    raise RuntimeError('창 대기 실패: '+title)

def key(w,keys):
    w.set_focus();keyboard.send_keys(keys,pause=.06,vk_packet=False);time.sleep(.2)

def click(w,text):
    u=ui.window(handle=w.handle)
    u.child_window(title=text,control_type='Button').invoke();time.sleep(.2)

def drag(w):
    w.set_focus();win32api.SetCursorPos((121,195));win32api.mouse_event(2,0,0,0,0);time.sleep(.1)
    for i in range(1,11):win32api.SetCursorPos((121+80*i,195+45*i));time.sleep(.02)
    win32api.mouse_event(4,0,0,0,0);time.sleep(.1)

def prepare():
    key(wait_window('Clip'),'^+9');w=wait_window('Clip 영역 선택');drag(w);key(w,'{ENTER}');return wait_window('Clip 녹화')

def videos():return {str(p) for p in folder.glob('Clip-*.mp4')}

# 영역 선택 취소와 녹화 준비 취소는 파일을 만들지 않는다.
before=videos();key(wait_window('Clip'),'^+9');key(wait_window('Clip 영역 선택'),'{ESC}');wait_window('Clip')
assert videos()==before
w=prepare();key(w,'%{F4}');wait_window('Clip');assert videos()==before
results.append({'scenario':'선택·준비 취소','passed':True})

# 시작/정지 키, 닫기 저장, 즉시 정지를 반복한다.
for index in range(4):
    before=videos();w=prepare();start=time.monotonic()
    key(w,'^+9')
    time.sleep(2 if index<2 else .15)
    if index==1:key(w,'%{F4}')
    else:key(w,'^+9')
    wait_window('Clip',30);new=videos()-before;assert len(new)==1,new
    assert not list(folder.glob('.clip-recording-*.partial.mp4'))
    results.append({'scenario':['단축키 시작·정지','닫기 정지·저장','짧은 녹화','반복 녹화'][index],'passed':True,'wall_seconds':round(time.monotonic()-start,3),'file':new.pop()})

# 영상 선택 알림이 반환되고 재생·일시정지·다시재생이 모두 호출 가능해야 한다.
main=wait_window('Clip');items=ui.window(handle=main.handle).descendants(control_type='ListItem')
next(i for i in items if '.mp4' in i.window_text()).select();time.sleep(.5)
player=next(w for w in native.windows() if w.window_text().startswith('Clip · Clip-') and '.mp4' in w.window_text())
until=time.monotonic()+10
while time.monotonic()<until:
    texts=[c.window_text() for c in ui.window(handle=player.handle).descendants(control_type='Text')]
    if any('800 × 450px' in text for text in texts):break
    time.sleep(.2)
assert any('800 × 450px' in text for text in texts),texts
click(player,'일시 정지');click(player,'처음부터');time.sleep(.4);click(player,'일시 정지')
results.append({'scenario':'라이브러리 재생·일시정지·처음부터','passed':True,'text':texts})
key(player,'%{F4}');wait_window('Clip')

# 영상 구현 후 기존 이미지 캡처·저장 회귀.
key(wait_window('Clip'),'^+2');w=wait_window('Clip 영역 선택');drag(w)
main=wait_window('Clip · 저장 전');before_png=set(folder.glob('*.png'));key(main,'^s');time.sleep(.3)
new_png=set(folder.glob('*.png'))-before_png;assert len(new_png)==1
results.append({'scenario':'이미지 캡처·PNG 저장 회귀','passed':True,'file':str(new_png.pop())})
path=Path(a.report);path.write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(results,ensure_ascii=False,indent=2))
