"""자동 실행 설정을 실제 UI로 검사하고 원래 Run 항목을 반드시 복원한다."""
import argparse,ctypes,json,os,subprocess,sys,time,winreg
import win32file,win32con
from pathlib import Path
from pywinauto import Application,keyboard
from PIL import ImageGrab
sys.stdout.reconfigure(encoding='utf-8');ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
p=argparse.ArgumentParser();p.add_argument('--pid',type=int,required=True);p.add_argument('--exe',required=True);p.add_argument('--data',required=True);p.add_argument('--report',required=True);a=p.parse_args()
keypath=r'Software\Microsoft\Windows\CurrentVersion\Run';name='Clip.Capture';data=Path(a.data).resolve();exe=Path(a.exe).resolve();results=[];active_pid=a.pid
def entry():
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,keypath) as key:
        try:return winreg.QueryValueEx(key,name)
        except FileNotFoundError:return None
original=entry();Path('artifacts/ui/startup-original-0.5.json').write_text(json.dumps(original),encoding='utf-8')
def restore():
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,keypath,0,winreg.KEY_SET_VALUE) as key:
        if original is None:
            try:winreg.DeleteValue(key,name)
            except FileNotFoundError:pass
        else:winreg.SetValueEx(key,name,0,original[1],original[0])
def native():return Application(backend='win32').connect(process=active_pid)
def window(title):
    deadline=time.monotonic()+8
    while time.monotonic()<deadline:
        found=[w for w in native().windows() if w.window_text()==title and w.is_visible()]
        if found:return found[0]
        time.sleep(.1)
    raise RuntimeError('창 대기 실패: '+title)
def u(w):return Application(backend='uia').connect(process=active_pid).window(handle=w.handle)
def button(w,title):u(w).child_window(title=title,control_type='Button').invoke();time.sleep(.2)
def settings():
    button(window('Clip'),'설정');return window('Clip · 설정')
def toggle(w,desired):
    c=u(w).child_window(control_type='CheckBox').wrapper_object()
    if bool(c.get_toggle_state())!=desired:c.toggle()
def close_app():
    subprocess.run([sys.executable,'scripts/qa-close-clip.py',str(active_pid)],check=True)
try:
    w=settings();initial=entry();toggle(w,True);button(w,'취소');assert entry()==initial
    results.append({'scenario':'자동 실행 설정 취소는 등록을 바꾸지 않음','passed':True})
    w=settings();toggle(w,True);button(w,'저장');registered=entry();assert registered and str(exe) in registered[0] and '--startup' in registered[0] and str(data) in registered[0]
    results.append({'scenario':'현재 사용자 Run에 정확한 EXE·데이터 경로 등록','passed':True})
    w=settings();assert u(w).child_window(control_type='CheckBox').get_toggle_state()==1
    r=w.rectangle();ImageGrab.grab(bbox=(r.left+10,r.top+10,r.right-10,r.bottom-10)).save('artifacts/ui/screens/28-settings-0.5.png')
    folder=u(w).child_window(control_type='Edit');old_folder=folder.window_text();folder.set_edit_text('relative-folder');toggle(w,False);button(w,'저장');assert entry()==registered;assert any('전체 폴더 경로' in c.window_text() for c in u(w).descendants(control_type='Text'))
    r=w.rectangle();ImageGrab.grab(bbox=(r.left+10,r.top+10,r.right-10,r.bottom-10)).save('artifacts/ui/screens/29-settings-error-0.5.png')
    button(w,'취소');results.append({'scenario':'잘못된 저장 폴더는 자동 실행 변경도 적용하지 않음','passed':True})
    close_app()
    env=os.environ.copy();env['DOTNET_ROOT']=str(Path('.tools/dotnet').resolve())
    proc=subprocess.Popen(registered[0],env=env);active_pid=proc.pid;time.sleep(1)
    assert proc.poll() is None;assert not [w for w in native().windows() if w.is_visible() and w.window_text().startswith('Clip')]
    results.append({'scenario':'등록된 명령줄 실행 시 창 없이 트레이 대기','passed':True})
    duplicate=subprocess.Popen([str(exe),'--data-dir',str(data)],env=env);assert duplicate.wait(timeout=10)==0;window('Clip')
    results.append({'scenario':'일반 재실행은 기존 트레이 인스턴스 표시','passed':True})
    main=window('Clip');main.close();time.sleep(.3);assert proc.poll() is None;assert not main.is_visible()
    duplicate=subprocess.Popen([str(exe),'--data-dir',str(data)],env=env);assert duplicate.wait(timeout=10)==0;window('Clip')
    results.append({'scenario':'X 닫기 후 트레이 유지·재실행 복귀','passed':True})
    w=settings();toggle(w,False);button(w,'저장');assert entry() is None
    results.append({'scenario':'설정 끄기로 현재 복사본 Run 항목 제거','passed':True})
    w=settings();toggle(w,True)
    lock=win32file.CreateFile(str(data/'settings.json'),win32con.GENERIC_READ,win32con.FILE_SHARE_READ,None,win32con.OPEN_EXISTING,0,None)
    try:
        button(w,'저장');assert entry() is None
        assert any('설정 저장 실패' in c.window_text() for c in u(w).descendants(control_type='Text'))
    finally:win32file.CloseHandle(lock)
    button(w,'취소');results.append({'scenario':'설정 파일 교체 실패 시 자동 실행 등록 롤백','passed':True})
    close_app();active_pid=0
finally:
    restore();assert entry()==original
    Path(a.report).write_text(json.dumps({'scenarios':results,'original_registration_restored':True,'remaining_pid':active_pid},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(results,ensure_ascii=False,indent=2))
