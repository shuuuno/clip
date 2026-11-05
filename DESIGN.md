---
name: Clip
description: 캡처 즉시 복사와 빠른 주석·공유를 위한 Windows 앱
colors:
  background: "#16191F"
  panel: "#1D2129"
  elevated: "#282D37"
  canvas: "#101217"
  text: "#F1F3F7"
  muted: "#A4AEBD"
  line: "#363E4B"
  primary: "#2869E8"
  selection: "#21395C"
  selection-text: "#9BC1FF"
typography:
  body:
    fontFamily: "Segoe UI, Malgun Gothic"
    fontSize: "13px"
rounded:
  control: "6px"
  input: "5px"
spacing:
  xs: "4px"
  sm: "8px"
  base: "12px"
  md: "16px"
  lg: "20px"
  xl: "24px"
  xxl: "28px"
---

# Clip UI 기준

기존 다크·파랑 색상 체계를 유지하면서 도구 밀도와 WPF 컨트롤 일관성을 개선한다. 작업 모드는 Operate이며 주 경로는 캡처 즉시 복사 → 주석 → 복사·저장이다.

## 토큰

앞머리 토큰의 px 표기는 문서 교환용이며 WPF 구현에서는 동일 수치의 DIP를 사용한다. 글자 크기는 메타 11·안내 12·본문/버튼 13·구역 14·제목 23~24 DIP다. 주요 파랑은 새 캡처와 복사에 사용하고, 선택 도구에는 약한 선택 배경을 사용한다.

토큰은 Theme.cs, 컨트롤 상태는 Views/Controls.xaml이 기준이다. 템플릿은 기존 WPF 버튼·콤보·입력·체크박스·목록·스크롤의 키보드 및 UI Automation 동작을 유지한다. 포커스는 밝은 파랑 경계, 선택 도구는 약한 파랑 배경으로 표시한다. 아이콘은 동일한 1.6 선 굵기의 직접 작성한 기하 경로이며 별도 외부 자산은 없다.

## 화면 구조

- 상단: 작은 Clip 표제, 설정·이미지 열기·녹화·새 캡처.
- 왼쪽: 폭 208 DIP 라이브러리. 작은 새로 고침, 썸네일·파일명·형식·날짜, 저장 경로는 말줄임·툴팁.
- 편집: 폭 120 DIP 세로 도구, 상단 속성·Undo/Redo와 크기·줌, 중앙 캔버스.
- 하단: 상태 안내와 적용·삭제·복사·PNG 저장. 복사는 주요 완료 동작이다.
- 설정: 시작 방식, 저장 폴더, 단축키. 본문은 스크롤 가능하고 오류·저장·취소는 하단에 유지한다.

## 확인 기준

960×640 DIP 및 기본 창에서 도구·완료 동작을 찾을 수 있어야 한다. 긴 경로, 빈 라이브러리, 이미지·영상·GIF, 선택·오류·비활성·키보드 포커스를 실제 렌더로 확인한다. 대비나 기능 검증을 이미지의 인상만으로 대체하지 않는다.
