# Medical Form Analyzer: 휴먼 팩터 기반 의료기기 검사 성적서 분석 시스템

## 개요

ISO 13485, MDSAP, CE MDR, KGMP, FDA 규정을 준수하는 의료기기 제조기업의 검사 성적서를 인지 공학(Cognitive Engineering) 및 휴먼 팩터(Human Factors) 관점에서 분석하여, 휴먼 에러 발생 위험을 예측하고 문서 개선을 제안하는 혁신적인 시스템입니다.

## 핵심 기능

### 1. 문서 구조 분석 (Document Layout Analysis)
- **기술**: LayoutParser, Tesseract OCR, DocTR
- **기능**: 표, 입력 필드, 체크박스, 서명란 자동 인식 및 분류
- **출력**: 문서 요소의 좌표, 타입, 밀집도(Density) 정보

### 2. 시각적 주의력 예측 (Visual Attention Modeling)
- **기술**: DeepGaze III, Saliency Maps, Eye Tracking Simulation
- **이론**: F-Pattern, Z-Pattern, Gutenberg Diagram
- **출력**: 시선 이동 경로, 시선 머무름 시간, 주의력 히트맵

### 3. 휴먼 에러 위험 분석 (Human Error Risk Analysis)
- **이론**: Fitts's Law, Gestalt Principles, Cognitive Load Theory, Vigilance Decrement
- **모델**: Swiss Cheese Model, HFACS (Human Factors Analysis and Classification System)
- **출력**: 위험도 점수, 에러 발생 확률, 취약 구역 식별

### 4. 종합 평가 및 개선 제안
- **평가 항목**:
  - 시각적 명확성 (Visual Clarity Score)
  - 인지 부하 (Cognitive Load Score)
  - 에러 예방 설계 (Error Prevention Score)
  - 작업 효율성 (Workflow Efficiency Score)
  - 규정 준수 (Compliance Score)
- **출력**:
  - 종합 점수 (0-100점)
  - 강점 및 약점 분석
  - 구체적 개선 제안
  - 우선순위별 액션 플랜

### 5. 시각화 및 오버레이
- **Layer 1**: Attention Heatmap (주의력 분포)
- **Layer 2**: Gaze Path Trajectory (시선 이동 경로)
- **Layer 3**: Risk Zones (고위험 구역)
- **Layer 4**: Improvement Suggestions (개선 제안 표시)

## 이론적 배경

### 시선 추적 패턴
- **F-Pattern**: 텍스트 중심 문서의 전형적인 읽기 패턴
- **Z-Pattern**: 시각적 요소가 많은 문서의 스캐닝 패턴
- **Gutenberg Diagram**: 4분면 주의력 분포 (Primary Optical Area, Blind Spots)

### 인지 공학 원리
- **Fitts's Law**: 목표물까지의 거리와 크기가 동작 정확도에 미치는 영향
- **Gestalt Principles**: 근접성, 유사성, 연속성 등 시각적 그룹화 원리
- **Cognitive Load Theory**: 외생적 부하(Extraneous Load) 최소화 설계

### 휴먼 에러 모델
- **Swiss Cheese Model**: 다층 방어의 구멍이 정렬될 때 에러 발생
- **HFACS**: 4단계 에러 분류 (조직적, 감독적, 전제조건, 불안전 행동)
- **Vigilance Decrement**: 15분 이상 반복 작업 시 주의력 저하

## 설치

```bash
# 기본 설치
pip install -r requirements.txt

# 딥러닝 모델 다운로드
python models/download_models.py

# 개발 모드 설치
pip install -e .
```

## 빠른 시작

```python
from src.main import MedicalFormAnalyzer

# 분석기 초기화
analyzer = MedicalFormAnalyzer()

# 문서 분석
result = analyzer.analyze("path/to/inspection_form.pdf")

# 결과 시각화
result.visualize(output_path="analysis_result.png")

# 리포트 생성
result.generate_report(output_path="report.pdf")
```

## GUI 실행

```bash
# Streamlit 웹 인터페이스
streamlit run src/gui/streamlit_app.py
```

## 프로젝트 구조

```
medical-form-analyzer/
├── src/
│   ├── document_analyzer/      # 문서 구조 분석
│   ├── attention_model/         # 시각적 주의력 모델링
│   ├── risk_analyzer/           # 휴먼 에러 위험 분석
│   ├── evaluation/              # 종합 평가 및 점수 계산
│   ├── visualization/           # 결과 시각화
│   └── gui/                     # 사용자 인터페이스
├── models/                      # 사전 학습 모델
├── data/                        # 샘플 데이터
├── tests/                       # 단위 테스트
└── docs/                        # 문서
```

## 주요 의존성

- **Document Analysis**: layoutparser, pytesseract, doctr, opencv-python
- **Deep Learning**: torch, torchvision, transformers
- **Saliency Models**: DeepGaze (custom implementation)
- **Visualization**: matplotlib, seaborn, plotly, PIL
- **GUI**: streamlit, gradio
- **Scientific Computing**: numpy, scipy, scikit-learn, pandas

## 활용 사례

### 의료기기 제조 현장
- 스텐트, 임플란트 등 고위험 의료기기 검사 성적서 최적화
- 작업자 교육용 자료 개발
- 규제 기관 제출용 품질 문서 개선

### 품질 관리 부서
- 기존 양식의 휴먼 에러 위험 평가
- 신규 양식 설계 시 사전 검증
- CAPA (Corrective and Preventive Action) 근거 자료

### 규제 준수
- FDA 483 Observation 예방
- ISO 13485 Audit 대비
- MDSAP, CE MDR 문서 품질 향상

