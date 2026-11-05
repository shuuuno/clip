"""검증용 Clip의 실제 트레이 종료 메뉴를 사용한다."""
import sys,time,json,ctypes
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
from pywinauto import Application,Desktop
import win32api
sys.stdout.reconfigure(encoding='utf-8');pid=int(sys.argv[1]);desktop=Desktop(backend='uia')
def icons():
    result=[]
    for name in ['Shell_TrayWnd','TopLevelWindowForOverflowXamlIsland']:
        for root in desktop.windows(class_name=name):
            result.extend((root,c) for c in root.descendants(control_type='Button') if c.is_visible() and c.window_text().startswith('Clip ·'))
    return result
found=icons()
for attempt in range(3):
    if found:break
    tray=desktop.window(class_name='Shell_TrayWnd');buttons=[c for c in tray.descendants(control_type='Button') if '숨겨진 아이콘' in c.window_text()]
    assert len(buttons)==1,[c.window_text() for c in buttons];buttons[0].invoke()
    deadline=time.monotonic()+1
    while time.monotonic()<deadline and not found:time.sleep(.1);found=icons()
assert len(found)==1,[c.window_text() for _,c in found]
root,icon=found[0];root.set_focus();time.sleep(.15);r=icon.rectangle();win32api.SetCursorPos(((r.left+r.right)//2,(r.top+r.bottom)//2));win32api.mouse_event(8,0,0,0,0);win32api.mouse_event(16,0,0,0,0)
app=Application(backend='uia').connect(process=pid)
deadline=time.monotonic()+5
while time.monotonic()<deadline:
    menus=[w for w in app.windows() if w.element_info.control_type=='Menu']
    entries=[c for menu in menus for c in menu.descendants() if c.window_text()=='Clip 종료']
    if entries:
        entries[0].invoke();break
    time.sleep(.1)
else:raise RuntimeError('Clip 트레이 종료 메뉴 대기 실패')
app.wait_for_process_exit(timeout=30);print(json.dumps({'pid':pid,'exited':True}))
