"""격리된 qa-pattern/qa-long MP4로 GIF 내보내기 UI를 검증한다."""
import argparse,ctypes,hashlib,json,sys,time,subprocess
from pathlib import Path
from pywinauto import Application,keyboard
from PIL import Image
sys.stdout.reconfigure(encoding='utf-8')
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
p=argparse.ArgumentParser();p.add_argument('--pid',type=int,required=True);p.add_argument('--data',required=True);p.add_argument('--report',default='artifacts/ui/gif-ui-results.json');a=p.parse_args()
native=Application(backend='win32').connect(process=a.pid);ui=Application(backend='uia').connect(process=a.pid)
folder=Path(a.data)/'captures';before_hash={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.glob('*.mp4')};results=[]
def wait_value(getter,predicate,seconds=20):
    until=time.monotonic()+seconds
    while time.monotonic()<until:
        value=getter()
        if predicate(value):return value
        time.sleep(.1)
    raise RuntimeError(str(value))
def visible(title):return [w for w in native.windows() if w.is_visible() and w.window_text()==title]
def window(title):return wait_value(lambda:visible(title),bool)[0]
def u(w):return ui.window(handle=w.handle)
def texts(w):return [c.window_text() for c in u(w).descendants(control_type='Text')]
def button(w,title):
    matches=[c for c in u(w).descendants(control_type='Button') if c.window_text()==title and c.rectangle().top>w.rectangle().top+90]
    assert len(matches)==1,(title,len(matches));return matches[0]
def click(w,title):button(w,title).invoke()
def key(w,keys):w.set_focus();keyboard.send_keys(keys,pause=.04,vk_packet=False);time.sleep(.15)
def gif_files():return set(folder.glob('Clip-*.gif'))
def edits(w,first,last,width,fps='15'):
    fields=u(w).descendants(control_type='Edit');assert len(fields)==3
    for field,value in zip(fields,[first,last,width]):field.set_edit_text(str(value))
    u(w).descendants(control_type='ComboBox')[0].select(fps)
def open_video(name):
    main=window('Clip');item=next(c for c in u(main).descendants(control_type='ListItem') if name in c.window_text());item.select()
    player=window('Clip · '+name)
    wait_value(lambda:button(player,'GIF 내보내기').is_enabled(),bool);return player
def export_dialog(player):
    click(player,'GIF 내보내기');w=window('Clip · GIF 내보내기');wait_value(lambda:button(w,'GIF 저장').is_enabled(),bool);return w
def wait_done(w,needle):return wait_value(lambda:texts(w),lambda rows:any(needle in text for text in rows),60)
def start_long(w):
    edits(w,0,30,1280,'30');click(w,'GIF 저장');wait_value(lambda:button(w,'변환 취소').is_enabled(),bool)
    wait_value(lambda:u(w).descendants(control_type='ProgressBar')[0].iface_range_value.CurrentValue,lambda n:n>0)
def shot(w,name):
    path=Path('artifacts/ui/screens')/name;w.capture_as_image().save(path)

if visible('Clip · GIF 내보내기'):
    dialog=window('Clip · GIF 내보내기');player=window('Clip · qa-pattern.mp4')
else:
    player=open_video('qa-pattern.mp4');dialog=export_dialog(player)
edits(dialog,3,1,160);assert not button(dialog,'GIF 저장').is_enabled();results.append({'scenario':'잘못된 구간 저장 차단','passed':True})
edits(dialog,1,3,160);before=gif_files();click(dialog,'GIF 저장');status=wait_done(dialog,'GIF 저장 완료');new=gif_files()-before;assert len(new)==1;gif=new.pop()
im=Image.open(gif);assert im.size==(160,120) and im.n_frames==30 and im.info.get('loop')==0
total=0
for i in range(im.n_frames):im.seek(i);total+=im.info['duration']
assert total==2000;im.close();shot(dialog,'21-gif-saved.png')
results.append({'scenario':'구간·크기·fps 지정 저장','passed':True,'file':str(gif),'frames':30,'duration_ms':total,'status':status})
click(dialog,'GIF 보기');viewer=window('Clip GIF · '+gif.name);first=texts(viewer);time.sleep(.35);second=texts(viewer);assert first!=second
click(viewer,'일시 정지');time.sleep(.1);paused=texts(viewer);time.sleep(.2);assert paused==texts(viewer);shot(viewer,'22-gif-preview.png');key(viewer,'%{F4}')
results.append({'scenario':'GIF 반복 미리보기·일시 정지','passed':True,'first':first,'second':second})
click(dialog,'닫기');window('Clip · qa-pattern.mp4');key(player,'%{F4}');main=window('Clip')
next(c for c in u(main).descendants(control_type='ListItem') if gif.name in c.window_text()).select();viewer=window('Clip GIF · '+gif.name);key(viewer,'%{F4}');results.append({'scenario':'라이브러리 GIF 재열기','passed':True})
player=open_video('qa-long.mp4');dialog=export_dialog(player);before=gif_files();start_long(dialog);click(dialog,'변환 취소');wait_done(dialog,'변환을 취소했습니다');assert gif_files()==before and not list(folder.glob('.clip-*.tmp'))
results.append({'scenario':'변환 중 취소·미완료 파일 정리','passed':True})
start_long(dialog);key(dialog,'%{F4}');window('Clip · qa-long.mp4');assert not visible('Clip · GIF 내보내기');assert gif_files()==before and not list(folder.glob('.clip-*.tmp'))
results.append({'scenario':'변환 중 창 닫기·정리 후 복귀','passed':True})
dialog=export_dialog(player);start_long(dialog)
subprocess.run([sys.executable,'scripts/qa-close-clip.py',str(a.pid)],check=True)
assert gif_files()==before and not list(folder.glob('.clip-*.tmp'));assert before_hash=={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.glob('*.mp4')}
results.append({'scenario':'변환 중 앱 종료·원본 보존','passed':True})
Path(a.report).write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(results,ensure_ascii=False,indent=2))