## 연구 기반 Sources

본 프로젝트는 다음 학술 연구 및 표준을 기반으로 개발되었습니다:

### Eye Tracking & Visual Attention
- [Visual Hierarchy: Organizing content to follow natural eye movement patterns (IxDF)](https://www.interaction-design.org/literature/article/visual-hierarchy-organizing-content-to-follow-natural-eye-movement-patterns)
- [F-Shape Pattern And How Users Read (Smashing Magazine, 2024)](https://www.smashingmagazine.com/2024/04/f-shape-pattern-how-users-read/)
- [The Science of Eye Tracking (Acclaim)](https://acclaim.agency/blog/the-science-of-eye-tracking-where-users-really-look-on-your-website)

### Saliency Prediction
- [DeepGaze IIE: Calibrated prediction in and out-of-domain (ICCV 2021)](https://openaccess.thecvf.com/content/ICCV2021/papers/Linardos_DeepGaze_IIE_Calibrated_Prediction_in_and_Out-of-Domain_for_State-of-the-Art_Saliency_ICCV_2021_paper.pdf)
- [DeepGaze III: Modeling free-viewing human scanpaths with deep learning (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC9055565/)
- [Deep saliency models learn low-, mid-, and high-level features (Scientific Reports)](https://www.nature.com/articles/s41598-021-97879-z)

### Medical Device Human Factors
- [FDA human factors engineering for medical devices (Johner Institute)](https://blog.johner-institute.com/iec-62366-usability/fda-human-factors-engineering/)
- [Applying Human Factors and Usability Engineering to Medical Devices (FDA)](https://www.fda.gov/regulatory-information/search-fda-guidance-documents/applying-human-factors-and-usability-engineering-medical-devices)
- [US FDA sets 2026 timeline for medical device QMSR rollout (Emergo by UL)](https://www.emergobyul.com/news/us-fda-incorporates-iso-13485-within-its-qmsr-final-rule)

### Cognitive Load Theory
- [A critical analysis of cognitive load measurement methods (arXiv, 2024)](https://arxiv.org/abs/2402.11820)
- [Cognitive Load Theory in UI Design (Aufait UX)](https://www.aufaitux.com/blog/cognitive-load-theory-ui-design/)

### Fitts's Law
- [Fitts's Law: The Importance of Size and Distance in UI Design (IxDF)](https://www.interaction-design.org/literature/article/fitts-s-law-the-importance-of-size-and-distance-in-ui-design)
- [Fitts's Law and Its Applications in UX (NN/G)](https://www.nngroup.com/articles/fitts-law/)

### Gestalt Principles
- [7 Gestalt Principles of Visual Perception (UserTesting, 2024)](https://www.usertesting.com/blog/gestalt-principles)
- [What are the Gestalt Principles? (IxDF, 2025)](https://www.interaction-design.org/literature/topics/gestalt-principles)
- [Proximity Principle in Visual Design (NN/G)](https://www.nngroup.com/articles/gestalt-proximity/)

### Reading Patterns
- [Gutenberg Diagram — Why you should know it and use it (Medium)](https://medium.com/user-experience-3/the-gutenberg-diagram-in-web-design-e5347c172627)
- [3 Design Layouts: Gutenberg Diagram, Z-Pattern, And F-Pattern (Vanseo Design)](https://vanseodesign.com/web-design/3-design-layouts/)

### Human Error Models
- [Understanding the "Swiss Cheese Model" and Its Application to Patient Safety (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC8514562/)
- [Application of HFACS Model to the Prevention of Medical Errors (PMC, 2024)](https://pmc.ncbi.nlm.nih.gov/articles/PMC10803676/)
- [A novel framework for HFACS-MES (PMC, 2024)](https://pmc.ncbi.nlm.nih.gov/articles/PMC10889608/)

### Vigilance & Habituation
- [Vigilance Decrement and Enhancement Techniques: A Review (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC6721323/)
- [Beyond detection rate: understanding the vigilance decrement (Frontiers, 2024)](https://www.frontiersin.org/journals/cognition/articles/10.3389/fcogn.2024.1505046/full)
- [The strategic allocation theory of vigilance (WIREs, 2024)](https://wires.onlinelibrary.wiley.com/doi/10.1002/wcs.1693)

### Visual Clutter Measurement
- [Measuring visual clutter (ARVO Journals)](https://jov.arvojournals.org/article.aspx?articleid=2122001)
- [Feature congestion: A measure of visual clutter (ResearchGate)](https://www.researchgate.net/publication/239450341_Feature_congestion_A_measure_of_visual_clutter)

### Document Analysis Tools
- [LayoutParser: A Unified Toolkit for Deep Learning Based Document Image Analysis (GitHub)](https://github.com/Layout-Parser/layout-parser)
- [Top 8 OCR Libraries in Python (Analytics Vidhya, 2024)](https://www.analyticsvidhya.com/blog/2024/04/ocr-libraries-in-python/)

## 라이선스

MIT License

## 기여

이슈 및 풀 리퀘스트를 환영합니다. 대규모 변경의 경우 먼저 이슈를 열어 논의해 주세요.

## 문의

프로젝트 관련 문의사항은 이슈 트래커를 이용해 주세요.

---

**주의**: 본 시스템은 의료기기 검사 성적서의 설계 품질을 평가하는 도구이며, 실제 검사 결과의 정확성이나 제품의 안전성을 보증하지 않습니다. 최종 검토는 반드시 자격을 갖춘 품질 관리 전문가가 수행해야 합니다.
