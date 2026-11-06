# 빠른 캡처·자동 업데이트 검증

작업 브랜치: `feat/quick-capture-autoupdate`, 버전: `0.7.0`.

## 구현

- 캡처 완료 시 PNG 자동 저장과 클립보드 복사. 기본은 창을 숨긴 채 Windows 토스트를 표시한다.
- 토스트마다 해당 캡처 경로를 전달하며 수신 시 창을 열어 그 항목을 선택한다. 저장 폴더 변경 뒤 예전 알림도 해당 PNG를 연다.
- 빠른 캡처는 기존 편집 문서를 교체하지 않는다. 설정의 `캡처 후 Clip 창 열기`로 매번 창을 열 수 있다.
- 설치판은 GitHub 정식 Release를 확인하고 다운로드한 뒤 다음 단독 실행 때 적용한다. 기존 앱이 실행 중이면 업데이트 적용 전에 활성화 신호만 전달한다.
- 설치판 사용자 데이터는 교체되는 `current` 밖에 둔다. 포터블은 기존 데이터 경로·수동 교체를 유지한다.
- `scripts/release.ps1 -Version 0.7.0`은 로컬 빌드·검증·패키징만 한다. 게시에는 별도 승인과 `-Upload`가 필요하다.

## 통과한 검증

- `scripts/build.ps1`: 경고 0, 오류 0.
- `scripts/test.ps1`: 26개 시나리오 통과. 새 기본값, 기존 설정 호환과 창 열기 옵션 저장·재로드 포함.
- `scripts/release.ps1 -Version 0.7.0`: 자체 포함 win-x64 배포, Velopack 부트스트랩 검사, Setup·Portable·nupkg·업데이트 피드 생성.
- NuGet 취약점 검사: 알려진 취약 패키지 없음. 기존 디자인 검증 프로젝트도 새 Windows 대상 프레임워크로 빌드 통과(실행 검증은 보류).
- `--update-review`: 실제 로컬 피드에서 0.6.0 → 0.7.0 탐지, nupkg 다운로드, SHA256 일치, 재시작 대기 판정, 최신 버전 판정, 잘못된 해시의 패키지 거부 통과. Velopack 공식 `TestVelopackLocator`를 사용하므로 실제 설치·파일 교체 검증은 아니다.

설치 파일: `artifacts/release/0.7.0/packages/Clip-win-Setup.exe`.

설치 파일 SHA256: `F2232FA22BD6F8F835C12E73E5E56FD16125F21564547BECFCB0F9223F24838A`. 최종 nupkg의 481개 항목에 사용자 데이터·테스트·참고 앱·도구·PDB는 포함되지 않았다.

## 화면 검증 보류

Windows native pipe 연결이 실패했고 대체 `orca` 실행 파일도 없다. 앱 내부 통합 검증 시 화면을 확인한 결과 Windows가 잠겨 있어 화면 검증을 중단했다. 클립보드 접근도 `CLIPBRD_E_CANT_OPEN`으로 실패했다. 잠금 화면은 제품 증거로 사용하지 않는다.

알림이 포함된 스크린샷, 실제 Ctrl+Shift+2 드래그 완료, 실제 토스트 클릭, 종료 상태에서 토스트 클릭, 창 열기 설정의 실제 조작은 **확인 불가**다. 이 폴더에 성공 스크린샷을 아직 저장하지 않았다. 따라서 브리프의 전체 완료 기준은 아직 충족하지 않았다.

잠금 해제 뒤 내부 통합 검증 재현 명령(실제 물리 조작 검증과 구분):

```powershell
. ./scripts/env.ps1
& $Dotnet run --project tests/Clip.Tests/Clip.Tests.csproj -c Release -- --quick-capture-review "$ClipRoot/artifacts/quick-review-new/data" "$ClipRoot/artifacts/quick-review-new/evidence"
```

각 실행에는 새 데이터 폴더를 사용한다. 성공한 화면을 확인하고 이 폴더에 복사한다. 테스트는 합성 이미지와 별도 데이터만 쓰며 테스트 알림 등록을 정리한다.

로컬 업데이트 검증 재현 명령:

```powershell
. ./scripts/env.ps1
& $Dotnet run --project tests/Clip.Tests/Clip.Tests.csproj -c Release -- --update-review "$ClipRoot/artifacts/release/0.7.0/packages" "$ClipRoot/artifacts/update-review-new"
```

## 공개 전 검사

- Gitleaks 8.30.1: `git . --log-opts=--all --redact` 검사에서 비밀값 0건. 원본 JSON은 비추적 `.tools/gitleaks-history.json`에 보관한다.
- 추적 파일과 이번 신규 소스를 별도로 모아 검사한 현재 공개 대상 파일도 0건이다. 추가 전체 폴더 검사에서 나온 11개 패턴은 모두 비추적 `.tools/`, `_reference/`, `artifacts/`에 있다. 참고 앱 분석·도구·과거 QA 결과를 공개 범위에 포함하지 않는다. 해당 원문은 문서에 복사하지 않는다.
- 전체 이력의 텍스트에서 이메일·휴대전화·주민번호 패턴 추가 검사: 본문 일치 파일 없음.
- 커밋 작성자 메타데이터에서 개인 이메일 1종 발견. 주소를 공개될 문서에 재기록하지 않는다.
- 브리프 C의 조건에 따라 **저장소는 비공개로 유지**한다. 공개 전환과 이력 수정은 실행하지 않았다. 비밀값 검사 통과를 개인정보 전체 검사 통과로 간주하지 않는다.
- main 병합·push·Release 업로드는 실행하지 않았다. GitHub 공개 피드 전달도 아직 확인 불가다.

## 구현 근거

- [Velopack 초기화](https://docs.velopack.io/getting-started/csharp): WPF 진입점의 부트스트랩.
- [업데이트 통합](https://docs.velopack.io/integrating/overview): 자동 다운로드, 대기 패키지, 실행 중인 다중 인스턴스 고려.
- [GitHub 업데이트 소스](https://docs.velopack.io/integrating/update-sources): 공개 저장소를 토큰 없이 사용.
- [Microsoft 알림 API](https://learn.microsoft.com/en-us/dotnet/api/communitytoolkit.winui.notifications.toastnotificationmanagercompat): Windows 토스트와 활성화 이벤트.

코드 서명은 미구성 상태다. 실제 설치·재시작 교체, GitHub 전달, Windows 10 및 혼합 DPI는 별도 검증이 필요하다.
