"""
Recommendation Engine
~~~~~~~~~~~~~~~~~~~~~

Generates specific improvement recommendations based on analysis.
"""

from typing import Dict, List


class RecommendationEngine:
    """Generates improvement recommendations."""

    def __init__(self, config: Dict):
        self.config = config

    def generate_recommendations(self,
                                scores: Dict,
                                risk_analysis: Dict,
                                cognitive_load: Dict,
                                attention_analysis: Dict) -> List[Dict]:
        """Generate prioritized recommendations."""
        recommendations = []

        # Visual clarity recommendations
        if scores['category_scores']['visual_clarity'] < 60:
            recommendations.extend(self._recommend_visual_improvements(attention_analysis))

        # Cognitive load recommendations
        if scores['category_scores']['cognitive_load'] < 60:
            recommendations.extend(self._recommend_cognitive_improvements(cognitive_load))

        # Error prevention recommendations
        if scores['category_scores']['error_prevention'] < 60:
            recommendations.extend(self._recommend_error_prevention(risk_analysis))

        # Workflow recommendations
        if scores['category_scores']['workflow_efficiency'] < 60:
            recommendations.extend(self._recommend_workflow_improvements(attention_analysis))

        # Prioritize and limit
        recommendations = self._prioritize_recommendations(recommendations)

        return recommendations[:10]  # Top 10

    def _recommend_visual_improvements(self, attention_analysis: Dict) -> List[Dict]:
        """Recommend visual clarity improvements."""
        recommendations = []

        cold_spots = attention_analysis.get('cold_spots', [])
        if len(cold_spots) > 3:
            recommendations.append({
                'category': 'visual_clarity',
                'priority': 'high',
                'title': 'Reduce Blind Spots',
                'description': f'Found {len(cold_spots)} areas with low attention. Consider adding visual emphasis (borders, colors) to critical information in these areas.',
                'impact': 'Reduces risk of missing critical information by 40-60%'
            })

        return recommendations

    def _recommend_cognitive_improvements(self, cognitive_load: Dict) -> List[Dict]:
        """Recommend cognitive load reductions."""
        recommendations = []

        if cognitive_load.get('total_cognitive_load', 0) > 0.7:
            recommendations.append({
                'category': 'cognitive_load',
                'priority': 'high',
                'title': 'Reduce Information Density',
                'description': 'Cognitive load is high. Consider: (1) Breaking form into multiple pages, (2) Adding more whitespace, (3) Grouping related items',
                'impact': 'Can improve completion accuracy by 25-35%'
            })

        clutter = cognitive_load.get('visual_clutter', {})
        if clutter.get('clutter_level') == 'high':
            recommendations.append({
                'category': 'cognitive_load',
                'priority': 'medium',
                'title': 'Reduce Visual Clutter',
                'description': 'High edge density detected. Simplify borders, remove unnecessary visual elements.',
                'impact': 'Improves scan efficiency by 15-20%'
            })

        return recommendations

    def _recommend_error_prevention(self, risk_analysis: Dict) -> List[Dict]:
        """Recommend error prevention improvements."""
        recommendations = []

        risk_zones = risk_analysis.get('risk_zones', [])
        high_risk_zones = [z for z in risk_zones if z['risk_score'] > 0.7]

        if high_risk_zones:
            recommendations.append({
                'category': 'error_prevention',
                'priority': 'critical',
                'title': 'Address High-Risk Zones',
                'description': f'Found {len(high_risk_zones)} high-risk areas. Implement error-proofing: checksum fields, mandatory fields, visual warnings.',
                'impact': 'Reduces error rate by 50-70%'
            })

        return recommendations

    def _recommend_workflow_improvements(self, attention_analysis: Dict) -> List[Dict]:
        """Recommend workflow efficiency improvements."""
        recommendations = []

        efficiency = attention_analysis.get('efficiency', {})
        revisit_ratio = efficiency.get('revisit_ratio', 0)

        if revisit_ratio > 0.3:
            recommendations.append({
                'category': 'workflow_efficiency',
                'priority': 'medium',
                'title': 'Improve Logical Flow',
                'description': 'High revisit ratio suggests confusing layout. Arrange fields in clear top-to-bottom, left-to-right order.',
                'impact': 'Reduces completion time by 20-30%'
            })

        return recommendations

    @staticmethod
    def _prioritize_recommendations(recommendations: List[Dict]) -> List[Dict]:
        """Sort recommendations by priority."""
        priority_order = {'critical': 0, 'high': 1, 'medium': 2, 'low': 3}
        return sorted(recommendations, key=lambda x: priority_order.get(x['priority'], 4))
