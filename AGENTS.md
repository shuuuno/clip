# 프로젝트 지침

- 작업 루트는 `D:\projects\my-capture`이며 요구사항은 UTF-8 한국어 `brief.md`와 후속 `brief2.md`에 있다. 충돌하면 최신 사용자 지시를 따른다.
- 제품명은 `Clip`인 Windows 10/11 개인용 캡처 앱 프로젝트다. 폴더명은 `my-capture`로 유지한다. 이미지·MP4 녹화·GIF 내보내기에 이어 이미지 띠 제거 구현도 승인됐다. 오디오·포인터 녹화는 후속 범위다.
- 결제·push는 별도 승인 전 금지한다. `brief2.md`가 허용한 참고 앱 실행·역컴파일은 `_reference/`에서만 분석하며, 테스트로 생긴 시스템·앱 설정 변경을 정리하고 앱을 종료한다. 외부 발송은 명시적으로 요청된 ask-grokbot 보고만 허용한다.
- 참고 앱의 역컴파일 결과와 자산은 분석 전용이며 Clip에 복사하지 않는다. 보고서는 직접 실행 관찰, 정적 분석, 추론, 확인 불가를 구분한다.
- C#/.NET 10 WPF, Win32 캡처·단축키, WinForms 트레이를 사용한다. SDK·NuGet·CLI·임시 파일은 `.tools/`, 앱 데이터는 기본 `data/`에 둔다. 시스템 자동 시작·업데이트·push는 구현 범위가 아니다.
- `scripts/build.ps1`, `scripts/test.ps1`, `scripts/publish.ps1`가 빌드·검증·포터블 배포 진입점이다. 제품 프로젝트는 `src/Clip`, 검증은 `tests/Clip.Tests`다.
- Git 기본 브랜치는 `main`이다. `_reference/`, `.tools/`, `artifacts/`, `data/`와 빌드 결과는 추적하지 않는다. 공유는 사용 데이터 없는 버전별 포터블 ZIP으로 하며 절차는 `docs/sharing.md`에 있다.
- 캡처·편집 좌표는 실제 이미지 픽셀 기준이다. WPF DIP는 PointToScreen으로 변환한다. 주석은 세션 중에만 객체로 유지하고 PNG 저장·클립보드는 합성 결과를 내보낸다. 저장은 새 파일+원자 이동으로 기존 파일을 보존한다.
- 띠 제거는 가로 행·세로 열을 삭제해 간격 없이 연결한다. 크롭과 같이 주석을 합성하고 Undo에서 원본·주석을 복원한다. 이미지 변경·도구 변경 시 미완료 선택을 지운다.
- WPF Closing 중에는 Show를 호출하지 않는다. 창 닫기는 저장 확인 후 트레이 유지, 트레이 종료는 자원을 해제하고 프로세스를 종료한다. 빌드 전 실행 중인 테스트 Clip을 종료한다.
- 녹화는 `Recording/ScreenRecorder` 전용 작업 스레드에서 GDI 캡처와 Media Foundation COM 인코딩을 수행한다. `Mp4Writer` 포인터는 생성 스레드에서만 사용·해제하며 Finalize 후 파일을 최종 이름으로 이동한다. FFmpeg는 개발 검증에만 사용하며 배포 의존성이 아니다.
- WPF Closing 안에서 다시 Close하지 않는다. 녹화창의 취소·정지 요청은 Dispatcher로 예약한다. 영상 선택 시에도 UI Automation 동기 호출을 반환한 뒤 재생창을 연다.
- GIF는 `VideoReader`의 Media Foundation 디코딩·축소와 `GifStreamWriter`의 프레임별 WIC 팔레트/LZW 인코딩을 사용한다. GIF 프레임 전체를 메모리에 보관하지 않는다. 취소 시 원자 저장 임시 파일을 정리하며, 앱 종료는 진행 중인 GIF 변환 취소를 기다린다. GIF 미리보기는 한 장의 WriteableBitmap을 재사용한다.
