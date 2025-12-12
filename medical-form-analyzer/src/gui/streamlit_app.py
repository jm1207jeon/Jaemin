"""
Streamlit Web Application
~~~~~~~~~~~~~~~~~~~~~~~~~~

Interactive web interface for Medical Form Analyzer.
"""

import streamlit as st
import cv2
import numpy as np
from PIL import Image
import sys
from pathlib import Path
import io

# Add parent directory to path
sys.path.append(str(Path(__file__).parent.parent.parent))

from src.main import MedicalFormAnalyzer


def main():
    """Main Streamlit application."""
    # Page configuration
    st.set_page_config(
        page_title="Medical Form Analyzer",
        page_icon="🏥",
        layout="wide",
        initial_sidebar_state="expanded"
    )

    # Title
    st.title("🏥 Medical Form Analyzer")
    st.markdown("### 휴먼 팩터 기반 의료기기 검사 성적서 분석 시스템")

    # Sidebar
    with st.sidebar:
        st.header("📋 About")
        st.markdown("""
        이 시스템은 의료기기 검사 성적서를 인지 공학 및 휴먼 팩터 관점에서 분석합니다.

        **분석 항목:**
        - 👁️ 시각적 주의력 패턴
        - ⚠️ 휴먼 에러 위험도
        - 🧠 인지 부하
        - 📊 종합 품질 점수
        - 💡 개선 제안
        """)

        st.markdown("---")
        st.markdown("**기반 이론:**")
        st.markdown("""
        - Fitts's Law
        - Gestalt Principles
        - Cognitive Load Theory
        - Swiss Cheese Model
        - DeepGaze III
        """)

    # File upload
    st.header("1️⃣ Upload Document")
    uploaded_file = st.file_uploader(
        "Upload inspection form (PNG, JPG, PDF, DOCX)",
        type=['png', 'jpg', 'jpeg', 'pdf', 'docx']
    )

    if uploaded_file is not None:
        # Save uploaded file temporarily with safe filename
        import uuid
        from pathlib import Path

        # Get file extension
        file_ext = Path(uploaded_file.name).suffix
        # Create safe filename with UUID
        safe_filename = f"temp_{uuid.uuid4().hex[:8]}{file_ext}"
        temp_path = safe_filename

        with open(temp_path, 'wb') as f:
            f.write(uploaded_file.read())

        # Display uploaded image preview
        try:
            if uploaded_file.type in ['image/png', 'image/jpeg', 'image/jpg']:
                image = Image.open(temp_path)
                st.image(image, caption="Uploaded Document", use_container_width=True)
            elif uploaded_file.type == 'application/pdf':
                st.info("📄 PDF file uploaded. Preview will be generated during analysis.")
            elif uploaded_file.type == 'application/vnd.openxmlformats-officedocument.wordprocessingml.document':
                st.info("📝 DOCX file uploaded. Will be processed during analysis.")
        except Exception as e:
            st.warning(f"Could not preview file: {str(e)}")

        # Analysis button
        if st.button("🚀 Start Analysis", type="primary"):
            with st.spinner("Analyzing document... This may take a few minutes."):
                try:
                    # Run analysis
                    analyzer = MedicalFormAnalyzer()
                    result = analyzer.analyze(temp_path)

                    # Store result in session state
                    st.session_state['result'] = result

                    st.success("✅ Analysis complete!")

                except Exception as e:
                    st.error(f"Error during analysis: {str(e)}")
                    st.exception(e)
                finally:
                    # Clean up temporary file
                    import os
                    if os.path.exists(temp_path):
                        try:
                            os.remove(temp_path)
                        except:
                            pass  # Ignore cleanup errors

    # Display results if available
    if 'result' in st.session_state:
        result = st.session_state['result']

        st.markdown("---")
        st.header("2️⃣ Analysis Results")

        # Executive Summary
        col1, col2, col3, col4 = st.columns(4)

        with col1:
            st.metric(
                "Overall Score",
                f"{result.scores['overall_score']:.0f}/100",
                delta=result.scores['grade']
            )

        with col2:
            st.metric(
                "Risk Level",
                result.risk_analysis['risk_level'].upper(),
                delta=f"{result.risk_analysis['overall_risk_score']:.0f}"
            )

        with col3:
            st.metric(
                "Cognitive Load",
                result.cognitive_load['load_level'].upper(),
                delta=f"{result.cognitive_load['total_cognitive_load']:.2f}"
            )

        with col4:
            st.metric(
                "Recommendations",
                len(result.recommendations),
                delta="issues found"
            )

        # Tabs for different views
        tab1, tab2, tab3, tab4, tab5 = st.tabs([
            "📊 Dashboard",
            "👁️ Attention Analysis",
            "⚠️ Risk Zones",
            "🎯 Recommendations",
            "📄 Detailed Report"
        ])

        with tab1:
            st.subheader("Quality Score Dashboard")

            # Display dashboard image
            dashboard_img = result.visualizations['dashboard']
            st.image(
                cv2.cvtColor(dashboard_img, cv2.COLOR_BGR2RGB),
                caption="Quality Scores",
                use_container_width=True
            )

            # Category details
            st.markdown("### Category Breakdown")

            for category, score in result.scores['category_scores'].items():
                col1, col2 = st.columns([3, 1])
                with col1:
                    st.progress(score / 100, text=category.replace('_', ' ').title())
                with col2:
                    st.markdown(f"**{score:.0f}/100**")

        with tab2:
            st.subheader("Visual Attention Analysis")

            # Attention heatmap
            st.markdown("#### Attention Heatmap")
            st.markdown("Shows where users are likely to look (red = high attention, blue = low attention)")

            heatmap_img = result.visualizations['heatmap']
            st.image(
                cv2.cvtColor(heatmap_img, cv2.COLOR_BGR2RGB),
                caption="Attention Heatmap",
                use_container_width=True
            )

            # Gaze path
            st.markdown("#### Predicted Gaze Path")
            st.markdown("Simulated eye movement sequence")

            gaze_img = result.visualizations['gaze_path']
            st.image(
                cv2.cvtColor(gaze_img, cv2.COLOR_BGR2RGB),
                caption="Gaze Path",
                use_container_width=True
            )

            # Statistics
            col1, col2, col3 = st.columns(3)
            with col1:
                st.metric(
                    "Fixations",
                    len(result.attention_analysis['fixations'])
                )
            with col2:
                st.metric(
                    "Coverage",
                    f"{result.attention_analysis['efficiency']['coverage_ratio']*100:.0f}%"
                )
            with col3:
                st.metric(
                    "Blind Spots",
                    len(result.attention_analysis['cold_spots'])
                )

        with tab3:
            st.subheader("Risk Zone Analysis")

            # Risk zones visualization
            risk_img = result.visualizations['risk_zones']
            st.image(
                cv2.cvtColor(risk_img, cv2.COLOR_BGR2RGB),
                caption="Identified Risk Zones",
                use_container_width=True
            )

            # Risk zone details
            st.markdown("#### Risk Zone Details")

            if result.risk_analysis['risk_zones']:
                for i, zone in enumerate(result.risk_analysis['risk_zones'], 1):
                    with st.expander(f"Zone {i}: {zone['risk_type']} (Risk: {zone['risk_score']:.0%})"):
                        st.markdown(f"**Type:** {zone['risk_type']}")
                        st.markdown(f"**Risk Score:** {zone['risk_score']:.0%}")
                        st.markdown(f"**Description:** {zone['description']}")

                        # Risk level indicator
                        if zone['risk_score'] > 0.8:
                            st.error("🔴 Critical Risk")
                        elif zone['risk_score'] > 0.6:
                            st.warning("🟠 High Risk")
                        elif zone['risk_score'] > 0.4:
                            st.info("🟡 Medium Risk")
                        else:
                            st.success("🟢 Low Risk")
            else:
                st.success("No high-risk zones identified!")

        with tab4:
            st.subheader("Improvement Recommendations")

            if result.recommendations:
                for i, rec in enumerate(result.recommendations, 1):
                    # Priority badge
                    priority_colors = {
                        'critical': '🔴',
                        'high': '🟠',
                        'medium': '🟡',
                        'low': '🟢'
                    }

                    priority_icon = priority_colors.get(rec['priority'], '⚪')

                    with st.expander(f"{priority_icon} {i}. {rec['title']} [{rec['priority'].upper()}]"):
                        st.markdown(f"**Category:** {rec['category'].replace('_', ' ').title()}")
                        st.markdown(f"**Description:**")
                        st.markdown(rec['description'])
                        st.markdown(f"**Expected Impact:**")
                        st.info(rec['impact'])
            else:
                st.success("🎉 No major improvements needed! Form design is excellent.")

        with tab5:
            st.subheader("Detailed Report")

            # Strengths and Weaknesses
            col1, col2 = st.columns(2)

            with col1:
                st.markdown("#### ✅ Strengths")
                if result.scores['strengths']:
                    for strength in result.scores['strengths']:
                        st.success(f"✓ {strength.replace('_', ' ').title()}")
                else:
                    st.info("See category scores above")

            with col2:
                st.markdown("#### ⚠️ Areas for Improvement")
                if result.scores['weaknesses']:
                    for weakness in result.scores['weaknesses']:
                        st.warning(f"⚠ {weakness.replace('_', ' ').title()}")
                else:
                    st.success("No major weaknesses found!")

            # Detailed metrics
            st.markdown("#### 📈 Detailed Metrics")

            with st.expander("Document Structure"):
                st.json({
                    'Elements Detected': len(result.elements),
                    'Text Regions': len(result.text_regions),
                    'Sections': result.structure['metrics']['section_count'],
                    'Tables': result.structure['metrics']['table_count'],
                    'Forms': result.structure['metrics']['form_count']
                })

            with st.expander("Attention Analysis"):
                st.json({
                    'Total Fixations': len(result.attention_analysis['fixations']),
                    'Coverage Ratio': f"{result.attention_analysis['efficiency']['coverage_ratio']:.2%}",
                    'Revisit Ratio': f"{result.attention_analysis['efficiency']['revisit_ratio']:.2%}",
                    'Hotspots': len(result.attention_analysis['hotspots']),
                    'Cold Spots': len(result.attention_analysis['cold_spots'])
                })

            with st.expander("Risk Analysis"):
                st.json({
                    'Overall Risk Score': f"{result.risk_analysis['overall_risk_score']:.1f}/100",
                    'Attention Risk': f"{result.risk_analysis['component_scores']['attention_risk']:.1f}",
                    'Human Factors Risk': f"{result.risk_analysis['component_scores']['human_factors_risk']:.1f}",
                    'Error Prediction Risk': f"{result.risk_analysis['component_scores']['error_prediction_risk']:.1f}",
                    'Cognitive Load Risk': f"{result.risk_analysis['component_scores']['cognitive_load_risk']:.1f}"
                })

            # Download buttons
            st.markdown("#### 💾 Download Results")

            col1, col2, col3 = st.columns(3)

            with col1:
                # Composite image download
                composite_img = result.visualizations['composite']
                _, buffer = cv2.imencode('.png', composite_img)
                st.download_button(
                    label="📥 Download Composite",
                    data=buffer.tobytes(),
                    file_name="analysis_composite.png",
                    mime="image/png"
                )

            with col2:
                # Dashboard download
                dashboard_img = result.visualizations['dashboard']
                _, buffer = cv2.imencode('.png', dashboard_img)
                st.download_button(
                    label="📥 Download Dashboard",
                    data=buffer.tobytes(),
                    file_name="analysis_dashboard.png",
                    mime="image/png"
                )

            with col3:
                # Report download (text)
                report_text = generate_text_report(result)
                st.download_button(
                    label="📥 Download Report",
                    data=report_text,
                    file_name="analysis_report.txt",
                    mime="text/plain"
                )


