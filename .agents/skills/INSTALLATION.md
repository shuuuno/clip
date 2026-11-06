# 프로젝트 전용 디자인 스킬

2026-11-06, `docs/brief_design_polish.md`에 따라 이 프로젝트의 `.agents/skills`에만 설치했다. 사용자 전역 스킬·다른 앱은 수정하지 않았다. SDK·원본 checkout·검증용 프로젝트는 추적하지 않는 `.tools/design-polish`에 있다. 제품 런타임 의존성은 추가하지 않았다.

| 스킬 | 공식 원본 | 고정 커밋 | 라이선스 |
|---|---|---|---|
| Impeccable 4.8.0 | https://github.com/pbakaus/impeccable | c3bb418ee38ee49a6b999b7752c1368f3123a2bd | Apache-2.0, impeccable/LICENSE |
| Hallmark 2.0.0 | https://github.com/Nutlope/hallmark | 407fa689f4a276f2fd389208d271d0fef08f5058 | MIT, hallmark/LICENSE |

Impeccable의 기존 0.1.18 엔진을 읽어 사용한 뒤 `.tools/design-polish/engine/impeccable.exe`로 복사했다. 이후 실행은 `IMPECCABLE_BIN`을 그 로컬 경로로 지정했다. 새 엔진을 전역에 설치하지 않았다. 스킬에 포함된 예제·이미지는 스킬 참고 자료이며 제품 코드·자산으로 가져오지 않았다.

Impeccable `reference/extract.md`, `harden.md`, `optimize.md`의 기존 행 끝 공백과 마지막 빈 줄만 정리했다. 그 밖의 스킬 본문·스크립트는 원본 그대로다.

두 도구의 HTML/CSS 탐지기는 WPF를 지원하지 않는다. 네이티브 앱을 웹으로 바꾸거나 잘못된 `ios`/`android` 플랫폼 값으로 등록하지 않는다. 검토 적용과 자동 실행의 한계는 `docs/design-polish/README.md`에 기록한다. Taste Skill은 설치·적용하지 않았다.
