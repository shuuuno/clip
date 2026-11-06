"""격리 데이터로 UI 전후 화면과 상태를 검증한다. 기존 UI 검증 환경에서 실행한다."""
import argparse, ctypes, json, shutil, subprocess, sys, time
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageGrab
from pywinauto import Application, keyboard, mouse

sys.stdout.reconfigure(encoding='utf-8')
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
p = argparse.ArgumentParser()
p.add_argument('--exe', required=True)
p.add_argument('--output', required=True)
p.add_argument('--baseline', action='store_true')
a = p.parse_args()
root = Path(__file__).resolve().parents[1]
out = Path(a.output).resolve(); out.mkdir(parents=True, exist_ok=True)
data = out / 'data'; captures = data / 'captures'; captures.mkdir(parents=True, exist_ok=True)
assert not list(captures.iterdir()), '새 검증 폴더를 지정하세요.'
launch = [str(Path(a.exe).resolve())]
if Path(a.exe).name == 'Clip.Tests.exe': launch.append('--ui-review-app')
proc = subprocess.Popen(launch + ['--data-dir', str(data)])
native = Application(backend='win32').connect(process=proc.pid)
uia = Application(backend='uia').connect(process=proc.pid)
results = []
fixture = None

def wait(getter, predicate=bool, timeout=15):
    until = time.monotonic() + timeout
    while time.monotonic() < until:
        value = getter()
        if predicate(value): return value
        time.sleep(.1)
    raise AssertionError(str(value))

def window(title):
    return wait(lambda: [w for w in native.windows() if w.is_visible() and w.window_text() == title])[0]

def ui(w): return uia.window(handle=w.handle)
def button(w, name):
    matches = [c for c in ui(w).descendants(control_type='Button') if c.window_text() == name]
    if len(matches) > 1:
        matches = [c for c in matches if c.rectangle().top > w.rectangle().top + 90]
    assert len(matches) == 1, (name, len(matches))
    return matches[0]
def click(w, name): button(w, name).invoke(); time.sleep(.2)
def key(w, value): w.set_focus(); keyboard.send_keys(value, pause=.04, vk_packet=False); time.sleep(.2)
def texts(w): return [c.window_text() for c in ui(w).descendants(control_type='Text')]
def check(name, condition):
    assert condition, name
    results.append({'scenario': name, 'passed': True})

def shot(w, name):
    w.set_focus(); time.sleep(.25); r = w.rectangle()
    ImageGrab.grab(bbox=(r.left, r.top, r.right, r.bottom)).save(out / (name + '.png'))
    controls = []
    for c in ui(w).descendants():
        if c.is_visible():
            b = c.rectangle()
            controls.append({'text': c.window_text(), 'type': c.element_info.control_type,
                             'rect': [b.left, b.top, b.right, b.bottom], 'enabled': c.is_enabled()})
    (out / (name + '.json')).write_text(json.dumps(controls, ensure_ascii=False, indent=2), encoding='utf-8')

def inside(w, names):
    r = w.rectangle(); boxes = [button(w, n).rectangle() for n in names]
    for name, b in zip(names, boxes):
        assert r.left <= b.left < b.right <= r.right and r.top <= b.top < b.bottom <= r.bottom, (name, b, r)
    for i, x in enumerate(boxes):
        for y in boxes[i+1:]:
            assert min(x.right, y.right) <= max(x.left, y.left) or min(x.bottom, y.bottom) <= max(x.top, y.top), (x, y)

