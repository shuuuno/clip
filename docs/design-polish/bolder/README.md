# Clip Bolder

2026-11-06. [요청 문서](../../brief_design_bolder.md)에 따라 `design-polish` 브랜치에서 Impeccable **bolder → polish**를 적용했다. 비교 기준은 직전 polish 커밋 `39d2340`과 상위 폴더의 기존 after 이미지다.

| 영역 | 이전 | 이번 변경 |
|---|---|---|
| 도구 | 136 DIP 세로 열 | 72×52 DIP 가로 도구 8개. 주석/이미지 변경 그룹 구분 |
| 라이브러리 | 208 DIP, 큰 썸네일 카드 | 232 DIP, 작은 썸네일 옆 파일명·날짜의 행 목록 |
| 패널 | 비슷한 명도의 다크 패널 | 어두운 라이브러리·캔버스, 밝은 리본·완료 영역 |
| 작업 영역 | 속성/줌 두 행, 전체 폭 푸터 | 속성·Undo/Redo·줌 한 행, 편집 영역 안의 44 DIP 완료 버튼 |
| 빈 화면 | 24 DIP 제목과 안내 | 32 DIP 제목, 기존 캡처 아이콘, 실제 단축키 강조 |
| 설정/텍스트 | 작은 제목과 단일 표면 | 제목/본문/완료 영역 분리, 큰 구역 제목과 입력 공간 |

기존 Windows 글꼴·파랑·기능·단축키를 유지했다. 외부 앱 코드·자산·폰트, 그라데이션·글래스·장식 애니메이션은 추가하지 않았다. 첫 렌더에서 리본이 차지하는 높이가 커 polish 단계에서 52 DIP로 낮추고 줌을 속성 행에 합쳤다. 구현 의도는 [DESIGN.md](../../../DESIGN.md)에 반영했다.

검증: 빌드 경고/오류 0, 기존 기능 **25개 시나리오**, 기존 **50개 WPF 검사** 통과. 50개 중 28개는 최소 창 버튼 쌍별 겹침 검사다. 기본 1220×800/최소 960×640 DIP, DPI 168에서 같은 8개 상태를 촬영했다. 상세 결과는 [verification.json](verification.json), 원본 검증 출력은 `artifacts/ui/bolder-polished`다. 기능 시나리오 증거는 `artifacts/tests/83656cc915e74e98a15eaf1d9c50cc17`에 있다.

독립 Hallmark finish review(`gpt-6-astra`, `high`) 판정은 **ship**이다. 직전과 구조 차이가 분명하고 8개 상태에서 잘림·겹침·누락이 없음을 확인했다. 교환관계도 있다. 행 목록은 이전보다 이미지 미리보기·파일명이 작고, 가로 리본 때문에 최소 창의 화면 맞춤 이미지가 작아졌다. 가로 작업 공간과 목록 밀도를 얻기 위한 선택이며, 그라데이션·장식을 늘리는 방식은 사용하지 않았다. 검토자는 제공된 테스트 결과를 독립 재실행하지 않았다.

PNG는 실제 제품 WPF 컨트롤의 렌더 캡처다. 데스크톱 사진·외부 마우스/키보드 실기와 구분하며, WPF 내부 실제 포커스 획득은 검사했다. 웹 전용 탐지기의 WPF 미지원을 자동 통과로 처리하지 않았다. merge·push·배포·publish는 수행하지 않았다.

Grok Bot 보고 결과는 [수신 기록](grokbot-report.md)에 남긴다.

| 상태 | 직전 polish | 이번 Bolder |
|---|---|---|
| 빈 화면 | [이전](../01-empty-after.png) | [01-empty-after.png](01-empty-after.png) |
| 설정 | [이전](../02-settings-after.png) | [02-settings-after.png](02-settings-after.png) |
| 설정 오류 | [이전](../03-settings-error-after.png) | [03-settings-error-after.png](03-settings-error-after.png) |
| 편집 | [이전](../04-editor-after.png) | [04-editor-after.png](04-editor-after.png) |
| 최소 창 | [이전](../05-minimum-after.png) | [05-minimum-after.png](05-minimum-after.png) |
| 텍스트 입력 | [이전](../06-text-after.png) | [06-text-after.png](06-text-after.png) |
| 저장 후 | [이전](../07-saved-after.png) | [07-saved-after.png](07-saved-after.png) |
| 포커스 | [이전](../08-focus-after.png) | [08-focus-after.png](08-focus-after.png) |
