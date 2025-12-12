"""
Error Predictor
~~~~~~~~~~~~~~~

Predicts potential human errors based on Swiss Cheese Model and HFACS.

Based on research:
- Understanding the "Swiss Cheese Model" (PMC 8514562)
- Application of HFACS Model to Medical Errors (PMC 10803676)
"""

import numpy as np
from typing import Dict, List
from ..document_analyzer.layout_parser import LayoutElement


class ErrorPredictor:
    """Predicts human error probabilities."""

    def __init__(self, config: Dict):
        self.config = config
        self.error_weights = config.get('risk_analysis', {}).get('error_weights', {
            'omission': 1.0,
            'commission': 0.8,
            'sequence': 0.9,
            'time': 0.6
        })

    def predict_errors(self,
                      elements: List[LayoutElement],
                      attention_analysis: Dict,
                      human_factors: Dict) -> Dict:
        """
        Predict error probabilities using Swiss Cheese Model approach.

        Error types:
        - Omission: Missing required information
        - Commission: Entering incorrect information
        - Sequence: Performing steps out of order
        - Time: Time-related errors (rushing, fatigue)
        """
        omission_risks = self._predict_omission_errors(elements, attention_analysis)
        commission_risks = self._predict_commission_errors(elements, human_factors)
        sequence_risks = self._predict_sequence_errors(elements)

        return {
            'omission_risks': omission_risks,
            'commission_risks': commission_risks,
            'sequence_risks': sequence_risks,
            'overall_risk_score': self._calculate_overall_risk(
                omission_risks, commission_risks, sequence_risks
            )
        }

    def _predict_omission_errors(self, elements: List[LayoutElement], attention_analysis: Dict) -> List[Dict]:
        """Predict omission errors (missing required information)."""
        writable_elements = [e for e in elements if e.type in ['Checkbox', 'SignatureField', 'TextField']]
        cold_spots = attention_analysis.get('cold_spots', [])

        omission_risks = []

        for i, elem in enumerate(writable_elements):
            # Check if element is in a cold spot
            in_cold_spot = any(
                self._bbox_overlap(elem.bbox, cold_spot) > 0.5
                for cold_spot in cold_spots
            )

            risk_score = 0.1  # Base risk

            if in_cold_spot:
                risk_score += 0.6  # High risk if in blind spot

            # Small elements are easier to miss
            if elem.area < 400:  # 20x20 pixels
                risk_score += 0.2

            # Elements at edges are easier to miss
            if self._is_at_edge(elem, elements):
                risk_score += 0.15

            omission_risks.append({
                'element_index': i,
                'element_type': elem.type,
                'risk_score': min(risk_score, 1.0),
                'in_blind_spot': in_cold_spot
            })

        return omission_risks

    def _predict_commission_errors(self, elements: List[LayoutElement], human_factors: Dict) -> List[Dict]:
        """Predict commission errors (incorrect information)."""
        habituation_risk = human_factors.get('habituation_risk', {})
        fitts_analysis = human_factors.get('fitts_analysis', {})

        commission_risks = []

        # Habituation-related commission errors
        if habituation_risk.get('at_risk'):
            for seq_len in habituation_risk.get('consecutive_sequences', []):
                risk_score = min(0.3 + (seq_len - 5) * 0.05, 0.9)
                commission_risks.append({
                    'error_type': 'habituation',
                    'risk_score': risk_score,
                    'sequence_length': seq_len
                })

        # Fitts's Law related errors (difficult movements lead to errors)
        high_risk_transitions = fitts_analysis.get('high_risk_transitions', [])
        for transition in high_risk_transitions:
            commission_risks.append({
                'error_type': 'motor_control',
                'risk_score': transition['error_risk'],
                'from_element': transition['from'],
                'to_element': transition['to']
            })

        return commission_risks

    def _predict_sequence_errors(self, elements: List[LayoutElement]) -> Dict:
        """Predict sequence errors (performing steps out of order)."""
        writable_elements = [e for e in elements if e.type in ['Checkbox', 'SignatureField', 'TextField']]

        if len(writable_elements) < 2:
            return {'at_risk': False, 'confusing_sequences': []}

        # Check for non-linear sequences (elements not in reading order)
        sorted_by_position = sorted(writable_elements, key=lambda e: (e.bbox[1], e.bbox[0]))
        actual_order = writable_elements

        confusing_sequences = []
        for i in range(len(actual_order)):
            expected_pos = sorted_by_position.index(actual_order[i]) if actual_order[i] in sorted_by_position else i
            if abs(expected_pos - i) > 2:  # Out of order
                confusing_sequences.append({
                    'element_index': i,
                    'expected_position': expected_pos,
                    'actual_position': i,
                    'risk_score': min(abs(expected_pos - i) * 0.1, 1.0)
                })

        return {
            'at_risk': len(confusing_sequences) > 0,
            'confusing_sequences': confusing_sequences,
            'risk_score': np.mean([s['risk_score'] for s in confusing_sequences]) if confusing_sequences else 0
        }

    @staticmethod
    def _bbox_overlap(bbox1: tuple, bbox2: tuple) -> float:
        """Calculate overlap ratio between two bounding boxes."""
        x1_1, y1_1, x2_1, y2_1 = bbox1
        x1_2, y1_2, x2_2, y2_2 = bbox2

        # Calculate intersection
        x1_i = max(x1_1, x1_2)
        y1_i = max(y1_1, y1_2)
        x2_i = min(x2_1, x2_2)
        y2_i = min(y2_1, y2_2)

        if x2_i < x1_i or y2_i < y1_i:
            return 0.0

        intersection = (x2_i - x1_i) * (y2_i - y1_i)
        area1 = (x2_1 - x1_1) * (y2_1 - y1_1)
        area2 = (x2_2 - x1_2) * (y2_2 - y1_2)

        union = area1 + area2 - intersection

        return intersection / union if union > 0 else 0

    @staticmethod
    def _is_at_edge(element: LayoutElement, all_elements: List[LayoutElement], margin: int = 50) -> bool:
        """Check if element is at document edge."""
        if not all_elements:
            return False

        all_x = [e.bbox[0] for e in all_elements] + [e.bbox[2] for e in all_elements]
        all_y = [e.bbox[1] for e in all_elements] + [e.bbox[3] for e in all_elements]

        doc_left = min(all_x)
        doc_right = max(all_x)
        doc_top = min(all_y)
        doc_bottom = max(all_y)

        elem_x, elem_y = element.center

        at_left = abs(elem_x - doc_left) < margin
        at_right = abs(elem_x - doc_right) < margin
        at_top = abs(elem_y - doc_top) < margin
        at_bottom = abs(elem_y - doc_bottom) < margin

        return at_left or at_right or at_top or at_bottom

    def _calculate_overall_risk(self,
                               omission_risks: List[Dict],
                               commission_risks: List[Dict],
                               sequence_risks: Dict) -> float:
        """Calculate overall error risk score."""
        omission_score = np.mean([r['risk_score'] for r in omission_risks]) if omission_risks else 0
        commission_score = np.mean([r['risk_score'] for r in commission_risks]) if commission_risks else 0
        sequence_score = sequence_risks.get('risk_score', 0)

        # Weighted combination
        overall = (
            omission_score * self.error_weights['omission'] +
            commission_score * self.error_weights['commission'] +
            sequence_score * self.error_weights['sequence']
        ) / sum([
            self.error_weights['omission'],
            self.error_weights['commission'],
            self.error_weights['sequence']
        ])

        return overall
