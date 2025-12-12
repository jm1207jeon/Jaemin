"""
Human Factors Analyzer
~~~~~~~~~~~~~~~~~~~~~~

Applies human factors engineering principles including Fitts's Law and Gestalt Principles.

Based on research:
- Fitts's Law: The Importance of Size and Distance in UI Design (IxDF)
- Gestalt Principles of Visual Perception (UserTesting, 2024)
"""

import numpy as np
from typing import List, Dict, Tuple
import cv2
from ..document_analyzer.layout_parser import LayoutElement


class HumanFactorsAnalyzer:
    """Analyzes documents using human factors engineering principles."""

    def __init__(self, config: Dict):
        self.config = config
        self.fitts_k = config.get('risk_analysis', {}).get('fitts_law', {}).get('k_constant', 100)
        self.proximity_threshold = config.get('risk_analysis', {}).get('gestalt', {}).get('proximity_threshold', 50)
        self.similarity_threshold = config.get('risk_analysis', {}).get('gestalt', {}).get('similarity_threshold', 0.7)
        self.habituation_threshold = config.get('risk_analysis', {}).get('gestalt', {}).get('repetition_habituation_threshold', 5)

    def analyze_fitts_law(self, elements: List[LayoutElement]) -> Dict:
        """
        Apply Fitts's Law to analyze interaction difficulty.

        Fitts's Law: T = a + b * log2(D/W + 1)
        where T = time, D = distance, W = width (target size)
        """
        writable_elements = [e for e in elements if e.type in ['Checkbox', 'SignatureField', 'TextField']]

        if len(writable_elements) < 2:
            return {'fitts_scores': [], 'avg_difficulty': 0}

        fitts_scores = []

        for i in range(len(writable_elements) - 1):
            current = writable_elements[i]
            next_elem = writable_elements[i + 1]

            # Calculate distance between centers
            distance = np.sqrt(
                (next_elem.center[0] - current.center[0])**2 +
                (next_elem.center[1] - current.center[1])**2
            )

            # Target size (minimum dimension)
            target_width = min(next_elem.width, next_elem.height)

            # Fitts's Law difficulty index
            if target_width > 0:
                index_of_difficulty = np.log2(distance / target_width + 1)
                movement_time = self.fitts_k * index_of_difficulty

                fitts_scores.append({
                    'from': i,
                    'to': i + 1,
                    'distance': distance,
                    'target_size': target_width,
                    'index_of_difficulty': index_of_difficulty,
                    'predicted_time_ms': movement_time,
                    'error_risk': min(index_of_difficulty / 10, 1.0)
                })

        return {
            'fitts_scores': fitts_scores,
            'avg_difficulty': np.mean([s['index_of_difficulty'] for s in fitts_scores]) if fitts_scores else 0,
            'max_difficulty': max([s['index_of_difficulty'] for s in fitts_scores]) if fitts_scores else 0,
            'high_risk_transitions': [s for s in fitts_scores if s['error_risk'] > 0.7]
        }

    def analyze_gestalt_principles(self, elements: List[LayoutElement]) -> Dict:
        """Analyze document using Gestalt principles."""
        proximity_groups = self._analyze_proximity(elements)
        similarity_groups = self._analyze_similarity(elements)
        habituation_risk = self._analyze_repetition_habituation(elements)

        return {
            'proximity_groups': proximity_groups,
            'similarity_groups': similarity_groups,
            'habituation_risk': habituation_risk
        }

    def _analyze_proximity(self, elements: List[LayoutElement]) -> List[List[int]]:
        """Group elements by proximity (Gestalt Proximity Principle)."""
        if not elements:
            return []

        groups = []
        used = set()

        for i, elem1 in enumerate(elements):
            if i in used:
                continue

            group = [i]
            used.add(i)

            for j, elem2 in enumerate(elements):
                if j in used or j <= i:
                    continue

                distance = np.sqrt(
                    (elem1.center[0] - elem2.center[0])**2 +
                    (elem1.center[1] - elem2.center[1])**2
                )

                if distance < self.proximity_threshold:
                    group.append(j)
                    used.add(j)

            if len(group) > 1:
                groups.append(group)

        return groups

    def _analyze_similarity(self, elements: List[LayoutElement]) -> Dict:
        """Analyze similarity grouping (Gestalt Similarity Principle)."""
        type_groups = {}

        for i, elem in enumerate(elements):
            if elem.type not in type_groups:
                type_groups[elem.type] = []
            type_groups[elem.type].append(i)

        return type_groups

    def _analyze_repetition_habituation(self, elements: List[LayoutElement]) -> Dict:
        """
        Detect repetitive patterns that can cause habituation errors.

        Habituation: Reduced response due to repeated stimulation.
        """
        checkboxes = [e for e in elements if e.type == 'Checkbox']

        if len(checkboxes) < self.habituation_threshold:
            return {'at_risk': False, 'consecutive_count': len(checkboxes)}

        # Check for long sequences of checkboxes
        consecutive_sequences = []
        current_sequence = []

        sorted_checkboxes = sorted(checkboxes, key=lambda e: (e.bbox[1], e.bbox[0]))

        for i, checkbox in enumerate(sorted_checkboxes):
            if not current_sequence:
                current_sequence.append(checkbox)
            else:
                prev = current_sequence[-1]
                distance = np.sqrt(
                    (checkbox.center[0] - prev.center[0])**2 +
                    (checkbox.center[1] - prev.center[1])**2
                )

                if distance < 100:  # Close proximity
                    current_sequence.append(checkbox)
                else:
                    if len(current_sequence) >= self.habituation_threshold:
                        consecutive_sequences.append(len(current_sequence))
                    current_sequence = [checkbox]

        # Check last sequence
        if len(current_sequence) >= self.habituation_threshold:
            consecutive_sequences.append(len(current_sequence))

        return {
            'at_risk': len(consecutive_sequences) > 0,
            'consecutive_sequences': consecutive_sequences,
            'max_consecutive': max(consecutive_sequences) if consecutive_sequences else 0,
            'risk_level': 'high' if any(s > 10 for s in consecutive_sequences) else ('medium' if consecutive_sequences else 'low')
        }
