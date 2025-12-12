"""
Visual Attention Analyzer
~~~~~~~~~~~~~~~~~~~~~~~~~

High-level analyzer that combines saliency and eye tracking for complete attention analysis.
"""

import numpy as np
from typing import Dict, List, Tuple
import cv2

from .saliency_map import SaliencyMapGenerator
from .eye_tracking_simulator import EyeTrackingSimulator, Fixation, Saccade
from ..document_analyzer.layout_parser import LayoutElement


class VisualAttentionAnalyzer:
    """
    Comprehensive visual attention analysis combining multiple techniques.
    """

    def __init__(self, config: Dict):
        """Initialize visual attention analyzer."""
        self.config = config
        self.saliency_generator = SaliencyMapGenerator(config.get('attention_model', {}))
        self.eye_tracker = EyeTrackingSimulator(config.get('attention_model', {}))

    def analyze(self, image: np.ndarray, elements: List[LayoutElement] = None) -> Dict:
        """
        Perform complete visual attention analysis.

        Args:
            image: Document image
            elements: Optional list of document elements

        Returns:
            Dictionary with comprehensive attention analysis
        """
        # Generate saliency map
        saliency_base = self.saliency_generator.generate(image)

        # Combine with reading patterns
        reading_patterns = self.config.get('attention_model', {}).get('reading_patterns',
                                                                      ['f_pattern', 'gutenberg'])
        saliency_combined = self.saliency_generator.combine_saliency_with_patterns(
            image, patterns=reading_patterns
        )

        # Simulate eye tracking
        fixations, saccades = self.eye_tracker.simulate_scan_path(
            saliency_combined, num_fixations=50
        )

        # Calculate dwell time map
        dwell_map = self.eye_tracker.calculate_dwell_time_map(fixations, image.shape[:2])

        # Identify hotspots and cold spots
        hotspots = self.eye_tracker.identify_attention_hotspots(fixations, image.shape[:2])
        cold_spots = self.eye_tracker.identify_cold_spots(fixations, image.shape[:2])

        # Calculate scan efficiency
        efficiency = self.eye_tracker.calculate_scan_efficiency(fixations, image.shape[:2])

        # Analyze element visibility
        element_visibility = {}
        if elements:
            element_visibility = self._analyze_element_visibility(elements, dwell_map)

        return {
            'saliency_map': saliency_combined,
            'fixations': fixations,
            'saccades': saccades,
            'dwell_map': dwell_map,
            'hotspots': hotspots,
            'cold_spots': cold_spots,
            'efficiency': efficiency,
            'element_visibility': element_visibility
        }

    def _analyze_element_visibility(self,
                                    elements: List[LayoutElement],
                                    dwell_map: np.ndarray) -> Dict:
        """Analyze visibility of each document element."""
        visibility_scores = {}

        for i, element in enumerate(elements):
            x1, y1, x2, y2 = element.bbox

            # Extract dwell time for this element
            roi = dwell_map[y1:y2, x1:x2]
            total_dwell = np.sum(roi)
            avg_dwell = np.mean(roi) if roi.size > 0 else 0

            visibility_scores[i] = {
                'element_type': element.type,
                'total_dwell_ms': total_dwell,
                'avg_dwell_ms': avg_dwell,
                'visibility_score': min(avg_dwell / 1000, 1.0)  # Normalized 0-1
            }

        return visibility_scores
