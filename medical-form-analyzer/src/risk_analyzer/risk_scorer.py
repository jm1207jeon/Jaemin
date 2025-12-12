"""
Risk Scorer
~~~~~~~~~~~

Calculates comprehensive risk scores for documents.
"""

import numpy as np
from typing import Dict, List
from ..document_analyzer.layout_parser import LayoutElement


class RiskScorer:
    """Calculates comprehensive risk scores."""

    def __init__(self, config: Dict):
        self.config = config
        self.risk_levels = config.get('risk_analysis', {}).get('risk_levels', {
            'low': [0, 30],
            'medium': [30, 60],
            'high': [60, 80],
            'critical': [80, 100]
        })

    def calculate_risk_scores(self,
                             elements: List[LayoutElement],
                             attention_analysis: Dict,
                             human_factors: Dict,
                             error_prediction: Dict,
                             cognitive_load: Dict) -> Dict:
        """Calculate comprehensive risk scores."""
        # Individual component scores
        attention_risk = self._score_attention_risk(attention_analysis)
        human_factors_risk = self._score_human_factors_risk(human_factors)
        error_risk = error_prediction.get('overall_risk_score', 0)
        cognitive_risk = cognitive_load.get('total_cognitive_load', 0)

        # Weighted combination
        overall_risk = (
            attention_risk * 0.25 +
            human_factors_risk * 0.30 +
            error_risk * 0.30 +
            cognitive_risk * 0.15
        )

        # Scale to 0-100
        risk_score = overall_risk * 100

        return {
            'overall_risk_score': risk_score,
            'risk_level': self._categorize_risk(risk_score),
            'component_scores': {
                'attention_risk': attention_risk * 100,
                'human_factors_risk': human_factors_risk * 100,
                'error_prediction_risk': error_risk * 100,
                'cognitive_load_risk': cognitive_risk * 100
            },
            'risk_zones': self._identify_risk_zones(elements, attention_analysis, error_prediction)
        }

    def _score_attention_risk(self, attention_analysis: Dict) -> float:
        """Score risk based on attention analysis."""
        cold_spots = attention_analysis.get('cold_spots', [])
        efficiency = attention_analysis.get('efficiency', {})

        coverage = efficiency.get('coverage_ratio', 1.0)
        risk = 1 - coverage + len(cold_spots) * 0.05

        return min(risk, 1.0)

    def _score_human_factors_risk(self, human_factors: Dict) -> float:
        """Score risk based on human factors."""
        fitts_analysis = human_factors.get('fitts_analysis', {})
        habituation = human_factors.get('habituation_risk', {})

        fitts_risk = min(fitts_analysis.get('avg_difficulty', 0) / 5, 1.0)
        habituation_risk = 0.8 if habituation.get('at_risk') else 0.2

        return (fitts_risk + habituation_risk) / 2

    def _categorize_risk(self, score: float) -> str:
        """Categorize risk level."""
        for level, (low, high) in self.risk_levels.items():
            if low <= score < high:
                return level
        return 'critical'

    def _identify_risk_zones(self,
                            elements: List[LayoutElement],
                            attention_analysis: Dict,
                            error_prediction: Dict) -> List[Dict]:
        """Identify specific high-risk zones in the document."""
        risk_zones = []

        # Cold spots
        for cold_spot in attention_analysis.get('cold_spots', []):
            risk_zones.append({
                'bbox': cold_spot,
                'risk_type': 'low_attention',
                'risk_score': 0.8,
                'description': 'Area likely to be overlooked'
            })

        # Omission risks
        for omission_risk in error_prediction.get('omission_risks', []):
            if omission_risk['risk_score'] > 0.6:
                elem_idx = omission_risk['element_index']
                writable_elems = [e for e in elements if e.type in ['Checkbox', 'SignatureField', 'TextField']]
                if elem_idx < len(writable_elems):
                    elem = writable_elems[elem_idx]
                    risk_zones.append({
                        'bbox': elem.bbox,
                        'risk_type': 'omission',
                        'risk_score': omission_risk['risk_score'],
                        'description': f'High risk of missing {elem.type}'
                    })

        return risk_zones
