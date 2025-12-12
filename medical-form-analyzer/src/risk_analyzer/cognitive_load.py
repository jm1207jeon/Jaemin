"""
Cognitive Load Analyzer
~~~~~~~~~~~~~~~~~~~~~~~

Analyzes cognitive load based on Cognitive Load Theory.

Based on research:
- Cognitive Load Theory in UI Design (Aufait UX)
- A critical analysis of cognitive load measurement methods (arXiv 2024)
"""

import numpy as np
import cv2
from typing import Dict, List
from ..document_analyzer.layout_parser import LayoutElement


class CognitiveLoadAnalyzer:
    """Analyzes cognitive load of documents."""

    def __init__(self, config: Dict):
        self.config = config
        self.cl_config = config.get('risk_analysis', {}).get('cognitive_load', {})
        self.max_elements_per_section = self.cl_config.get('max_elements_per_section', 10)

    def analyze(self, image: np.ndarray, elements: List[LayoutElement], structure: Dict) -> Dict:
        """
        Analyze cognitive load across multiple dimensions.

        Dimensions:
        - Intrinsic load: Inherent complexity
        - Extraneous load: Design-related complexity
        - Germane load: Learning-related load
        """
        intrinsic_load = self._calculate_intrinsic_load(elements, structure)
        extraneous_load = self._calculate_extraneous_load(image, elements)
        visual_clutter = self._measure_visual_clutter(image)

        total_load = (intrinsic_load + extraneous_load) / 2

        return {
            'intrinsic_load': intrinsic_load,
            'extraneous_load': extraneous_load,
            'visual_clutter': visual_clutter,
            'total_cognitive_load': total_load,
            'load_level': self._categorize_load(total_load)
        }

    def _calculate_intrinsic_load(self, elements: List[LayoutElement], structure: Dict) -> float:
        """
        Calculate intrinsic cognitive load (inherent complexity).

        Factors:
        - Number of elements
        - Element types diversity
        - Structural complexity
        """
        if not elements:
            return 0.0

        # Element count factor
        element_count_factor = min(len(elements) / 50, 1.0)

        # Diversity factor
        unique_types = len(set(e.type for e in elements))
        diversity_factor = min(unique_types / 7, 1.0)

        # Structural complexity
        section_count = structure.get('metrics', {}).get('section_count', 1)
        table_count = structure.get('metrics', {}).get('table_count', 0)
        structural_factor = min((section_count + table_count * 2) / 10, 1.0)

        intrinsic_load = (element_count_factor + diversity_factor + structural_factor) / 3

        return intrinsic_load

    def _calculate_extraneous_load(self, image: np.ndarray, elements: List[LayoutElement]) -> float:
        """
        Calculate extraneous cognitive load (design-induced complexity).

        Factors:
        - Poor contrast
        - Small fonts
        - Dense information
        - Poor grouping
        """
        # Density analysis
        density_score = self._analyze_density(image, elements)

        # Contrast analysis
        contrast_score = self._analyze_contrast(image)

        # Size analysis
        size_score = self._analyze_element_sizes(elements)

        extraneous_load = (density_score + (1 - contrast_score) + size_score) / 3

        return extraneous_load

    def _analyze_density(self, image: np.ndarray, elements: List[LayoutElement]) -> float:
        """Analyze information density."""
        if not elements:
            return 0.0

        h, w = image.shape[:2]
        total_area = h * w

        # Calculate area covered by elements
        covered_area = sum(e.area for e in elements)
        coverage_ratio = covered_area / total_area

        # High coverage = high density
        density_score = min(coverage_ratio * 1.5, 1.0)

        return density_score

    def _analyze_contrast(self, image: np.ndarray) -> float:
        """Analyze visual contrast."""
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if len(image.shape) == 3 else image

        # Calculate standard deviation as proxy for contrast
        std_dev = np.std(gray)

        # Normalize (typical std for good contrast is around 60-80)
        contrast_score = min(std_dev / 80, 1.0)

        return contrast_score

    def _analyze_element_sizes(self, elements: List[LayoutElement]) -> float:
        """Analyze if elements are appropriately sized."""
        if not elements:
            return 0.0

        writable_elements = [e for e in elements if e.type in ['Checkbox', 'SignatureField', 'TextField']]

        if not writable_elements:
            return 0.0

        # Count small elements
        small_elements = sum(1 for e in writable_elements if e.width < 20 or e.height < 20)

        size_score = small_elements / len(writable_elements)

        return size_score

    def _measure_visual_clutter(self, image: np.ndarray) -> Dict:
        """
        Measure visual clutter using multiple methods.

        Methods:
        - Edge Density
        - Feature Congestion (simplified)
        """
        edge_density = self._calculate_edge_density(image)

        return {
            'edge_density': edge_density,
            'clutter_level': 'high' if edge_density > 0.7 else ('medium' if edge_density > 0.4 else 'low')
        }

    @staticmethod
    def _calculate_edge_density(image: np.ndarray) -> float:
        """
        Calculate edge density (percentage of edge pixels).

        Based on: Measuring visual clutter (ARVO Journals)
        """
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if len(image.shape) == 3 else image

        # Detect edges
        edges = cv2.Canny(gray, 50, 150)

        # Calculate density
        edge_pixels = np.sum(edges > 0)
        total_pixels = edges.size

        density = edge_pixels / total_pixels

        return density

    @staticmethod
    def _categorize_load(load: float) -> str:
        """Categorize cognitive load level."""
        if load < 0.3:
            return 'low'
        elif load < 0.6:
            return 'medium'
        elif load < 0.8:
            return 'high'
        else:
            return 'critical'
