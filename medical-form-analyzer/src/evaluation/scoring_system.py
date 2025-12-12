"""
Scoring System
~~~~~~~~~~~~~~

Comprehensive scoring system for document quality evaluation.
"""

import numpy as np
from typing import Dict


class ScoringSystem:
    """Calculates comprehensive quality scores for documents."""

    def __init__(self, config: Dict):
        self.config = config
        self.category_weights = {
            'visual_clarity': 0.20,
            'cognitive_load': 0.25,
            'error_prevention': 0.30,
            'workflow_efficiency': 0.15,
            'compliance': 0.10
        }

    def calculate_scores(self,
                        document_analysis: Dict,
                        attention_analysis: Dict,
                        risk_analysis: Dict,
                        cognitive_load: Dict) -> Dict:
        """Calculate comprehensive scores across all categories."""
        # Category scores
        visual_clarity_score = self._score_visual_clarity(document_analysis, attention_analysis)
        cognitive_load_score = self._score_cognitive_load(cognitive_load)
        error_prevention_score = self._score_error_prevention(risk_analysis)
        workflow_efficiency_score = self._score_workflow_efficiency(attention_analysis)
        compliance_score = self._score_compliance(document_analysis)

        # Overall score
        overall_score = (
            visual_clarity_score * self.category_weights['visual_clarity'] +
            cognitive_load_score * self.category_weights['cognitive_load'] +
            error_prevention_score * self.category_weights['error_prevention'] +
            workflow_efficiency_score * self.category_weights['workflow_efficiency'] +
            compliance_score * self.category_weights['compliance']
        )

        return {
            'overall_score': overall_score,
            'grade': self._get_grade(overall_score),
            'category_scores': {
                'visual_clarity': visual_clarity_score,
                'cognitive_load': cognitive_load_score,
                'error_prevention': error_prevention_score,
                'workflow_efficiency': workflow_efficiency_score,
                'compliance': compliance_score
            },
            'strengths': self._identify_strengths({
                'visual_clarity': visual_clarity_score,
                'cognitive_load': cognitive_load_score,
                'error_prevention': error_prevention_score,
                'workflow_efficiency': workflow_efficiency_score,
                'compliance': compliance_score
            }),
            'weaknesses': self._identify_weaknesses({
                'visual_clarity': visual_clarity_score,
                'cognitive_load': cognitive_load_score,
                'error_prevention': error_prevention_score,
                'workflow_efficiency': workflow_efficiency_score,
                'compliance': compliance_score
            })
        }

    def _score_visual_clarity(self, document_analysis: Dict, attention_analysis: Dict) -> float:
        """Score visual clarity (0-100)."""
        # Good coverage = good clarity
        coverage = attention_analysis.get('efficiency', {}).get('coverage_ratio', 0.5)
        coverage_score = coverage * 100

        # Few cold spots = good clarity
        cold_spots_count = len(attention_analysis.get('cold_spots', []))
        cold_spots_score = max(100 - cold_spots_count * 10, 0)

        return (coverage_score + cold_spots_score) / 2

    def _score_cognitive_load(self, cognitive_load: Dict) -> float:
        """Score cognitive load (lower is better, inverted for score)."""
        load = cognitive_load.get('total_cognitive_load', 0.5)
        # Invert: low load = high score
        return (1 - load) * 100

    def _score_error_prevention(self, risk_analysis: Dict) -> float:
        """Score error prevention design."""
        overall_risk = risk_analysis.get('overall_risk_score', 50)
        # Invert: low risk = high score
        return 100 - overall_risk

    def _score_workflow_efficiency(self, attention_analysis: Dict) -> float:
        """Score workflow efficiency."""
        efficiency = attention_analysis.get('efficiency', {})

        coverage = efficiency.get('coverage_ratio', 0.5) * 100
        revisit_penalty = efficiency.get('revisit_ratio', 0.3) * 50

        return max(coverage - revisit_penalty, 0)

    def _score_compliance(self, document_analysis: Dict) -> float:
        """Score regulatory compliance."""
        # Simplified: check if required fields are present
        return 85.0  # Placeholder

    @staticmethod
    def _get_grade(score: float) -> str:
        """Convert score to letter grade."""
        if score >= 90:
            return 'A'
        elif score >= 75:
            return 'B'
        elif score >= 60:
            return 'C'
        elif score >= 40:
            return 'D'
        else:
            return 'F'

    @staticmethod
    def _identify_strengths(scores: Dict) -> list:
        """Identify top 3 strengths."""
        sorted_scores = sorted(scores.items(), key=lambda x: x[1], reverse=True)
        return [name for name, score in sorted_scores[:3] if score > 70]

    @staticmethod
    def _identify_weaknesses(scores: Dict) -> list:
        """Identify top 3 weaknesses."""
        sorted_scores = sorted(scores.items(), key=lambda x: x[1])
        return [name for name, score in sorted_scores[:3] if score < 60]
