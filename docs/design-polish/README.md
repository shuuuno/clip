# Clip 디자인 마감

2026-11-06. 로컬 브랜치 `design-polish`. 시작 커밋은 `76f07877471083f29d64e2cd6c53ee527c8ddac6`이다. [요청 문서](../brief_design_polish.md)의 Impeccable audit→critique→polish를 수행하고 Hallmark 기준으로 최종 점검했다. 제품의 다크·파랑, Windows 글꼴과 편집 배치는 유지했다.

## 변경

- 입력 경계·비활성·오류·주요 버튼 글자색을 공통 Theme 토큰으로 정의했다. 입력 경계는 구획선과 구별한다.
- 키보드 포커스를 버튼 채움과 3 DIP 떨어진 2 DIP 링으로 표시해 파란 버튼에서도 분명하게 했다. 크기 변화나 애니메이션은 없다.
- 이미지가 없으면 속성·이미지 정보·줌 행을 숨기고 캡처 안내에 집중한다. 이미지를 열면 같은 위치에 복원한다.
- 설정 오류는 원인과 해결 문구를 유지하면서 오류색을 적용하고, 잘못된 저장 경로의 테두리도 표시한다.
- 이미지 크기·주석 수·저장 상태를 잇던 반복 중점 구분을 없앴다.

수정 파일은 `Theme.cs`, `Controls.xaml`, `MainWindow.cs`, `SettingsDialog.cs`이며 의도는 [DESIGN.md](../../DESIGN.md)에 반영했다. 캡처·녹화·GIF 처리 코드와 저장 규칙은 수정하지 않았다. 외부 앱의 코드·자산은 제품에 사용하지 않았다. 새 소개 화면·랜딩 페이지·폰트·제품 의존성은 추가하지 않았다.

## Impeccable 결과

[수정 전 audit·critique](critique-before.md)에 점수·관찰·우선순위를 남겼다. audit 13/20, critique 32/40은 관찰 범위에 대한 정성 평가이며 자동 접근성 인증 점수가 아니다. 첫 기록이라 이전 추세는 없다. 대상 slug는 `src-clip-views-mainwindow-cs`이며 스냅샷 `2026-11-06T03-16-09Z__src-clip-views-mainwindow-cs.md`를 저장·조회하고 해당 두 우선 항목을 해결한 뒤 닫았다.

| 우선 항목 | 결과 |
|---|---|
| P1 공통 컨트롤 대비·포커스 | 입력 경계 1.28~1.74:1에서 3.63~4.92:1. 포커스 링은 인접 중립 배경 대비 8.81~9.61:1 |
| P2 빈 화면·오류 위계 | 빈 편집기의 무효 속성 행 제거. 오류 글자 Panel 대비 9.50:1, 입력 경계도 표시 |

## Hallmark 결과

프로젝트에 설치한 [Hallmark](../../.agents/skills/hallmark/SKILL.md) 2.0.0의 app scope와 polish 범위로 점검했다. 기존 [DESIGN.md](../../DESIGN.md)를 고정 시스템으로 사용했다. 자료는 실제 제품 컨트롤과 합성 fixture이고 마케팅 수치·고객·추천·로고는 없다.

```powershell
node .agents/skills/hallmark/scripts/sloplint.mjs src/Clip/Views --scope app --genre modern-minimal --render
```

실행 결과는 `sloplint: no .html, .css, .jsx, .tsx, .vue, .svelte or .astro files found`, 종료 코드 1이다. **자동 검사 통과가 아니다.** Impeccable의 웹 detector도 네이티브 리뷰 지침에 따라 사용하지 않았다. WPF를 HTML 모형으로 바꾸어 검사를 통과시키지 않았다.

네이티브에 적용 가능한 기준은 소스와 실제 렌더로 확인했다.

| 기준 | 확인 |
|---|---|
| 40·41 대비 | 실제 Theme 브러시를 사용한 수식 검사 6개 통과. 주요 버튼 글자 4.91:1 |
| 26a 포커스 | 실제 WPF Focus()·IsKeyboardFocused 통과, 전후 08-focus 이미지 |
| 39a 입력·상태 | 폴더 입력/선택 버튼 높이 36 DIP 유지, 비활성은 native IsEnabled 유지, 오류 문구와 색 동시 표시 |
| 48 토큰 | 변경한 UI 색은 Theme에 정의하고 컨트롤에서 공통 자원으로 소비 |
| 63 구분 기호 | 반복 중점 메타 문자열 제거. 단일 단축키 구분과 실제 파일 형식·날짜 캡션은 기능을 구분하는 기존 표기이므로 유지 |
| 장식·문구 | 그라데이션·장식 글자·그림 카드·이모지 아이콘·새 애니메이션·가짜 지표 없음 |
| 배치·밀도 | 기본 1220×800, 최소 960×640 DIP에서 주요 버튼 경계와 겹침 검사 통과 |

