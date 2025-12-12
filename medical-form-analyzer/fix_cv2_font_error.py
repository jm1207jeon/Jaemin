#!/usr/bin/env python3
"""
OpenCV FONT_HERSHEY_BOLD 에러 자동 수정 스크립트
"""

import os
from pathlib import Path

print("=" * 80)
print("Medical Form Analyzer - OpenCV 폰트 에러 수정")
print("=" * 80)
print()

# overlay_renderer.py 파일 경로
file_path = Path("src/visualization/overlay_renderer.py")

if not file_path.exists():
    print(f"❌ 파일을 찾을 수 없습니다: {file_path}")
    print("   현재 디렉토리를 확인하세요.")
    exit(1)

print(f"📄 파일 읽는 중: {file_path}")

# 파일 읽기
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 백업 생성
backup_path = file_path.with_suffix('.py.backup')
with open(backup_path, 'w', encoding='utf-8') as f:
    f.write(content)
print(f"💾 백업 생성: {backup_path}")

# FONT_HERSHEY_BOLD를 FONT_HERSHEY_SIMPLEX로 교체
if 'FONT_HERSHEY_BOLD' in content:
    content = content.replace('cv2.FONT_HERSHEY_BOLD', 'cv2.FONT_HERSHEY_SIMPLEX')

    # 파일 쓰기
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)

    print("✅ 수정 완료!")
    print("   cv2.FONT_HERSHEY_BOLD → cv2.FONT_HERSHEY_SIMPLEX")
else:
    print("ℹ️  FONT_HERSHEY_BOLD가 이미 수정되었거나 존재하지 않습니다.")

print()
print("=" * 80)
print("다음 단계:")
print("1. streamlit run src/gui/streamlit_app.py")
print("2. 문서를 업로드하고 다시 분석 시도")
print("=" * 80)
