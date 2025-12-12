# Tesseract OCR 설치 가이드 (Windows)

## 📥 설치 방법

### 방법 1: 설치 프로그램 다운로드 (추천)

1. **다운로드 페이지 접속**:
   - https://github.com/UB-Mannheim/tesseract/wiki

2. **설치 파일 다운로드**:
   - **64비트 Windows**: `tesseract-ocr-w64-setup-5.3.3.20231005.exe`
   - **32비트 Windows**: `tesseract-ocr-w32-setup-5.3.3.20231005.exe`

3. **설치**:
   - 다운로드한 `.exe` 파일 실행
   - 설치 경로: `C:\Program Files\Tesseract-OCR` (기본값 추천)
   - **중요**: "Additional language data" 선택 시 **Korean** 체크!

4. **환경 변수 설정**:
   ```
   시작 → "환경 변수" 검색
   → 시스템 변수 → Path → 편집
   → 새로 만들기 → C:\Program Files\Tesseract-OCR 추가
   → 확인
   ```

5. **설치 확인**:
   ```powershell
   # 새 명령 프롬프트를 열고:
   tesseract --version
   ```

   출력 예시:
   ```
   tesseract v5.3.3.20231005
   ```

---

### 방법 2: Chocolatey 사용 (개발자용)

```powershell
# PowerShell (관리자 권한)에서:
choco install tesseract

# 한글 언어팩 추가
choco install tesseract-language-korean
```

---

## 🔧 문제 해결

### "tesseract is not recognized" 에러

**원인**: PATH 환경 변수에 Tesseract가 추가되지 않음

**해결**:
1. Tesseract 설치 경로 확인: `C:\Program Files\Tesseract-OCR`
2. 환경 변수에 추가 (위의 4단계)
3. **명령 프롬프트를 완전히 닫고 다시 열기**

---

### Python에서 Tesseract 경로 수동 지정

환경 변수 설정이 안 되면, 코드에서 직접 지정:

```python
# src/document_analyzer/ocr_engine.py 파일에서
import pytesseract

# 이 줄을 추가:
pytesseract.pytesseract.tesseract_cmd = r'C:\Program Files\Tesseract-OCR\tesseract.exe'
```

---

## ✅ 설치 후 확인

```powershell
cd C:\medical-form-analyzer

# Python에서 Tesseract 인식 확인
python -c "import pytesseract; print(pytesseract.get_tesseract_version())"
```

성공 시 출력:
```
<tesseract.pytesseract.TesseractVersion object>
```

---

## 🎯 Tesseract 없이 사용하기

Tesseract 설치가 어렵다면, **OCR 기능 없이** 사용 가능합니다:

- ✅ 레이아웃 분석 작동
- ✅ 시선 추적 작동
- ✅ 위험도 분석 작동
- ⚠️ 텍스트 추출은 안 됨 (OCR 필요)

**영향**: 텍스트 기반 분석이 제한적이지만, 시각적 분석은 정상 작동합니다.

---

## 📞 추가 도움

- **Tesseract 공식 문서**: https://tesseract-ocr.github.io/
- **Windows 설치 가이드**: https://github.com/UB-Mannheim/tesseract/wiki
