"""실제 Clip 창 검증용 도구. pywinauto/Pillow가 있는 분석 환경에서 사용한다."""
import argparse,ctypes,json,time,sys
from pathlib import Path
from pywinauto import Application,keyboard,mouse
import win32api
from PIL import ImageGrab
sys.stdout.reconfigure(encoding='utf-8')
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
p=argparse.ArgumentParser();p.add_argument('action',choices=['list','show','shot','click','drag','key','named','close']);p.add_argument('--pid',type=int,required=True);p.add_argument('--hwnd',type=int);p.add_argument('--name',default='state');p.add_argument('--text');p.add_argument('--keys');p.add_argument('--x',type=int);p.add_argument('--y',type=int);p.add_argument('--tx',type=int);p.add_argument('--ty',type=int);a=p.parse_args()
root=Path(__file__).resolve().parents[1]/'artifacts'/'ui';root.mkdir(parents=True,exist_ok=True)
app=Application(backend='win32').connect(process=a.pid)
def rect(r):return [r.left,r.top,r.right,r.bottom]
def windows():
    result=[{'hwnd':w.handle,'title':w.window_text(),'visible':w.is_visible(),'rect':rect(w.rectangle())} for w in app.windows(visible_only=False) if w.window_text().startswith('Clip')]
    print(json.dumps(result,ensure_ascii=False));return result
if a.action=='list':windows();raise SystemExit
w=app.window(handle=a.hwnd).wrapper_object()
if w.process_id()!=a.pid:raise RuntimeError('PID mismatch')
if a.action=='show':w.restore()
def focus():
    if ctypes.windll.user32.GetForegroundWindow()!=a.hwnd:
        try:w.set_focus()
        except Exception:
            r=w.rectangle();mouse.click(coords=(r.left+60,r.top+20));time.sleep(.15)
    if ctypes.windll.user32.GetForegroundWindow()!=a.hwnd:raise RuntimeError('대상 창 포커스 확인 실패')
if a.action=='named':
    u=Application(backend='uia').connect(process=a.pid).window(handle=a.hwnd)
    candidates=[c for c in u.descendants() if c.window_text()==a.text and c.is_enabled() and c.is_visible() and c.element_info.control_type in ['Button','MenuItem','CheckBox','TabItem']]
    if len(candidates)!=1:raise RuntimeError(f'matches={len(candidates)}')
    try:candidates[0].invoke()
    except Exception:candidates[0].click_input()
elif a.action=='key':focus();keyboard.send_keys(a.keys,pause=.05,vk_packet=not a.keys.isascii())
elif a.action=='click':
    focus();r=w.rectangle();mouse.click(coords=(r.left+a.x,r.top+a.y))
elif a.action=='drag':
    focus();r=w.rectangle();win32api.SetCursorPos((r.left+a.x,r.top+a.y));win32api.mouse_event(2,0,0,0,0)
    actual_start=win32api.GetCursorPos()
    time.sleep(.08)
    for i in range(1,11):win32api.SetCursorPos((r.left+round(a.x+(a.tx-a.x)*i/10),r.top+round(a.y+(a.ty-a.y)*i/10)));time.sleep(.03)
    win32api.mouse_event(4,0,0,0,0)
    actual_end=win32api.GetCursorPos()
    event={'action':'drag','hwnd':a.hwnd,'requested':[[r.left+a.x,r.top+a.y],[r.left+a.tx,r.top+a.ty]],'actual':[actual_start,actual_end]}
    with (root/'actions.jsonl').open('a',encoding='utf-8') as out:out.write(json.dumps(event)+'\n')
    print(json.dumps(event))
elif a.action=='close':w.close()
if a.action=='shot':
    focus();time.sleep(.25);r=w.rectangle();path=root/'screens'/f'{a.name}.png'
    ImageGrab.grab(bbox=(r.left+12,r.top+12,r.right-12,r.bottom-12),all_screens=True).save(path)
    controls=[]
    u=Application(backend='uia').connect(process=a.pid).window(handle=a.hwnd)
    for c in u.descendants():
        if c.is_visible():controls.append({'text':c.window_text(),'type':c.element_info.control_type,'rect':rect(c.rectangle()),'enabled':c.is_enabled()})
    data={'hwnd':a.hwnd,'rect':rect(r),'screenshot':str(path),'controls':controls};(root/'screens'/f'{a.name}.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(data,ensure_ascii=False))
else:time.sleep(.4);windows()
