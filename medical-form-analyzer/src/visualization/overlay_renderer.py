"""
Overlay Renderer
~~~~~~~~~~~~~~~~

Renders analysis results as visual overlays on document images.
"""

import cv2
import numpy as np
from typing import Dict, List, Tuple


class OverlayRenderer:
    """Renders visual overlays for analysis results."""

    def __init__(self, config: Dict):
        self.config = config

    def render_complete_analysis(self,
                                image: np.ndarray,
                                saliency_map: np.ndarray,
                                fixations: List,
                                risk_zones: List[Dict],
                                scores: Dict) -> Dict[str, np.ndarray]:
        """Render complete analysis with multiple visualization layers."""
        # Layer 1: Attention Heatmap
        heatmap_overlay = self.render_saliency_heatmap(image, saliency_map)

        # Layer 2: Gaze Path
        gaze_path_overlay = self.render_gaze_path(image, fixations)

        # Layer 3: Risk Zones
        risk_zone_overlay = self.render_risk_zones(image, risk_zones)

        # Layer 4: Composite (all layers)
        composite = self.render_composite(image, saliency_map, fixations, risk_zones)

        # Layer 5: Score Dashboard
        dashboard = self.render_score_dashboard(scores)

        return {
            'heatmap': heatmap_overlay,
            'gaze_path': gaze_path_overlay,
            'risk_zones': risk_zone_overlay,
            'composite': composite,
            'dashboard': dashboard,
            'original': image
        }

    def render_saliency_heatmap(self, image: np.ndarray, saliency_map: np.ndarray) -> np.ndarray:
        """Render saliency map as heatmap overlay."""
        # Convert saliency to heatmap
        saliency_uint8 = (saliency_map * 255).astype(np.uint8)
        heatmap = cv2.applyColorMap(saliency_uint8, cv2.COLORMAP_JET)

        # Overlay on image
        overlay = cv2.addWeighted(image, 0.6, heatmap, 0.4, 0)

        return overlay

    def render_gaze_path(self, image: np.ndarray, fixations: List) -> np.ndarray:
        """Render eye tracking scan path."""
        overlay = image.copy()

        if not fixations:
            return overlay

        # Draw connections
        for i in range(len(fixations) - 1):
            pt1 = (fixations[i].x, fixations[i].y)
            pt2 = (fixations[i + 1].x, fixations[i + 1].y)
            cv2.arrowedLine(overlay, pt1, pt2, (0, 255, 0), 2, tipLength=0.2)

        # Draw fixation points
        for i, fixation in enumerate(fixations):
            radius = int(5 + fixation.duration / 50)
            cv2.circle(overlay, (fixation.x, fixation.y), radius, (0, 0, 255), -1)
            cv2.circle(overlay, (fixation.x, fixation.y), radius + 2, (255, 255, 255), 2)

            # Show order for first 10 fixations
            if i < 10:
                cv2.putText(overlay, str(i + 1),
                          (fixation.x + 10, fixation.y - 10),
                          cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 2)

        return overlay

    def render_risk_zones(self, image: np.ndarray, risk_zones: List[Dict]) -> np.ndarray:
        """Render risk zones with color coding."""
        overlay = image.copy()

        for zone in risk_zones:
            bbox = zone['bbox']
            risk_score = zone['risk_score']

            # Color based on risk level
            if risk_score > 0.8:
                color = (0, 0, 255)  # Red
            elif risk_score > 0.6:
                color = (0, 165, 255)  # Orange
            elif risk_score > 0.4:
                color = (0, 255, 255)  # Yellow
            else:
                color = (0, 255, 0)  # Green

            # Draw rectangle
            cv2.rectangle(overlay, (bbox[0], bbox[1]), (bbox[2], bbox[3]), color, 3)

            # Add label
            label = f"{zone['risk_type']}: {risk_score:.0%}"
            cv2.putText(overlay, label,
                       (bbox[0], bbox[1] - 10),
                       cv2.FONT_HERSHEY_SIMPLEX, 0.5, color, 2)

        return overlay

    def render_composite(self,
                        image: np.ndarray,
                        saliency_map: np.ndarray,
                        fixations: List,
                        risk_zones: List[Dict]) -> np.ndarray:
        """Render composite view with all layers."""
        # Start with heatmap
        composite = self.render_saliency_heatmap(image, saliency_map)

        # Add risk zones
        for zone in risk_zones:
            bbox = zone['bbox']
            risk_score = zone['risk_score']

            color = (0, 0, 255) if risk_score > 0.7 else (0, 165, 255)
            cv2.rectangle(composite, (bbox[0], bbox[1]), (bbox[2], bbox[3]), color, 2)

        # Add key fixations
        if fixations:
            for i in range(min(10, len(fixations))):
                fixation = fixations[i]
                cv2.circle(composite, (fixation.x, fixation.y), 8, (255, 255, 255), 2)

        return composite

    def render_score_dashboard(self, scores: Dict) -> np.ndarray:
        """Render score dashboard."""
        # Create dashboard image
        dashboard = np.ones((600, 800, 3), dtype=np.uint8) * 255

        # Overall score (large, centered)
        overall = scores.get('overall_score', 0)
        grade = scores.get('grade', 'N/A')

        # Draw score circle
        center = (400, 150)
        radius = 100
        color = self._score_to_color(overall)

        cv2.circle(dashboard, center, radius, color, -1)
        cv2.circle(dashboard, center, radius, (0, 0, 0), 3)

        # Score text
        cv2.putText(dashboard, f"{overall:.0f}",
                   (center[0] - 50, center[1] + 15),
                   cv2.FONT_HERSHEY_SIMPLEX, 2, (255, 255, 255), 3)

        cv2.putText(dashboard, f"Grade: {grade}",
                   (center[0] - 60, center[1] + 150),
                   cv2.FONT_HERSHEY_SIMPLEX, 1.5, (0, 0, 0), 2)

        # Category scores (bars)
        y_start = 320
        category_scores = scores.get('category_scores', {})

        for i, (category, score) in enumerate(category_scores.items()):
            y = y_start + i * 50

            # Category name
            cv2.putText(dashboard, category.replace('_', ' ').title(),
                       (50, y + 20),
                       cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 0), 1)

            # Score bar
            bar_width = int((score / 100) * 300)
            color = self._score_to_color(score)
            cv2.rectangle(dashboard, (350, y), (350 + bar_width, y + 25), color, -1)
            cv2.rectangle(dashboard, (350, y), (650, y + 25), (0, 0, 0), 2)

            # Score value
            cv2.putText(dashboard, f"{score:.0f}",
                       (660, y + 20),
                       cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 0), 1)

        return dashboard

    @staticmethod
    def _score_to_color(score: float) -> Tuple[int, int, int]:
        """Convert score to BGR color."""
        if score >= 90:
            return (0, 200, 0)  # Green
        elif score >= 75:
            return (0, 255, 200)  # Light green
        elif score >= 60:
            return (0, 255, 255)  # Yellow
        elif score >= 40:
            return (0, 165, 255)  # Orange
        else:
            return (0, 0, 255)  # Red
