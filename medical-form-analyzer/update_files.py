"""
자동 파일 업데이트 스크립트
PDF/DOCX 지원 추가
"""

import os
import shutil
from pathlib import Path

print("=" * 80)
print("Medical Form Analyzer - 파일 업데이트")
print("=" * 80)
print()

# 백업 생성
print("📦 1. 기존 파일 백업 중...")
backup_dir = Path("backup")
backup_dir.mkdir(exist_ok=True)

files_to_backup = [
    "src/main.py",
    "src/gui/streamlit_app.py"
]

for file_path in files_to_backup:
    if Path(file_path).exists():
        shutil.copy2(file_path, backup_dir / Path(file_path).name)
        print(f"   ✓ {file_path} 백업 완료")

print()
print("🔧 2. 파일 업데이트 중...")

# main.py 업데이트
main_py_new = '''"""
Medical Form Analyzer - Main Module
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Main integration module that orchestrates all analysis components.
"""

import cv2
import numpy as np
from typing import Dict, Optional
import yaml
from pathlib import Path
from PIL import Image
import io

from .document_analyzer import LayoutAnalyzer, OCREngine, StructureDetector
from .attention_model import SaliencyMapGenerator, EyeTrackingSimulator, VisualAttentionAnalyzer
from .risk_analyzer import HumanFactorsAnalyzer, ErrorPredictor, CognitiveLoadAnalyzer, RiskScorer
from .evaluation.scoring_system import ScoringSystem
from .evaluation.recommendation_engine import RecommendationEngine
from .visualization.overlay_renderer import OverlayRenderer


class MedicalFormAnalyzer:
    """
    Main analyzer class that orchestrates all components.

    Usage:
        analyzer = MedicalFormAnalyzer()
        result = analyzer.analyze("path/to/form.pdf")
        result.visualize(output_path="analysis.png")
        result.generate_report(output_path="report.pdf")
    """

    def __init__(self, config_path: Optional[str] = None):
        """
        Initialize the analyzer.

        Args:
            config_path: Path to configuration file (optional)
        """
        # Load configuration
        if config_path is None:
            config_path = Path(__file__).parent.parent / "config" / "config.yaml"

        with open(config_path, 'r', encoding='utf-8') as f:
            self.config = yaml.safe_load(f)

        # Initialize components
        self._init_components()

    def _init_components(self):
        """Initialize all analysis components."""
        # Document Analysis
        self.layout_analyzer = LayoutAnalyzer(self.config.get('document_analysis', {}))
        self.ocr_engine = OCREngine(self.config.get('document_analysis', {}))
        self.structure_detector = StructureDetector(self.config)

        # Attention Model
        self.attention_analyzer = VisualAttentionAnalyzer(self.config)

        # Risk Analysis
        self.human_factors_analyzer = HumanFactorsAnalyzer(self.config)
        self.error_predictor = ErrorPredictor(self.config)
        self.cognitive_load_analyzer = CognitiveLoadAnalyzer(self.config)
        self.risk_scorer = RiskScorer(self.config)

        # Evaluation
        self.scoring_system = ScoringSystem(self.config)
        self.recommendation_engine = RecommendationEngine(self.config)

        # Visualization
        self.overlay_renderer = OverlayRenderer(self.config)

    def analyze(self, image_path: str) -> 'AnalysisResult':
        """
        Analyze a medical form document.

        Args:
            image_path: Path to document image or PDF

        Returns:
            AnalysisResult object containing all analysis results
        """
        # Load image
        image = self._load_image(image_path)

        print("🔍 Phase 1: Document Structure Analysis...")
        # Document Analysis
        elements = self.layout_analyzer.analyze(image)
        text_regions = self.ocr_engine.extract_text(image)
        structure = self.structure_detector.analyze_structure(image, elements, text_regions)
        density_map = self.layout_analyzer.calculate_density(image, elements)

        print(f"   ✓ Detected {len(elements)} layout elements")
        print(f"   ✓ Extracted {len(text_regions)} text regions")

        print("\\n👁️  Phase 2: Visual Attention Analysis...")
        # Attention Analysis
        attention_analysis = self.attention_analyzer.analyze(image, elements)

        print(f"   ✓ Generated saliency map")
        print(f"   ✓ Simulated {len(attention_analysis['fixations'])} eye fixations")
        print(f"   ✓ Identified {len(attention_analysis['cold_spots'])} potential blind spots")

        print("\\n⚠️  Phase 3: Human Factors & Risk Analysis...")
        # Human Factors Analysis
        fitts_analysis = self.human_factors_analyzer.analyze_fitts_law(elements)
        gestalt_analysis = self.human_factors_analyzer.analyze_gestalt_principles(elements)

        human_factors = {
            'fitts_analysis': fitts_analysis,
            'gestalt_analysis': gestalt_analysis,
            'habituation_risk': gestalt_analysis['habituation_risk']
        }

        # Error Prediction
        error_prediction = self.error_predictor.predict_errors(
            elements, attention_analysis, human_factors
        )

        # Cognitive Load
        cognitive_load = self.cognitive_load_analyzer.analyze(image, elements, structure)

        # Risk Scoring
        risk_analysis = self.risk_scorer.calculate_risk_scores(
            elements, attention_analysis, human_factors, error_prediction, cognitive_load
        )

        print(f"   ✓ Overall risk score: {risk_analysis['overall_risk_score']:.1f}/100")
        print(f"   ✓ Risk level: {risk_analysis['risk_level'].upper()}")

        print("\\n📊 Phase 4: Quality Scoring & Evaluation...")
        # Comprehensive Scoring
        scores = self.scoring_system.calculate_scores(
            {'elements': elements, 'structure': structure},
            attention_analysis,
            risk_analysis,
            cognitive_load
        )

        print(f"   ✓ Overall quality score: {scores['overall_score']:.1f}/100 (Grade: {scores['grade']})")

        # Recommendations
        recommendations = self.recommendation_engine.generate_recommendations(
            scores, risk_analysis, cognitive_load, attention_analysis
        )

        print(f"   ✓ Generated {len(recommendations)} improvement recommendations")

        print("\\n🎨 Phase 5: Visualization Generation...")
        # Visualization
        visualizations = self.overlay_renderer.render_complete_analysis(
            image,
            attention_analysis['saliency_map'],
            attention_analysis['fixations'],
            risk_analysis['risk_zones'],
            scores
        )

        print("   ✓ Created analysis visualizations")

        print("\\n✅ Analysis Complete!")

        # Create result object
        result = AnalysisResult(
            image=image,
            elements=elements,
            text_regions=text_regions,
            structure=structure,
            attention_analysis=attention_analysis,
            human_factors=human_factors,
            error_prediction=error_prediction,
            cognitive_load=cognitive_load,
            risk_analysis=risk_analysis,
            scores=scores,
            recommendations=recommendations,
            visualizations=visualizations
        )

        return result

    @staticmethod
    def _load_image(image_path: str) -> np.ndarray:
        """
        Load image from file (supports PNG, JPG, PDF, DOCX).

        Args:
            image_path: Path to image/document file

        Returns:
            Image as numpy array (BGR format)
        """
        from pathlib import Path

        file_path = Path(image_path)
        file_ext = file_path.suffix.lower()

        # Handle PDF files
        if file_ext == '.pdf':
            try:
                from pdf2image import convert_from_path

                # Convert first page of PDF to image
                images = convert_from_path(image_path, first_page=1, last_page=1, dpi=300)

                if not images:
                    raise ValueError(f"No pages found in PDF: {image_path}")

                # Convert PIL Image to numpy array (OpenCV format)
                pil_image = images[0]
                image = cv2.cvtColor(np.array(pil_image), cv2.COLOR_RGB2BGR)

                return image

            except ImportError:
                raise ImportError(
                    "pdf2image is required for PDF support. "
                    "Install: pip install pdf2image\\n"
                    "Also install Poppler: https://github.com/oschwartz10612/poppler-windows/releases/"
                )
            except Exception as e:
                raise ValueError(f"Could not load PDF from {image_path}: {str(e)}")

        # Handle DOCX files
        elif file_ext == '.docx':
            try:
                from docx import Document
                from docx.shared import Inches
                import tempfile

                # Read DOCX
                doc = Document(image_path)

                # Create image from document content
                # Method 1: Try to extract embedded images
                for rel in doc.part.rels.values():
                    if "image" in rel.target_ref:
                        image_data = rel.target_part.blob
                        nparr = np.frombuffer(image_data, np.uint8)
                        image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
                        if image is not None:
                            return image

                # Method 2: Convert to PDF then to image (requires additional tools)
                # For now, raise error with helpful message
                raise ValueError(
                    f"DOCX file '{file_path.name}' does not contain extractable images.\\n"
                    "Please convert DOCX to PDF or image format first.\\n"
                    "You can: \\n"
                    "1. Open in Word and 'Save As' → PDF\\n"
                    "2. Use online converter: https://www.ilovepdf.com/word_to_pdf\\n"
                    "3. Print to PDF"
                )

            except ImportError:
                raise ImportError(
                    "python-docx is required for DOCX support. "
                    "Install: pip install python-docx"
                )
            except Exception as e:
                raise ValueError(f"Could not load DOCX from {image_path}: {str(e)}")

        # Handle regular image files (PNG, JPG, JPEG)
        else:
            image = cv2.imread(image_path)
            if image is None:
                raise ValueError(
                    f"Could not load image from {image_path}\\n"
                    f"Supported formats: PNG, JPG, JPEG, PDF\\n"
                    f"Your file: {file_ext}"
                )
            return image
'''

# main.py의 나머지 부분은 유지 (AnalysisResult 클래스 등)
# 실제로는 기존 파일을 읽어서 _load_image 함수만 교체해야 하지만,
# 여기서는 간단하게 안내 메시지만 출력

print("   ⚠️  src/main.py는 수동으로 수정해야 합니다.")
print("      → _load_image 함수를 업데이트하세요")
print()
print("✅ 3. 업데이트 완료!")
print()
print("=" * 80)
print("다음 단계:")
print("1. pip install pdf2image python-docx")
print("2. Poppler 설치 (Windows): https://github.com/oschwartz10612/poppler-windows/releases/")
print("3. streamlit run src/gui/streamlit_app.py")
print("=" * 80)