try:
    main = window('Clip'); dpi = ctypes.windll.user32.GetDpiForWindow(main.handle); scale = dpi / 96
    main.move_window(200, 80, round(1220*scale), round(800*scale)); shot(main, '01-empty')
    check('빈 편집기의 복사·저장 비활성', not button(main, '복사 · Ctrl+C').is_enabled() and not button(main, 'PNG 저장 · Ctrl+S').is_enabled())
    if not a.baseline:
        check('빈 편집기의 속성·줌 비활성', all(not c.is_enabled() for c in ui(main).descendants(control_type='ComboBox')))
    click(main, '설정'); settings = window('Clip · 설정'); shot(settings, '02-settings')
    edit = ui(settings).descendants(control_type='Edit')[0]
    edit.set_edit_text('relative-folder'); click(settings, '저장')
    check('상대 경로 저장 차단', any('전체 폴더 경로' in t for t in texts(settings)))
    shot(settings, '03-settings-error'); click(settings, '취소')
    if not a.baseline:
        click(main, '설정'); settings = window('Clip · 설정')
        long_folder = data / '별도 저장 폴더' / '긴 경로와 공백을 포함한 캡처 보관함'
        ui(settings).descendants(control_type='Edit')[0].set_edit_text(str(long_folder))
        click(settings, 'Ctrl+Shift+2'); key(settings, '^%5'); click(settings, '저장')
        saved_settings = json.loads((data/'settings.json').read_text(encoding='utf-8'))
        check('저장 폴더·변경 단축키 파일 반영', saved_settings['CaptureDirectory'] == str(long_folder) and saved_settings['Modifiers'] == 3 and saved_settings['KeyCode'] == 53)
        check('변경 단축키 화면 안내 갱신', any('Ctrl+Alt+5' in t for t in texts(main)))
        key(main, '^%5'); key(window('Clip 영역 선택'), '{ESC}')
        click(main, '설정'); settings = window('Clip · 설정')
        check('설정 다시 열기 값 유지', ui(settings).descendants(control_type='Edit')[0].window_text() == str(long_folder))
        shot(settings, '14-settings-persisted')
        ui(settings).descendants(control_type='Edit')[0].set_edit_text(str(captures))
        click(settings, 'Ctrl+Alt+5'); key(settings, '^+2'); click(settings, '저장')

    image = Image.new('RGB', (1000, 660), '#F8FAFC'); draw = ImageDraw.Draw(image)
    font = lambda n: ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', n)
    draw.text((48, 38), '화면 공유 체크리스트', font=font(32), fill='#182334')
    draw.text((48, 96), 'UI 검증용 합성 이미지', font=font(18), fill='#66758A')
    for i, text in enumerate(['필요한 부분에 번호와 박스 표시', '공유 전에 민감한 정보 가리기', '편집 결과를 복사하거나 PNG로 저장']):
        y = 190+i*130
        draw.line((48, y+74, 952, y+74), fill='#CCD4DF', width=2)
        draw.text((74, y), text, font=font(25), fill='#21314A')
    image.save(captures / '화면-공유-체크리스트.png')
    shutil.copyfile(root / 'artifacts/ui/gif-data/captures/qa-pattern.mp4', captures / 'qa-pattern.mp4')
    click(main, '새로 고침')
    next(c for c in ui(main).descendants(control_type='ListItem') if '체크리스트' in c.window_text()).select(); time.sleep(.3)
    main = window('Clip'); key(main, 'r'); shot(main, '04-editor')
    if not a.baseline:
        combos = {c.window_text(): c for c in ui(main).descendants(control_type='ComboBox')}
        check('박스 도구: 색·굵기 활성, 글자 비활성', combos['색'].is_enabled() and combos['굵기'].is_enabled() and not combos['글자'].is_enabled())
        for shortcut, name in [('v','선택  V'),('n','번호  N'),('r','박스  R'),('t','텍스트  T'),('m','모자이크  M'),('c','크롭  C'),('h','가로 띠 제거  H'),('j','세로 띠 제거  J')]:
            key(main, shortcut)
            check('도구 단축키 ' + shortcut.upper(), button(main, name).element_info.element.CurrentItemStatus == '선택됨')
    key(main, 't'); r = main.rectangle(); left = ui(main).child_window(control_type='List').rectangle().right+100
    screen = ImageGrab.grab(bbox=(left,r.top,r.right-12,r.bottom-12)).convert('RGB')
    mask = Image.new('L',screen.size); mask.putdata([255 if pixel==(248,250,252) else 0 for pixel in screen.get_flattened_data()]); bounds = mask.getbbox(); assert bounds
    mouse.click(coords=(left+bounds[0]+100,r.top+bounds[1]+150))
    text_dialog = window('Clip · 텍스트'); shot(text_dialog, '15-text-dialog')
    if not a.baseline:
        inside(text_dialog, ['취소','추가']); check('텍스트 입력창 완료 버튼 경계', True)
    ui(text_dialog).descendants(control_type='Edit')[0].set_edit_text('확인할 부분')
    key(text_dialog, '^{ENTER}'); main = window('Clip · 저장 전'); key(main, '^s'); check('한글 주석 Ctrl+Enter 추가·저장', any('주석 1개' in t for t in texts(main)))
    key(main, 'h'); main.move_window(width=round(960*scale), height=round(640*scale)); time.sleep(.3)
    inside(main, ['새 캡처','영역 녹화','설정','가로 띠 제거  H','세로 띠 제거  J','복사 · Ctrl+C','PNG 저장 · Ctrl+S'])
    check('최소 창 주요 버튼 경계·겹침', True); shot(main, '05-minimum')
    main.move_window(width=round(1220*scale), height=round(800*scale))
    key(main, 'v'); key(main, 'Tab'); shot(main, '06-focus')

    next(c for c in ui(main).descendants(control_type='ListItem') if 'qa-pattern.mp4' in c.window_text()).select()
    player = window('Clip · qa-pattern.mp4'); wait(lambda: button(player, 'GIF 내보내기').is_enabled())
    click(player, '일시 정지'); shot(player, '07-video')
    if not a.baseline:
        seek = ui(player).descendants(control_type='Slider')[0]; seek.set_value(1)
        seek.set_focus(); keyboard.send_keys('{RIGHT}'); time.sleep(.3)
        check('영상 탐색 슬라이더 조작', seek.iface_range_value.CurrentValue >= 1)
    click(player, 'GIF 내보내기'); gif = window('Clip · GIF 내보내기'); wait(lambda: button(gif, 'GIF 저장').is_enabled()); shot(gif, '08-gif-options')
    click(gif, 'GIF 저장'); wait(lambda: any('GIF 저장 완료' in t for t in texts(gif)), timeout=45); shot(gif, '09-gif-saved')
    click(gif, 'GIF 보기'); viewer = wait(lambda: [w for w in native.windows() if w.is_visible() and w.window_text().startswith('Clip GIF ·')])[0]
    click(viewer, '일시 정지'); shot(viewer, '10-gif-viewer'); key(viewer, '%{F4}')
    click(gif, '닫기'); key(player, '%{F4}'); main = window('Clip')
    fixture = subprocess.Popen([sys.executable, str(root/'scripts/qa-fixture.py'), '--full-screen'])
    time.sleep(.8)
    key(main, '^+2'); overlay = window('Clip 영역 선택'); shot(overlay, '11-capture'); key(overlay, '{ESC}')
    key(main, '^+9'); overlay = window('Clip 영역 선택'); overlay.set_focus()
    mouse.press(coords=(130,200))
    for step in range(1,11): mouse.move(coords=(130+80*step,200+45*step)); time.sleep(.025)
    mouse.release(coords=(930,650)); key(overlay, '{ENTER}')
    recording = window('Clip 녹화'); shot(recording, '12-record-ready')
    check('녹화 준비 시작 활성·정지 비활성', button(recording, '녹화 시작').is_enabled() and not button(recording, '정지·저장').is_enabled())
    key(recording, '^+9'); wait(lambda: button(recording, '정지·저장').is_enabled()); time.sleep(.6); shot(recording, '13-recording')
    key(recording, '^+9'); window('Clip'); check('녹화 단축키 시작·저장', len(list(captures.glob('Clip-*.mp4'))) == 1)
    check('GIF 파일 생성', len(list(captures.glob('Clip-*.gif'))) == 1)
    (out / 'results.json').write_text(json.dumps({'dpi':dpi,'results':results}, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'pid':proc.pid,'output':str(out),'passed':len(results),'dpi':dpi}, ensure_ascii=False))
finally:
    if fixture is not None and fixture.poll() is None: fixture.terminate(); fixture.wait(timeout=10)
    # 검증 앱은 오류 시에도 종료해 다음 실행의 단축키를 점유하지 않는다.
    if proc.poll() is None:
        if sys.exc_info()[0] is None:
            subprocess.run([sys.executable, str(root/'scripts/qa-close-clip.py'), str(proc.pid)], check=True, timeout=40)
        else:
            proc.terminate(); proc.wait(timeout=10)
