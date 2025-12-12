# Quick Start Guide

## 설치

### 1. 의존성 설치

```bash
# 기본 패키지 설치
pip install -r requirements.txt

# 또는 개발 모드로 설치
pip install -e .
```

### 2. Tesseract OCR 설치 (시스템 레벨)

#### Ubuntu/Debian
```bash
sudo apt-get update
sudo apt-get install tesseract-ocr tesseract-ocr-kor
```

#### macOS
```bash
brew install tesseract tesseract-lang
```

#### Windows
- [Tesseract GitHub Releases](https://github.com/tesseract-ocr/tesseract/releases)에서 다운로드
- 설치 후 PATH 환경 변수에 추가

### 3. 모델 준비

```bash
python models/download_models.py
```

## 사용 방법

### 방법 1: Streamlit 웹 인터페이스 (권장)

```bash
streamlit run src/gui/streamlit_app.py
```

브라우저에서 자동으로 열립니다. 문서를 업로드하고 "Start Analysis" 버튼을 클릭하세요.

### 방법 2: Python 코드

```python
from src.main import MedicalFormAnalyzer

# 분석기 초기화
analyzer = MedicalFormAnalyzer()

# 문서 분석
result = analyzer.analyze("path/to/your/form.png")

# 결과 확인
print(f"Overall Score: {result.scores['overall_score']:.1f}/100")
print(f"Grade: {result.scores['grade']}")

# 시각화 저장
result.visualize("output_composite.png", "composite")
result.visualize("output_heatmap.png", "heatmap")
result.visualize("output_risk_zones.png", "risk_zones")
result.visualize("output_dashboard.png", "dashboard")

# 리포트 생성
result.generate_report("report.txt")
```

### 방법 3: 커맨드 라인

```bash
python -m src.main path/to/your/form.png
```

결과 파일이 현재 디렉토리에 생성됩니다:
- `analysis_composite.png` - 종합 분석 결과
- `analysis_heatmap.png` - 주의력 히트맵
- `analysis_risk_zones.png` - 위험 구역 표시
- `analysis_dashboard.png` - 점수 대시보드
- `analysis_report.txt` - 상세 텍스트 리포트

## 예제 실행

```bash
# 기본 예제
python examples/basic_analysis.py

# 샘플 문서 필요 (data/sample_forms/ 에 배치)
```

## 출력 해석

### 종합 점수 (Overall Score)
- **90-100 (A)**: 우수 - 최소한의 개선 필요
- **75-89 (B)**: 양호 - 경미한 개선 권장
- **60-74 (C)**: 보통 - 일부 개선 필요
- **40-59 (D)**: 미흡 - 상당한 개선 필요
- **0-39 (F)**: 불량 - 전면 재설계 권장

### 위험 수준 (Risk Level)
- **Low**: 휴먼 에러 위험 낮음
- **Medium**: 주의 필요, 개선 권장
- **High**: 에러 발생 가능성 높음, 개선 필수
- **Critical**: 즉각적인 개선 필요

### 카테고리별 점수
1. **Visual Clarity (시각적 명확성)**: 정보의 가시성, 맹점 최소화
2. **Cognitive Load (인지 부하)**: 정보 복잡도, 처리 용이성
3. **Error Prevention (에러 예방)**: 휴먼 에러 방지 설계
4. **Workflow Efficiency (작업 효율성)**: 논리적 흐름, 작업 시간
5. **Compliance (규정 준수)**: 필수 항목 포함, 규제 요구사항

## 개선 제안 적용

시스템이 제공하는 개선 제안을 우선순위에 따라 적용하세요:

1. **Critical Priority**: 즉시 적용 (에러 발생 위험 높음)
2. **High Priority**: 빠른 시일 내 적용 (품질 개선 효과 큼)
3. **Medium Priority**: 차기 개정 시 적용
4. **Low Priority**: 장기적 개선 고려

## 문제 해결

### "Tesseract not found" 오류
```bash
# Tesseract 설치 확인
tesseract --version

# 설치되지 않았다면 위의 "Tesseract OCR 설치" 참조
```

### "LayoutParser model load failed" 경고
- 정상 동작: 시스템이 OpenCV 기반 fallback 사용
- 프로덕션 환경에서는 LayoutParser 모델 설치 권장

### 메모리 부족
- 큰 이미지(>4000px)는 리사이즈 후 분석
- 또는 config.yaml에서 해상도 설정 조정

### 분석 시간이 오래 걸림
- 첫 실행: 모델 로딩으로 1-2분 소요 (정상)
- 이후 실행: 문서당 30초~1분 소요

## 설정 커스터마이징

`config/config.yaml` 파일을 수정하여 분석 파라미터 조정 가능:

```yaml
# 예: Saliency map 해상도 조정
attention_model:
  saliency_resolution: [384, 512]  # 낮추면 빠르지만 정확도 감소

# 위험 분석 임계값 조정
risk_analysis:
  risk_levels:
    low: [0, 30]
    medium: [30, 60]
    high: [60, 80]
    critical: [80, 100]
```

## 다음 단계

- [이론적 배경](docs/theory_background.md) 읽기
- [상세 API 문서](docs/api_reference.md) 참조 (준비 중)
- 실제 검사 성적서로 테스트
- 개선 제안 적용 후 재분석하여 효과 검증

## 지원

- 이슈: GitHub Issues
- 문서: [README.md](README.md)
- 이론: [theory_background.md](docs/theory_background.md)

---

**면책 조항**: 본 시스템은 문서 품질 평가 도구이며, 실제 제품 안전성이나 검사 결과의 정확성을 보증하지 않습니다. 최종 검토는 자격을 갖춘 품질 관리 전문가가 수행해야 합니다.
