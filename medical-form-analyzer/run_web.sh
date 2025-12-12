#!/bin/bash
echo "🏥 Medical Form Analyzer 웹 인터페이스 시작 중..."
echo ""
echo "📦 필수 패키지 설치 중..."
pip install streamlit pyyaml scipy scikit-image -q 2>&1 | grep -v "WARNING:"

echo ""
echo "🚀 웹 서버 시작..."
echo "   브라우저가 자동으로 열립니다!"
echo "   수동으로 열려면: http://localhost:8501"
echo ""
streamlit run src/gui/streamlit_app.py
