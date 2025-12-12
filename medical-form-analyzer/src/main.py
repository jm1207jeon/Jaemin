"""
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

        print("\n👁️  Phase 2: Visual Attention Analysis...")
        # Attention Analysis
        attention_analysis = self.attention_analyzer.analyze(image, elements)

        print(f"   ✓ Generated saliency map")
        print(f"   ✓ Simulated {len(attention_analysis['fixations'])} eye fixations")
        print(f"   ✓ Identified {len(attention_analysis['cold_spots'])} potential blind spots")

        print("\n⚠️  Phase 3: Human Factors & Risk Analysis...")
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

        print("\n📊 Phase 4: Quality Scoring & Evaluation...")
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

        print("\n🎨 Phase 5: Visualization Generation...")
        # Visualization
        visualizations = self.overlay_renderer.render_complete_analysis(
            image,
            attention_analysis['saliency_map'],
            attention_analysis['fixations'],
            risk_analysis['risk_zones'],
            scores
        )

        print("   ✓ Created analysis visualizations")

        print("\n✅ Analysis Complete!")

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
                    "Install: pip install pdf2image\n"
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
                    f"DOCX file '{file_path.name}' does not contain extractable images.\n"
                    "Please convert DOCX to PDF or image format first.\n"
                    "You can: \n"
                    "1. Open in Word and 'Save As' → PDF\n"
                    "2. Use online converter: https://www.ilovepdf.com/word_to_pdf\n"
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
                    f"Could not load image from {image_path}\n"
                    f"Supported formats: PNG, JPG, JPEG, PDF\n"
                    f"Your file: {file_ext}"
                )
            return image


class AnalysisResult:
    """Container for analysis results."""

    def __init__(self, **kwargs):
        for key, value in kwargs.items():
            setattr(self, key, value)

    def visualize(self, output_path: str = "analysis_result.png", layer: str = "composite"):
        """
        Save visualization to file.

        Args:
            output_path: Output file path
            layer: Visualization layer ('composite', 'heatmap', 'gaze_path', 'risk_zones', 'dashboard')
        """
        if layer not in self.visualizations:
            raise ValueError(f"Unknown layer: {layer}. Available: {list(self.visualizations.keys())}")

        cv2.imwrite(output_path, self.visualizations[layer])
        print(f"Visualization saved to {output_path}")

    def generate_report(self, output_path: str = "report.txt"):
        """
        Generate text report.

        Args:
            output_path: Output file path
        """
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write("=" * 80 + "\n")
            f.write("MEDICAL FORM ANALYSIS REPORT\n")
            f.write("=" * 80 + "\n\n")

            # Executive Summary
            f.write("EXECUTIVE SUMMARY\n")
            f.write("-" * 80 + "\n")
            f.write(f"Overall Quality Score: {self.scores['overall_score']:.1f}/100 (Grade: {self.scores['grade']})\n")
            f.write(f"Risk Level: {self.risk_analysis['risk_level'].upper()}\n")
            f.write(f"Cognitive Load: {self.cognitive_load['load_level'].upper()}\n\n")

            # Category Scores
            f.write("CATEGORY SCORES\n")
            f.write("-" * 80 + "\n")
            for category, score in self.scores['category_scores'].items():
                f.write(f"  {category.replace('_', ' ').title()}: {score:.1f}/100\n")
            f.write("\n")

            # Strengths
            if self.scores['strengths']:
                f.write("STRENGTHS\n")
                f.write("-" * 80 + "\n")
                for strength in self.scores['strengths']:
                    f.write(f"  ✓ {strength.replace('_', ' ').title()}\n")
                f.write("\n")

            # Weaknesses
            if self.scores['weaknesses']:
                f.write("WEAKNESSES\n")
                f.write("-" * 80 + "\n")
                for weakness in self.scores['weaknesses']:
                    f.write(f"  ✗ {weakness.replace('_', ' ').title()}\n")
                f.write("\n")

            # Recommendations
            f.write("RECOMMENDATIONS\n")
            f.write("-" * 80 + "\n")
            for i, rec in enumerate(self.recommendations, 1):
                f.write(f"\n{i}. [{rec['priority'].upper()}] {rec['title']}\n")
                f.write(f"   {rec['description']}\n")
                f.write(f"   Expected Impact: {rec['impact']}\n")

            f.write("\n" + "=" * 80 + "\n")

        print(f"Report saved to {output_path}")

    def get_summary(self) -> Dict:
        """Get summary statistics."""
        return {
            'overall_score': self.scores['overall_score'],
            'grade': self.scores['grade'],
            'risk_level': self.risk_analysis['risk_level'],
            'element_count': len(self.elements),
            'blind_spots': len(self.attention_analysis['cold_spots']),
            'high_risk_zones': len([z for z in self.risk_analysis['risk_zones'] if z['risk_score'] > 0.7]),
            'recommendation_count': len(self.recommendations)
        }


def main():
    """Command-line interface."""
    import sys

    if len(sys.argv) < 2:
        print("Usage: python -m src.main <image_path>")
        sys.exit(1)

    image_path = sys.argv[1]

    print("Medical Form Analyzer")
    print("=" * 80)
    print(f"Analyzing: {image_path}\n")

    analyzer = MedicalFormAnalyzer()
    result = analyzer.analyze(image_path)

    # Save visualizations
    result.visualize("analysis_composite.png", "composite")
    result.visualize("analysis_heatmap.png", "heatmap")
    result.visualize("analysis_risk_zones.png", "risk_zones")
    result.visualize("analysis_dashboard.png", "dashboard")

    # Generate report
    result.generate_report("analysis_report.txt")

    print("\n" + "=" * 80)
    print("Analysis complete! Check output files:")
    print("  - analysis_composite.png")
    print("  - analysis_heatmap.png")
    print("  - analysis_risk_zones.png")
    print("  - analysis_dashboard.png")
    print("  - analysis_report.txt")


if __name__ == "__main__":
    main()
