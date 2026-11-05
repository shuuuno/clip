# Clip 검증 기록

## Clip 0.5 자동 실행·캡처 즉시 복사·UI 개선 · 2026-11-05

사용자 우선순위인 **캡처 즉시 복사 → 주석·공유 → 라이브러리 → 녹화·GIF**에 맞춰 개선했다. 영역 캡처를 확정하면 편집기에 표시하면서 즉시 클립보드에 복사한다. 편집 후 복사는 사용자가 실행한다. 새로운 복사 요청이 이전 재시도를 대체하며, 실패해도 편집 이미지는 유지한다.

- 자동 검사 **25개 통과**, 빌드 경고·오류 **0개**. 기존 23개에 자동 실행 명령줄 경로·공백·루트·길이 제한과 격리된 레지스트리 키의 등록·해제·다른 복사본 보호·복원을 추가했다.
- 최종 포터블 실제 UI **32개 통과**: 자동 실행 8개, 캡처 즉시 복사 2개, 녹화·캡처 회귀 7개, 띠 제거 7개, 크롭 1개, GIF 7개.
- 즉시 복사: Ctrl+C 없이 캡처 Enter만으로 기존 텍스트 클립보드가 **800×450 이미지**로 바뀌고 저장 PNG와 픽셀이 같았다. 다른 프로세스가 클립보드를 점유한 실패에서도 편집 이미지를 유지하고, 점유 해제 후 수동 복사가 성공했다.
- 자동 실행: 설정 취소 무변경, 정확한 EXE·데이터 경로 등록, 잘못된 폴더 변경 차단, 등록 명령 실행 시 창 없이 트레이 대기, 일반 재실행 시 기존 창 표시, X 닫기 후 프로세스 유지, 설정 끄기 후 등록 제거를 확인했다. settings.json 교체를 실제 파일 잠금으로 실패시켜 등록 롤백도 확인했다. **테스트 전 등록 상태로 복원했으며 이 PC의 Clip.Capture 항목은 없다.**
- UI: 폭 208 DIP 라이브러리와 120 DIP 세로 도구, 속성·줌·완료 동작을 분리했다. 버튼·콤보·입력·목록·체크박스·스크롤에 공통 WPF 템플릿을 적용했다. 아이콘은 직접 작성한 기하 경로이며 외부 자산은 없다. 설정은 스크롤 본문과 고정 하단 동작으로 오류가 길어져도 저장·취소를 유지한다.
- 독립 읽기 전용 검토: 제품·디자인 문서, WPF 소스 4개, 최종 화면 6장을 검토한 결과 **배포 가능, 중대한 UI 문제 없음**. 기본 창 및 **최소 960×640 DIP**에서 주요 도구와 복사·저장이 유지됐다. 대비 계산은 본문/패널 14.52:1, 보조 글자/패널 7.20:1, 흰 글자/주요 파랑 4.91:1이다. 검토자는 기능 검사를 별도로 재실행하지 않았다.
- 환경: Windows 11, 3840×2160, DPI 168(175%). 시스템 배율은 변경하지 않았다. 합성 화면·샘플 문서만 사용하고 테스트 앱과 합성 창을 종료했다.

검사 중 발견한 사항: 숨김 트레이 영역의 좌표 클릭과 가상화된 라이브러리의 화면 밖 항목 조회는 QA 도구를 수정했다. 0길이 띠 선택 뒤 Esc는 선택 도구로 돌아가도록 제품 동작을 정리해 불필요한 닫기 확인을 방지했다. 초기 실패를 최종 통과로 집계하지 않았다.

근거: `artifacts/test-results-0.5.txt`, `build-results-0.5.txt`, `publish-results-0.5.txt`; `artifacts/ui/startup-portable-results-0.5.json`, `auto-copy-portable-results-0.5.json`, `recording-portable-results-0.5.json`, `strip-portable-results-0.5.json`, `crop-regression-0.5.json`, `gif-portable-results-0.5.json`, `final-results-0.5.json`. 최종 화면은 `artifacts/ui/screens/30-empty-final-0.5.png`, `31-editor-final-0.5.png`, `32-minimum-final-0.5.png`, `33-selection-final-0.5.png`, `28-settings-0.5.png`, `29-settings-error-0.5.png`다.

