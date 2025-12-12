#!/usr/bin/env python3
"""간단한 데모 스크립트"""

import cv2
import numpy as np
import os
from pathlib import Path

print("=" * 80)
print("Medical Form Analyzer - Quick Demo")
print("=" * 80)

# 이미지 로드
image_path = "data/sample_forms/test_form.png"
print(f"\n📄 문서 로드: {image_path}")

img = cv2.imread(image_path)
if img is None:
    print("❌ 이미지를 로드할 수 없습니다.")
    exit(1)

print(f"✅ 로드 성공! (크기: {img.shape[1]}x{img.shape[0]})")

# 간단한 분석
print("\n🔍 기본 분석 수행 중...")

gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
edges = cv2.Canny(gray, 50, 150)
edge_density = np.sum(edges > 0) / edges.size

contours, _ = cv2.findContours(edges, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)

checkboxes = []
for contour in contours:
    x, y, w, h = cv2.boundingRect(contour)
    if 15 <= w <= 35 and 15 <= h <= 35:
        checkboxes.append((x, y, w, h))

print(f"\n📊 분석 결과:")
print(f"  • Edge Density: {edge_density:.3f}")
print(f"  • 검출된 체크박스: {len(checkboxes)}개")

# 결과 시각화
output = img.copy()
for (x, y, w, h) in checkboxes[:12]:  # 처음 12개만 표시
    cv2.rectangle(output, (x, y), (x+w, y+h), (0, 255, 0), 3)

os.makedirs('data/test_results', exist_ok=True)
output_path = "data/test_results/demo_result.png"
cv2.imwrite(output_path, output)

print(f"\n✅ 데모 완료!")
print(f"📁 결과: {output_path}")
print("\n" + "=" * 80)
print("다음 단계:")
print("  1. 전체 분석: python -m src.main data/sample_forms/test_form.png")
print("  2. 웹 GUI: streamlit run src/gui/streamlit_app.py")
print("=" * 80)