독립 finish review는 `gpt-6-astra`, `high`, 대화 이력 없이 읽기 전용으로 실행했다. 첫 판정은 텍스트 입력창 캡처 잘림에 대한 `recapture`였다. 촬영 호스트가 컨트롤의 margin을 제외하던 범위를 실제 상위 ContentPresenter 전체로 고쳤다. 제품 레이아웃은 바꾸지 않았다. 이후 시작 커밋과 수정본을 같은 호스트로 재촬영했다. 최종 판정은 별도 [Hallmark 리뷰 기록](hallmark-review.md)에 남긴다.

웹 전용 hero·landing·375px 모바일·CSS stamp·DOM render 기준은 Windows 전용 WPF 앱에 적용하지 않는다. 기존 Win32/WPF의 실제 창 제목줄은 웹페이지에서 모방한 OS 프레임이 아니다.

## 검증과 촬영

- `scripts/build.ps1`: 경고 0, 오류 0.
- `scripts/test.ps1`: 기존 기능 25개 시나리오 통과. 증거는 `artifacts/tests/d1b30df7eb5d4e96a7624d65a5445428`.
- `scripts/qa-design-polish.ps1`: 수정본 50개 WPF 렌더·상태·경계·대비 검사 통과. 그중 28개는 최소 창 버튼 쌍별 겹침 검사이며 50개의 독립 사용자 흐름을 뜻하지 않는다. 상세 결과는 [verification.json](verification.json).
- 시작 커밋을 `.tools/design-polish/baseline-source`에 `git archive`로 추출해 같은 촬영 호스트로 전후를 생성했다. 변경 전 상태 검사 44개, 변경 후 상태·대비 검사 50개다. 제품 데이터와 분리된 합성 이미지·설정을 사용했다.
- 해상도는 현재 Windows의 DPI 168(175%), 기본 창 1220×800 DIP와 최소 960×640 DIP다.

PNG는 실제 제품 WPF 컨트롤의 `RenderTargetBitmap` 캡처이며 창 바깥 OS 프레임은 포함하지 않는다. 제품 화면을 재구현한 목업이 아니다. Windows 제어 플러그인은 native pipe 연결 실패, 기존 외부 UI 스크립트는 SetForegroundWindow 오류로 실행을 끝내지 못했다. 따라서 이번 검증은 **데스크톱 화면 사진이나 외부 마우스·키보드 입력 실기와 구분한다.** WPF 내부의 실제 포커스 획득은 별도로 검사했다. 다른 장비·Windows 10·혼합 DPI·화면 읽기 프로그램의 음성 출력·전체 Tab 순회는 미확인이다.

재현 명령은 아래와 같다. 출력에는 존재하지 않는 새 폴더를 지정한다. 실행 시 검증 창이 열린 뒤 종료된다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/qa-design-polish.ps1 -Output artifacts/ui/design-polish-new-check
```

촬영용 프로젝트와 빌드 산출물은 `.tools/`에만 생성된다. 테스트 호스트는 제품의 OnStartup을 실행하지 않으므로 트레이·전역 단축키·로그인 자동 실행을 등록하지 않는다. 테스트 종료 후 창과 프로세스를 종료한다. 제품용 빌드·테스트 진입점은 바꾸지 않았다.

## 전후 이미지

| 화면 | 변경 전 | 변경 후 |
|---|---|---|
| 빈 편집기 | [01-empty-before.png](01-empty-before.png) | [01-empty-after.png](01-empty-after.png) |
| 설정 | [02-settings-before.png](02-settings-before.png) | [02-settings-after.png](02-settings-after.png) |
| 잘못된 경로 | [03-settings-error-before.png](03-settings-error-before.png) | [03-settings-error-after.png](03-settings-error-after.png) |
| 이미지 편집 | [04-editor-before.png](04-editor-before.png) | [04-editor-after.png](04-editor-after.png) |
| 최소 창 | [05-minimum-before.png](05-minimum-before.png) | [05-minimum-after.png](05-minimum-after.png) |
| 텍스트 입력 | [06-text-before.png](06-text-before.png) | [06-text-after.png](06-text-after.png) |
| 저장 후 | [07-saved-before.png](07-saved-before.png) | [07-saved-after.png](07-saved-after.png) |
| 복사 키보드 포커스 | [08-focus-before.png](08-focus-before.png) | [08-focus-after.png](08-focus-after.png) |

현재 보조 메타데이터 `.impeccable/design.json`은 DESIGN.md보다 오래되었다고 context가 보고한다. 요청하지 않은 재생성은 하지 않았다. 필요하면 Impeccable `document`로 갱신할 수 있다. `PRODUCT.md`의 Windows 플랫폼 값도 도구의 web/ios/android/adaptive 열거형에 없으므로 자동 분류 결과를 사용하지 않았다.

스킬 버전·원본·라이선스는 [설치 기록](../../.agents/skills/INSTALLATION.md)에 있다. 이 작업은 로컬 브랜치에만 보관하며 merge·push·배포·publish는 수행하지 않는다. Grok Bot 보고 결과는 [발송 기록](grokbot-report.md)에 기록한다.