def generate_text_report(result) -> str:
    """Generate text report from analysis result."""
    lines = []
    lines.append("=" * 80)
    lines.append("MEDICAL FORM ANALYSIS REPORT")
    lines.append("=" * 80)
    lines.append("")

    # Executive Summary
    lines.append("EXECUTIVE SUMMARY")
    lines.append("-" * 80)
    lines.append(f"Overall Quality Score: {result.scores['overall_score']:.1f}/100 (Grade: {result.scores['grade']})")
    lines.append(f"Risk Level: {result.risk_analysis['risk_level'].upper()}")
    lines.append(f"Cognitive Load: {result.cognitive_load['load_level'].upper()}")
    lines.append("")

    # Category Scores
    lines.append("CATEGORY SCORES")
    lines.append("-" * 80)
    for category, score in result.scores['category_scores'].items():
        lines.append(f"  {category.replace('_', ' ').title()}: {score:.1f}/100")
    lines.append("")

    # Recommendations
    lines.append("RECOMMENDATIONS")
    lines.append("-" * 80)
    for i, rec in enumerate(result.recommendations, 1):
        lines.append(f"\n{i}. [{rec['priority'].upper()}] {rec['title']}")
        lines.append(f"   {rec['description']}")
        lines.append(f"   Expected Impact: {rec['impact']}")

    lines.append("\n" + "=" * 80)

    return "\n".join(lines)


if __name__ == "__main__":
    main()