정책·한계: 자동 실행은 명시적으로 설정을 켜고 저장할 때만 현재 사용자 HKCU Run에 등록한다. 포터블 폴더를 옮기면 새 경로에서 설정을 다시 저장해야 한다. Windows가 시작 앱을 지연하거나 사용자가 비활성화할 수 있다. 명령줄 260자 제한은 [Microsoft Run/RunOnce 문서](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys), OS에서의 시작 앱 제어는 [Windows 시작 앱 설정](https://support.microsoft.com/en-us/windows/experience/startup-boot/configure-startup-applications-in-windows)을 따른다. **실제 재부팅·로그인, 다른 PC, Windows 10, 혼합 DPI·다중 모니터·고대비 모드, 모든 팝업 상태, HDR와 장시간 녹화는 미검증**이다. 이미지 기본 앱 연결·Print Screen 대체·오디오·포인터 녹화는 추가하지 않았다. 참고 앱 코드·자산은 제품에 포함하지 않는다. 아래는 각 이전 버전 시점의 기록이다.

## Clip 0.4 이미지 띠 제거 · 2026-11-05

가로 행 또는 세로 열을 선택해 제거하고 간격 없이 바로 붙인다. 적용 전 제거 범위·결과 크기를 표시하며 Enter/적용 버튼, Esc 취소, 도구 전환 취소를 지원한다. 크롭과 같이 기존 주석을 합성하고 Undo에서 이미지·주석·번호 상태를 함께 복원한다. 가장자리 제거를 허용하며 결과는 최소 1픽셀을 남겨야 한다. 원본 파일은 덮어쓰지 않는다.

- 자동 검사 **23개 통과**, 빌드 경고·오류 **0개**. 기존 20개에 모든 가로·세로 경계 조합의 픽셀·알파 보존, 주석 합성·Undo/Redo·저장 지점·PNG 재열기, 잘못된 구간·전체 제거 차단을 추가했다.
- 최종 포터블 실제 UI **7개 통과**: 역방향 선택·크기 예고·Esc, 가로 100px 제거, 세로 80px 제거, 전체·0길이 차단, 주석 합성·복원·미완료 선택 정리, 도구 전환 취소, 최소 창 960×640 DIP에서 축소 선택.
- 독립 Pillow 검사에서 **800×500 → 800×400**, **800×500 → 720×500** PNG와 클립보드가 원본을 직접 이어 붙인 기대 픽셀과 완전히 같았다. 원본 PNG의 SHA-256은 유지됐다.
- 기존 크롭 UI 회귀 **1개 통과**: 띠 제거 결과를 400×200으로 크롭한 클립보드 픽셀이 독립 계산과 같고 Undo로 복원됐다.
- Windows 11, 3840×2160, DPI 168(175%). 50% 축소에서 화면 1px은 이미지 약 1.14px이다. 30px 부근을 선택한 실제 범위는 29px였고 예고한 800×371과 출력 크기가 일치했다. 시스템 배율은 변경하지 않았다.
- 작은 창에서 굵기·글자 설명과 선택 상자가 서로 다른 줄로 갈라지지 않게 묶었다. 처음 UI 검사 도구는 확대 콤보에 포커스를 둔 채 단축키를 보내거나 잘린 화면 높이로 좌표를 계산했다. 도구의 포커스·창 크기를 바로잡았으며 이 실패를 제품 동작 성공으로 집계하지 않았다.
- 포터블이 패키지 내부 coreclr.dll을 로드함을 확인했다. 실제 트레이 메뉴로 테스트 Clip을 종료했다. 참고 앱 코드·자산은 사용하지 않았다.

근거: `artifacts/test-results-0.4.txt`, `build-results-0.4.txt`, `publish-results-0.4.txt`, `artifacts/ui/strip-ui-results.json`, `strip-portable-ui-results.json`, `crop-regression-0.4.json`, `strip-portable-modules.json`, `screens/23-strip-selection.png`, `screens/24-strip-result.png`, `screens/25-strip-minimum-window.png`, `final-results-0.4.json`.

정책·한계: 투명 간격을 추가하지 않는다. 적용 후 주석은 합성된 이미지의 일부이므로 개별 이동하려면 Undo해야 한다. 축소 상태에서 1px 단위 위치를 지정하기 어려우면 확대해 선택한다. 혼합 DPI·다중 모니터·Windows 10 실기, 매우 큰 이미지의 메모리 부하, 실제 디스크 고갈은 미검증이다. 오디오·포인터 녹화는 이번 범위에 추가하지 않았다. 아래는 이전 버전 시점의 검증 기록이다.

## Clip 0.3 GIF 구간 내보내기 · 2026-11-05

MP4에서 시작·끝 시간, 비율을 유지한 가로 크기, fps를 지정해 별도 GIF로 저장한다. 원본 MP4는 수정하지 않는다. GUI 시험 당시 화면은 3840×2160, 시스템 DPI 168(175%)이었다. 에이전트가 시스템 배율을 변경하지 않았다.

- 자동 검증 **20개**: 기존 14개와 MP4 메타데이터·축소·탐색·방향, GIF 시간 양자화·옵션 경계, 구간 변환·프레임·원본 보존, 취소·저장 실패 정리, 홀수 크기·손상 원본, GIF 프레임 색인·썸네일·손상 GIF 거부.
- 독립 Pillow/FFmpeg 검사: 원본 1~3초 구간을 **160×120, 30프레임, 2,000ms, 무한 반복**으로 저장. 첫 프레임은 독립 디코더의 1초 기준 영상과 평균 채널 오차 약 3.923/255였다. 위 빨강·아래 파랑, 움직임, 161×121 홀수 크기와 150ms 짧은 GIF도 확인했다.
- 실제 UI **7개 시나리오**: 잘못된 구간 저장 차단, 구간·크기·fps 저장, 반복 미리보기·일시 정지, 라이브러리 재열기, 변환 중 취소, 변환 중 창 닫기, 변환 중 트레이 앱 종료. 취소 후 미완료 파일은 없고 원본 MP4의 SHA-256은 유지됐다.
- 최대 설정 시험: 합성 MP4 30초 → **1280×720, 30fps, 900프레임**, 변환 **18,901ms**, 테스트 프로세스 최대 작업 메모리 **211,628,032바이트(약 202MiB)**. 출력 **130,291,873바이트**, 별도 디코더로 900프레임·총 30,000ms·무한 반복을 확인했다. 이 수치는 해당 합성 영상·PC의 측정이며 모든 영상의 속도·용량을 보장하지 않는다.
- 큰 GIF 재생 시험: 32초 동안 첫 반복이 끝나고 다음 반복까지 진행됐다. 재열기 **0.328초**, 최대 작업 메모리 **289,538,048바이트(약 276MiB)**, 최대 private bytes **207,990,784바이트**. GIF 프레임 위치만 색인하고 한 장의 표시 버퍼를 재사용한다.
- 큰 GIF 창을 찾지 못한 초기 시험 실패는 UI Automation의 최상위 창 목록만 조회한 도구 오류였다. Win32 하위 창 핸들로 찾아 재검증했다. 앱 열기 지연으로 판정하지 않는다. 트레이 아이콘이 숨김 영역 밖에 있는 환경도 종료 도구가 처리하도록 보완했다.

최종 포터블에서도 GIF 실제 UI 7개와 기존 녹화·캡처 회귀 7개를 통과했다. FFmpeg 없는 PATH와 유효하지 않은 외부 DOTNET_ROOT로 실행했으며 패키지의 coreclr.dll과 Windows의 Media Foundation 모듈을 로드했다. 회귀 시험의 MP4 4개를 별도 FFmpeg 디코더로 끝까지 읽었다. 빌드 경고·오류는 0개다. 트레이 정리 도구는 다른 창이 아이콘 영역을 덮을 때 잘못 클릭하던 문제를 해당 영역에 먼저 포커스를 주도록 수정했다. Clip은 실제 트레이 메뉴로 종료했고 합성 시험 창도 닫았다.

근거: `artifacts/test-results-0.3.txt`, `artifacts/build-results-0.3.txt`, `artifacts/ui/gif-decoder-results.json`, `gif-ui-results.json`, `gif-portable-ui-results.json`, `recording-ui-regression-0.3.json`, `recording-media-regression-0.3.json`, `portable-modules-0.3.json`, `gif-stress/stress-results.json`, `gif-stress-decoder-results.json`, `gif-preview-stress-results.json`, `screens/21-gif-saved.png`, `screens/22-gif-preview.png`.

제한: GIF 0.1~30초·각 변 1280px 이내·5~30fps·256색·무한 반복. GIF 시간은 10ms 단위로 반올림한다. 미리보기는 Clip의 전체 프레임·불투명 GIF용이며 외부 부분 프레임/투명 GIF는 거부한다. 외부 MP4 코덱·회전 메타데이터·HDR, Windows N, 실제 디스크 고갈, 강제 종료 중 복구는 미검증이다. 오디오·포인터 녹화·띠 제거는 추가하지 않았다.

공식 근거: [Media Foundation Source Reader](https://learn.microsoft.com/en-us/windows/win32/medfound/processing-media-data-with-the-source-reader), [영상 크기·색상 변환](https://learn.microsoft.com/en-us/windows/win32/medfound/mf-source-reader-enable-advanced-video-processing), [Windows GIF 인코더](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.gifbitmapencoder?view=windowsdesktop-10.0), [GIF89a 형식](https://www.w3.org/Graphics/GIF/spec-gif89a.txt). 영상 디코딩·축소와 프레임 압축에는 Windows 내장 기능을 사용하며 제품은 FFmpeg를 실행하거나 배포하지 않는다.

## Clip 0.2 영역 녹화 · 2026-11-05

기존 이미지 MVP에 소리·포인터 없는 영역 녹화와 MP4 라이브러리 재생을 추가했다. Windows 11 / 3840×2160 / 150% 배율, 연결 모니터 한 대에서 검증했다. 혼합 배율·다중 모니터의 실제 조작 검증은 하지 않았다.

- 자동 검증 **14개**: 기존 9개, 음수 원점·물리 픽셀 경계·짝수 패딩, MP4 타임스탬프·원자 완료, 미완료 정리, 최종 이동 실패 시 복구 파일 보존, 4K 인코딩.
- Media Foundation으로 생성한 시험 MP4를 독립 FFmpeg 디코더로 확인했다. 320×240 패턴의 재생 시간 **4.000초**, 위쪽 빨강·아래쪽 파랑, 시간에 따른 프레임 변화가 맞다. 3840×2160 H.264 파일도 생성·확인했다. 이는 4K 인코더 시험이며 4K 화면 실시간 30fps 성능 보장은 아니다.
- 실제 UI: 영역/준비 취소, Ctrl+Shift+9 시작·정지, 녹화창 닫기 후 저장, 짧은 녹화와 반복 녹화, 라이브러리 재생·일시정지·처음부터, 기존 이미지 캡처·PNG 저장 회귀를 통과했다.
- 실제 801×551 선택 → **802×552** H.264 MP4, **7.3333초**. 홀수 영역의 오른쪽·아래 여백 정책을 확인했다. 움직이는 합성 화면만 사용했다.
- 녹화 제어창을 촬영 영역 위로 옮겨도 저장 영상에는 제어창이 나타나지 않았다. Windows 캡처 제외 기능 실패 시 창을 숨기는 대체 경로는 구현했으나 이 PC에서 실제 실패 조건을 재현하지는 않았다.
- 녹화 중 실제 트레이의 Clip 종료 실행 → MP4 저장 완료 → 프로세스 종료, 미완료 파일 0개.
- 일반 파일이 저장 폴더를 가로막는 실패를 UI로 재현했다. 오류 안내 후 라이브러리로 복귀하고 기존 파일은 그대로 유지했다.
- 재생 시간 메타데이터를 Windows 플레이어가 제공하지 않는 매우 짧은 영상은 크기·현재 위치만 표시하고 탐색 슬라이더를 비활성화한다. 재생·일시 정지·처음부터는 사용할 수 있다.

UI 시험 중 녹화 준비창 닫기의 재진입 오류와 동기 UI Automation 선택에 걸리는 재생창 열기를 수정했다. 초반 실패 기록을 최종 통과 증거로 사용하지 않는다.

근거: `artifacts/test-results-0.2.txt`, `artifacts/build-results-0.2.txt`, `artifacts/ui/recording-ui-results.json`, `recording-exit-results.json`, `recording-failure-results.json`, `recording-media-results.json`. 영상에서 추출한 프레임은 `artifacts/ui/screens/15-recording-frame.png`다.

최종 포터블 배포본에서도 실제 UI 7개 시나리오를 다시 통과했다. FFmpeg가 없는 PATH와 유효하지 않은 외부 DOTNET_ROOT로 시작했고, 패키지의 `coreclr.dll` 및 Windows의 `mfplat.dll`·`mfreadwrite.dll`을 로드한 것을 확인했다. 배포본에서 생성한 MP4 4개 모두 독립 디코더로 끝까지 읽었다. 근거는 `recording-portable-modules.json`, `recording-portable-media-results.json`, `screens/19-recording-portable-final.png`다. 빌드 경고·오류는 모두 0개다.

남은 검증: 다중 모니터·혼합 DPI 실기, 모니터 연결 변경에 따른 자동 정지, Windows 10·Windows N·HDR, 장시간 녹화·실제 디스크 고갈·강제 종료 복구. GIF·오디오·마우스 포인터 녹화·영상 편집은 미구현이다.

공식 API 근거: [Media Foundation Sink Writer](https://learn.microsoft.com/en-us/windows/win32/medfound/tutorial--using-the-sink-writer-to-encode-video), [H.264 인코더](https://learn.microsoft.com/en-us/windows/win32/medfound/h-264-video-encoder), [녹화 제어창 캡처 제외](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity). COM ABI는 Microsoft Windows SDK의 `mfobjects.h`·`mfreadwrite.h`를 확인해 독립 작성했다. 참고 앱 코드·자산은 사용하지 않았다.

## Clip 0.1 이미지 MVP · 기존 기록

검증일: 2026-11-04. 대상: 독립 작성한 Clip 이미지 MVP. 참고 앱 검증과 별개다.

## 환경

- Windows 11 Pro x64, 10.0.26300 / 빌드 26300.
- 기본 화면 3840×2160, 테스트 창 DPI 144(150%). 시스템 배율은 변경하지 않았다.
- 로컬 .NET SDK 10.0.403, C#/.NET 10 WPF. SDK·캐시·임시 파일은 프로젝트 `.tools/` 안에 둔다.
- UI 시험은 합성 화면만 사용했다. 설정·저장 파일·스크린샷은 `artifacts/ui/`에 있다.

## 자동 검증

`scripts/test.ps1`의 STA 테스트 프로그램에서 9개 시나리오를 검증한다. 외부 테스트 프레임워크 대신 종료 코드가 있는 실행형 검사를 사용한다.

1. 역방향·1픽셀·화면 경계 선택과 무작위 좌표 1,000개.
2. 방향키 이동·크기 변경 및 경계 제한.
3. 모자이크 블록 평균값과 영역 바깥 픽셀 보존.
4. 주석 합성, 원본 불변, Undo/Redo, 저장 지점으로의 복귀.
5. 크롭의 실제 픽셀·크기 및 실행 취소 시 원본·주석 복원.
6. 원자 저장 실패 시 기존 파일 보존, 임시 파일 제거.
7. PNG 재열기 픽셀 일치, 동명 회피, 파일 잠금 해제, 저장 실패 시 편집 보존.
8. 손상 설정 원본 보존과 유효 설정 재시작 유지.
9. 실제 RegisterHotKey 충돌, 변경 실패 시 기존 등록 유지, 해제.

합성 영역 800×450을 GDI로 100회 캡처한 별도 검사: **1,674ms, GDI 핸들 5 → 5**. 이는 해당 PC·영역의 캡처 루프 측정이며 앱 전체 100회 조작이나 장시간 메모리 안정성을 보장하지 않는다.

## 실제 UI 확인

| 시나리오 | 결과 |
|---|---|
| 기본 Ctrl+Shift+2 → 영역 선택 → Enter | 편집기로 캡처 전달 |
| 단축키 변경·재시작 | 설정에서 Ctrl+Shift+3으로 변경 후 실제 호출. 재시작 후 동일 설정 유지 |
| 픽셀 선택·미세 조정 | 실제 화면 (121,195)→(921,645), 800×450 선택. 오른쪽 1px 이동·높이 1px 확대 후 **800×451** PNG 저장 |
| 선택 취소 | Esc 후 라이브러리 파일 수 유지 |
| 번호·박스·한글 문자열·모자이크 | 4개 주석 표시·합성 확인. 한글은 Unicode 자동 입력이며 실제 IME 조합 시험과 구분 |
| PNG·클립보드 | 편집한 PNG와 클립보드 RGBA 픽셀이 완전히 같음 |
| 크롭 | 실제 드래그로 **600×300** PNG 생성 |
| 저장 위치 변경 | 지정한 새 프로젝트 내부 폴더에 PNG 저장. 이전 파일은 이동하지 않음 |
| 미저장 닫기 | 저장 여부 창 표시. 취소 시 편집 유지, 예 선택 시 PNG 저장 후 트레이 유지 |
| 중복 실행 | 두 번째 프로세스 exit 0, 기존 프로세스 1개 유지·창 재활성화 |
| 트레이 종료 | 실제 종료 메뉴에서 프로세스 종료 확인 |
| 포터블 배포본 | 별도 런타임 설치 경로 없이 실행. 실제 coreclr·WPF 네이티브 모듈이 패키지 폴더에서 로드됨 |
| 배포본 편집·출력 | PNG 열기 → 번호·박스·모자이크 → 복사·저장. 800×451 PNG와 클립보드 RGBA 픽셀 완전 일치 |

UI 시험 중 배율 좌표 반올림, 입력 컨트롤 대비, 닫는 중 창 다시 표시 오류를 찾아 수정했다. 시험 도구의 마우스 좌표 오차도 별도로 측정하고 정확한 전달 좌표와 저장 크기로 재검증했다. 이전 실패 화면은 최종 성공 증거로 사용하지 않는다.

최종 상태의 대표 증거는 작업 폴더 `artifacts/ui/screens/08-capture-verified.png`, `10-unsaved-confirm.png`, `11-cancel-preserves-edit.png`, `12-settings-restored.png`다. 초기 편집 결과는 `03-edited.png`, 픽셀 비교는 `first-ui-results.json`, 중복 실행은 `single-instance.json`에 기록했다. 배포 ZIP에는 시험 이미지나 참고 앱 파일을 포함하지 않는다.

배포본 최종 화면은 `artifacts/ui/screens/14-portable-check.png`, 픽셀 비교는 `artifacts/ui/portable-results.json`, 런타임 모듈 경로는 `artifacts/ui/portable-modules.json`에 있다. 최종 빌드는 경고·오류 0개이며 9개 자동 검증을 모두 통과했다. 배포본도 실제 트레이 메뉴로 종료했다.

## 아직 확인하지 않은 범위

- Windows 10 및 다른 Windows 11 빌드, 다중 모니터·혼합 DPI·음수 모니터 원점·HDR·모니터 연결 변경.
- 실제 두벌식 IME 조합, 모든 외부 앱과 클립보드 형식 호환, 보호 콘텐츠·보안 데스크톱.
- 장시간 상주, 거대한 이미지·많은 주석·수만 장 라이브러리의 성능, 실제 디스크 고갈.
- 자동 테스트의 파일 오류 경로와 실제 사용 환경의 모든 I/O 실패는 동일한 보장이 아니다.

녹화/GIF·띠 제거·로그인 자동 시작·자동 업데이트는 이번 구현 범위에 없다.
