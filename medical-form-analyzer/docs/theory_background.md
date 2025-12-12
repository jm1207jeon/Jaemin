# 이론적 배경 (Theoretical Background)

본 시스템은 다음의 검증된 이론과 최신 연구를 기반으로 개발되었습니다.

## 1. 시선 추적 패턴 (Eye Tracking Patterns)

### F-Pattern
- **연구**: Smashing Magazine (2024), Interaction Design Foundation
- **내용**: 텍스트 중심 문서에서 사용자는 F자 형태로 시선을 이동
- **적용**: 중요한 정보를 F-패턴의 상단과 좌측에 배치

### Z-Pattern
- **연구**: NN/G (Nielsen Norman Group)
- **내용**: 시각적 요소가 많은 문서에서 Z자 형태의 시선 이동
- **적용**: 시각적 랜딩 페이지나 폼에 적합한 레이아웃 설계

### Gutenberg Diagram
- **연구**: Visual Hierarchy research
- **내용**: 문서를 4분면으로 나누어 주의력 분포 예측
  - Primary Optical Area (좌상단): 최고 주의력
  - Strong Deceleration (우상단): 중간 주의력
  - Weak Fallow (좌하단): 맹점(blind spot)
  - Terminal Area (우하단): 종료 지점
- **적용**: 중요 정보는 Primary Optical Area에, 서명란은 Terminal Area에 배치

## 2. Saliency Prediction (현저성 예측)

### DeepGaze III
- **연구**: PMC 9055565 (2022), ICCV 2021
- **내용**: 딥러닝 기반 시각적 주의력 예측 모델
- **성능**: MIT300 벤치마크에서 최고 성능 (AUC 0.906)
- **적용**: 문서에서 사용자가 가장 먼저 볼 영역 예측

### Itti-Koch Model
- **연구**: "A Model of Saliency-Based Visual Attention" (1998)
- **내용**: 전통적인 saliency map 생성 모델
- **특징**: 색상, 강도, 방향성 기반의 bottom-up 주의력
- **적용**: 계산 효율적인 주의력 예측

## 3. Fitts's Law (피츠의 법칙)

### 원리
```
T = a + b * log₂(D/W + 1)
```
- T: 목표 지점까지의 이동 시간
- D: 시작점부터 목표까지의 거리
- W: 목표의 크기

### 의료기기 검사 성적서 적용
- **문제**: 작은 체크박스가 멀리 떨어져 있으면 에러 증가
- **해결**: 연속된 입력 필드 간 거리 최소화, 충분한 타겟 크기 확보

## 4. Gestalt Principles (게슈탈트 원리)

### Proximity (근접성)
- 가까이 있는 요소들을 그룹으로 인식
- **적용**: 관련 필드를 가까이 배치

### Similarity (유사성)
- 비슷한 모양/색상의 요소를 연관된 것으로 인식
- **위험**: 연속된 체크박스는 습관적 체크 유발 → Habituation Error

### Continuity (연속성)
- 자연스러운 흐름을 따라 시선 이동
- **적용**: 좌→우, 상→하의 자연스러운 순서

## 5. Cognitive Load Theory (인지 부하 이론)

### 세 가지 부하 유형
1. **Intrinsic Load (내재적 부하)**: 정보 자체의 복잡도
2. **Extraneous Load (외재적 부하)**: 디자인으로 인한 불필요한 부하
3. **Germane Load (유의미 부하)**: 학습에 필요한 부하

### 의료 문서에서의 적용
- **목표**: Extraneous Load 최소화
- **방법**:
  - 정보 밀집도 감소
  - 명확한 그룹핑
  - 적절한 폰트 크기와 대비
  - 충분한 여백(whitespace)

## 6. Visual Clutter Measurement (시각적 혼잡도 측정)

### Edge Density
- **연구**: "Measuring visual clutter" (ARVO Journals)
- **방법**: 전체 픽셀 중 edge 픽셀의 비율
- **기준**: >0.7 = 높은 혼잡도

### Feature Congestion
- **내용**: 새로운 시각적 요소를 추가하기 어려운 정도
- **측정**: 색상, 방향성, 밝기 변화의 복합도

## 7. Vigilance Decrement (경계 감소)

### 연구 결과
- **출처**: Frontiers in Cognition (2024), PMC 6721323
- **발견**:
  - 15분 이상 반복 작업 시 주의력 저하
  - 품질 검사 업무에서 에러율 증가
  - 경험자와 초보자 모두 영향 받음

### 의료기기 제조 현장 적용
- **문제**: 장시간 검사 성적서 작성 시 놓침(omission) 증가
- **대책**:
  - 작업 시간 15분 이내로 제한
  - 중요 필드에 시각적 강조
  - 정기적 휴식 권장

## 8. Swiss Cheese Model & HFACS

### Swiss Cheese Model (James Reason, 1990)
- **개념**: 사고는 여러 방어층의 구멍이 정렬될 때 발생
- **의료기기 적용**: 다층 에러 방지 시스템 설계
  - Layer 1: 문서 디자인
  - Layer 2: 시각적 경고
  - Layer 3: 검증 프로세스
  - Layer 4: 승인 절차

### HFACS (Human Factors Analysis & Classification System)
- **출처**: PMC 10803676 (2024)
- **4단계 분류**:
  1. Organizational Influences (조직적 영향)
  2. Unsafe Supervision (불안전 감독)
  3. Preconditions for Unsafe Acts (전제 조건)
  4. Unsafe Acts (불안전 행동)

## 9. Habituation Error (습관화 에러)

### 정의
- 반복된 자극에 대한 반응 감소
- 연속된 체크박스를 무의식적으로 체크하는 경향

### 실험 결과
- **기준**: 5개 이상 연속된 유사 요소에서 에러율 급증
- **메커니즘**: Goal Habituation Theory

### 대책
- 연속된 체크박스를 3-4개 이하로 제한
- 중간에 다른 유형의 입력 삽입
- 중요 항목은 시각적으로 차별화

## 10. 규제 기관 요구사항

### ISO 13485:2016
- **Section 7.3**: Design and Development
- **요구사항**: 사용성 고려, 리스크 관리

### FDA 21 CFR Part 820 (QMSR 2024)
- **Human Factors**: IEC 62366-1 준용
- **2026년 발효**: ISO 13485 통합

### EU MDR 2017/745
- **Annex I**: General Safety and Performance Requirements
- **Section 5**: Eliminate or reduce risks related to use errors

## 참고문헌

완전한 참고문헌 목록은 README.md의 "연구 기반 Sources" 섹션을 참조하세요.

---

이 이론들을 종합적으로 적용하여, 본 시스템은 의료기기 검사 성적서의 휴먼 에러 위험을 정량적으로 평가하고 구체적인 개선 방안을 제시합니다.
