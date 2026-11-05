"""통합 제목 표시줄의 실제 클릭·창 이동·크기·트레이 동작 검사."""
import argparse,ctypes,json,subprocess,time
from pathlib import Path
import win32api,win32con,win32gui
from pywinauto import Application,keyboard,mouse
from PIL import ImageGrab
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
p=argparse.ArgumentParser();p.add_argument('--pid',type=int,required=True);p.add_argument('--exe',required=True);p.add_argument('--data',required=True);p.add_argument('--report',required=True);a=p.parse_args()
native=Application(backend='win32').connect(process=a.pid);ui=Application(backend='uia').connect(process=a.pid);results=[]
def main():return next(w for w in native.windows() if w.window_text()=='Clip' and w.is_visible())
def button(name):return ui.window(handle=main().handle).child_window(title=name,control_type='Button').wrapper_object()
def click(name):button(name).click_input();time.sleep(.3)
def drag(start,end):
    main().set_focus();time.sleep(.15)
    assert win32gui.WindowFromPoint(start)==main().handle,'드래그 지점이 다른 창에 가려짐'
    mouse.press(coords=start)
    try:
        for i in range(1,11):mouse.move(coords=(round(start[0]+(end[0]-start[0])*i/10),round(start[1]+(end[1]-start[1])*i/10)));time.sleep(.03)
    finally:mouse.release(coords=end)
def shot(name):
    w=main();w.set_focus();time.sleep(.2);r=w.rectangle();ImageGrab.grab(bbox=(r.left,r.top,r.right,r.bottom)).save('artifacts/ui/screens/'+name)
w=main();w.set_focus();scale=ctypes.windll.user32.GetDpiForWindow(w.handle)/96
w.move_window(400,100,int(1220*scale),int(800*scale));time.sleep(.3);r=w.rectangle()
click('설정');settings=next(w for w in native.windows() if w.window_text()=='Clip · 설정' and w.is_visible())
ui.window(handle=settings.handle).child_window(title='취소',control_type='Button').click_input();time.sleep(.2)
results.append({'scenario':'제목 영역의 앱 버튼 실제 클릭','passed':True})
w=main();r=w.rectangle();start=(r.left+int(140*scale),r.top+int(32*scale));end=(start[0]+110,start[1]+70)
drag(start,end);time.sleep(.3);moved=w.rectangle()
assert abs(moved.left-r.left-110)<4 and abs(moved.top-r.top-70)<4,(r,moved)
results.append({'scenario':'제목 빈 영역 드래그로 창 이동','passed':True})
click('최대화');assert ctypes.windll.user32.IsZoomed(w.handle)
work=win32api.GetMonitorInfo(win32api.MonitorFromWindow(w.handle))['Work'];close=button('닫기').rectangle()
assert close.right<=work[2]+1 and close.top>=work[1],(close,work)
shot('35-maximized-0.5.1.png');click('이전 크기로');assert not ctypes.windll.user32.IsZoomed(w.handle)
r=w.rectangle();point=(r.left+int(140*scale),r.top+int(32*scale));mouse.double_click(coords=point);time.sleep(.3);assert ctypes.windll.user32.IsZoomed(w.handle)
click('이전 크기로');results.append({'scenario':'최대화·복원·제목 두 번 클릭·작업 영역 유지','passed':True})
click('최소화');assert ctypes.windll.user32.IsIconic(w.handle);win32gui.ShowWindow(w.handle,win32con.SW_RESTORE);w.set_focus();time.sleep(.3)
results.append({'scenario':'최소화·복원','passed':True})
# 크기 조절 테두리가 실제 비클라이언트 히트 테스트에 응답해야 한다.
r=w.rectangle();x,y=r.right-8,r.bottom-8
hit=win32gui.SendMessage(w.handle,win32con.WM_NCHITTEST,0,(y<<16)|(x&65535));assert hit==win32con.HTBOTTOMRIGHT,hit
drag((x,y),(x-80,y-50));time.sleep(.3)
assert w.rectangle().width()<r.width()-50,(str(r),str(w.rectangle()),win32gui.GetCursorPos())
w.move_window(500,200,int(960*scale),int(640*scale));time.sleep(.4)
for name in ['설정','새 캡처','최소화','최대화','닫기','복사 · Ctrl+C','PNG 저장 · Ctrl+S']:
    rect=button(name).rectangle();bounds=w.rectangle();assert rect.left>=bounds.left and rect.right<=bounds.right and rect.bottom<=bounds.bottom,name
shot('36-minimum-0.5.1.png');results.append({'scenario':'테두리 크기 조절·최소 창 주요 버튼 유지','passed':True})
w.move_window(700,200,int(1220*scale),int(800*scale));time.sleep(.3);shot('34-editor-0.5.1.png')
click('닫기');time.sleep(.2);assert not win32gui.IsWindowVisible(w.handle)
other=subprocess.Popen([str(Path(a.exe).resolve()),'--data-dir',str(Path(a.data).resolve())]);assert other.wait(timeout=10)==0
time.sleep(.5);assert main().process_id()==a.pid
results.append({'scenario':'닫기 후 트레이 유지·재실행으로 기존 창 복귀','passed':True})
Path(a.report).write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8');print('PASS',len(results))
