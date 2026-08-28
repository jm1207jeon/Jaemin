# QMS Document Analyzer

의료기기 QMS(품질경영시스템) 문서 체계를 네트워크로 관리·탐색·검증하는 **C# 데스크톱 애플리케이션**입니다.
문서 등록 이력 대장(F401-09)에서 생성한 문서 네트워크(품질매뉴얼 → 절차서 → 지침/작업표준 → 양식)를 기반으로,
실제 수행 기록 폴더를 연결해 드릴다운 탐색·추적성 계보·정합성 검토·개정 영향 분석을 제공합니다.

## 주요 기능

| 화면 | 기능 |
|---|---|
| **문서 라이브러리** | 등록 문서 377건(+외부 규격·규제·파생 기술문서) 검색·필터, Rev/적용일/관리부서/기록 수 |
| **네트워크 그래프** | Force-directed 문서 관계 그래프. 레이어 토글(절차서/지침/양식/규격·규제/기술문서), 드래그·줌·클릭 선택, 이웃 강조, 우측 상세 패널 |
| **기록 탐색** | 좌→우 Miller Columns 드릴다운: 절차서 → 그룹 → 실행 단위 → 기록 테이블(Rev·수행일·작성/검토/승인·상태) |
| **역추적 · 검토** | 기록 → 양식 → 절차서 → 매뉴얼 → 규격의 추적성 계보, **[검토하기]** 정합성 점검(인용 Rev 불일치·서명 완결성·수행 주기 등), 개정 영향 체크리스트 자동 생성 |
| **심사 대비 대시보드** | 타입 분포, 규격·규제 커버리지, 갭·무결성 경고, 감사 추적(audit trail) |
| **설정** | 폴더 연결(통합 루트 + 노드별 지정), Claude API 연결(AI 문안 대조·요약) |

### 규제 커버리지

ISO 13485, KGMP(MFDS), MDSAP(미국·캐나다·브라질·호주·일본), FDA 21 CFR 820, EU MDR을 포함한
규제→담당 절차 매핑이 시드 네트워크에 내장되어 있습니다.

### 폴더 연결과 기록 자동 수집

- **통합 루트**: 폴더 하나를 지정하면 재귀 스캔 후 파일명·폴더명에서 문서번호(QP-xxx, F706-01 등)·일자·Rev를 패턴 추출해 자동 귀속
- **노드별 지정**: 특정 문서(예: QP-706)에 전용 폴더를 바인딩 — 우선순위: 노드별 > 통합 루트
- 매칭 실패분은 "미분류 인박스"로 분리

### AI 분석 (선택)

설정에서 Anthropic API 키를 입력하면 활성화됩니다 (미입력 시 규칙 기반 로컬 분석만 동작):
- **검토하기 보강**: 기록 본문 vs 상위 절차·지침 문안 대조 (판정기준·수치·요구항목)
- **문서 요약**
- 키는 이 PC의 로컬 설정 파일(`%AppData%/QmsAnalyzer/config.json`)에만 저장됩니다.

## 실행

### 배포본 (권장)
GitHub Actions가 커밋마다 자동 빌드합니다:
- **Actions 탭 → 최신 워크플로 → Artifacts → `QmsAnalyzer-win-x64`** 다운로드 후 `QmsAnalyzer.exe` 실행 (설치 불필요, self-contained)
- `v*` 태그를 푸시하면 GitHub Release가 자동 생성됩니다

### 소스 빌드
```bash
dotnet build            # 전체 빌드
dotnet test             # 단위 + 헤드리스 UI 테스트
dotnet run --project src/QmsAnalyzer.App
```
요구사항: .NET 8 SDK. Windows/macOS/Linux 모두 지원(Avalonia).

## 프로젝트 구조

```
src/QmsAnalyzer.Core/        도메인 로직 (UI 독립)
  Models/                    노드·엣지·기록·설정 모델
  Services/
    SeedNetworkService       임베디드 seedNetwork.json 로드·인덱스
    HierarchyService         문서번호 → 계층/계보 (F805-05-01 → SOP-805-05 → QP-805 → QM-001)
    FolderScanService        폴더 스캔·기록 메타데이터 추출
    RulesEngine              정합성 점검 + 개정 영향 규칙
    AiService                Claude API (문안 대조·요약)
    TextExtractService       docx/xlsx/txt 본문 추출
    XlsxImportService        문서 등록 대장(F401-09) 재임포트
    ConfigService/AuditService  로컬 설정 · 감사 추적(JSONL)
src/QmsAnalyzer.App/         Avalonia UI (뷰 6종 + 커스텀 그래프 컨트롤)
  Styles/Tokens.axaml        디자인 토큰 — 룩앤필 교체는 이 파일만 수정
tests/                       Core 단위 테스트 + 헤드리스 UI 스모크 테스트(스크린샷 캡처)
tools/build_network.py       문서 등록 대장 xlsx → 네트워크 시드 재생성
```

## 시드 네트워크 갱신

문서 등록 이력 대장이 개정되면:
```bash
python3 tools/build_network.py <문서등록이력대장.xlsx> src/QmsAnalyzer.Core/Assets/seedNetwork.json
dotnet build
```

## 로드맵

- [ ] 기록 파일 내부 서명란 파싱(양식별 셀 좌표 등록) — 현재는 파일명 패턴 기반
- [ ] PDF/HWP 본문 추출
- [ ] FileSystemWatcher 실시간 폴더 감시
- [ ] 개정 워크플로(체크리스트 진행 상태 저장, 전자서명)
- [ ] 그래프 미니맵·클러스터 헐

---
의료기기 품질관리 실무(ISO 13485 · KGMP · MDSAP · FDA QMSR · EU MDR)를 위한 도구입니다.
