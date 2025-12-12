"""
Eye Tracking Simulator
~~~~~~~~~~~~~~~~~~~~~

Simulates human eye tracking patterns when viewing documents.

Based on research:
- F-Shape Pattern And How Users Read (Smashing Magazine, 2024)
- Visual Hierarchy research (Interaction Design Foundation)
- Vigilance Decrement studies (Frontiers in Cognition, 2024)
"""

import numpy as np
from typing import List, Tuple, Dict, Optional
from dataclasses import dataclass
import cv2


@dataclass
class Fixation:
    """Represents an eye fixation point."""

    x: int
    y: int
    duration: float  # milliseconds
    order: int  # Order in sequence
    saliency: float = 0.0  # Saliency value at this point


@dataclass
class Saccade:
    """Represents an eye movement (saccade) between fixations."""

    from_fixation: Fixation
    to_fixation: Fixation
    distance: float  # pixels
    duration: float  # milliseconds
    velocity: float  # pixels/second


class EyeTrackingSimulator:
    """
    Simulates human eye tracking patterns on documents.

    Incorporates:
    - Saliency-driven attention
    - Reading patterns (F, Z, Gutenberg)
    - Cognitive factors (task goals, fatigue)
    - Statistical models of fixation duration and saccade behavior
    """

    def __init__(self, config: Dict):
        """
        Initialize eye tracking simulator.

        Args:
            config: Configuration dictionary
        """
        self.config = config

        # Fixation parameters (based on research)
        self.fixation_duration_mean = config.get('fixation_duration_mean', 250)  # ms
        self.fixation_duration_std = config.get('fixation_duration_std', 50)  # ms

        # Saccade parameters
        self.saccade_velocity = config.get('saccade_velocity', 300)  # pixels/second

        # Attention parameters
        self.attention_weights = {
            'top_left': config.get('top_left_weight', 1.0),
            'top_right': config.get('top_right_weight', 0.6),
            'bottom_left': config.get('bottom_left_weight', 0.3),
            'bottom_right': config.get('bottom_right_weight', 0.8)
        }

        # Vigilance decrement
        self.vigilance_decrement_start = config.get('vigilance_decrement_start', 900)  # 15 min in seconds
        self.max_scan_time = config.get('max_scan_time', 180)  # seconds

    def simulate_scan_path(self,
                          saliency_map: np.ndarray,
                          num_fixations: int = 50,
                          start_position: Optional[Tuple[int, int]] = None) -> Tuple[List[Fixation], List[Saccade]]:
        """
        Simulate a complete scan path (sequence of fixations and saccades).

        Args:
            saliency_map: Saliency map (attention prediction)
            num_fixations: Number of fixations to simulate
            start_position: Starting position (default: top-left)

        Returns:
            (fixations, saccades)
        """
        h, w = saliency_map.shape
        fixations = []
        saccades = []

        # Starting position
        if start_position is None:
            # Most people start near top-left (Primary Optical Area)
            current_x = w // 6
            current_y = h // 6
        else:
            current_x, current_y = start_position

        # Create first fixation
        fixations.append(Fixation(
            x=current_x,
            y=current_y,
            duration=self._sample_fixation_duration(),
            order=0,
            saliency=saliency_map[current_y, current_x] if 0 <= current_y < h and 0 <= current_x < w else 0
        ))

        # Simulate subsequent fixations
        for i in range(1, num_fixations):
            # Select next fixation location based on saliency and distance
            next_x, next_y = self._select_next_fixation(
                current_x, current_y,
                saliency_map,
                visited_fixations=fixations
            )

            # Calculate saccade
            distance = np.sqrt((next_x - current_x)**2 + (next_y - current_y)**2)
            saccade_duration = (distance / self.saccade_velocity) * 1000  # Convert to ms

            saccade = Saccade(
                from_fixation=fixations[-1],
                to_fixation=Fixation(next_x, next_y, 0, i),  # Duration filled below
                distance=distance,
                duration=saccade_duration,
                velocity=self.saccade_velocity
            )
            saccades.append(saccade)

            # Create fixation
            fixation_duration = self._sample_fixation_duration(order=i)
            fixation = Fixation(
                x=next_x,
                y=next_y,
                duration=fixation_duration,
                order=i,
                saliency=saliency_map[next_y, next_x] if 0 <= next_y < h and 0 <= next_x < w else 0
            )
            fixations.append(fixation)

            # Update current position
            current_x, current_y = next_x, next_y

        return fixations, saccades

    def _select_next_fixation(self,
                             current_x: int,
                             current_y: int,
                             saliency_map: np.ndarray,
                             visited_fixations: List[Fixation],
                             exploration_radius: int = 200) -> Tuple[int, int]:
        """
        Select next fixation location based on saliency and exploration.

        Uses a probabilistic model:
        - High saliency = higher probability
        - Reasonable distance (not too far, not too close)
        - Avoid recently visited areas (Inhibition of Return)
        """
        h, w = saliency_map.shape

        # Create probability map
        prob_map = saliency_map.copy()

        # Apply distance bias (prefer medium distances)
        y_coords, x_coords = np.ogrid[:h, :w]
        distance_from_current = np.sqrt((x_coords - current_x)**2 + (y_coords - current_y)**2)

        # Ideal saccade distance is around 50-150 pixels
        distance_bias = np.exp(-((distance_from_current - 100)**2) / (2 * 80**2))
        prob_map *= distance_bias

        # Apply Inhibition of Return (IOR) - reduce probability for visited areas
        for fixation in visited_fixations[-5:]:  # Last 5 fixations
            y, x = fixation.y, fixation.x
            # Create inhibition zone
            ior_mask = np.exp(-((x_coords - x)**2 + (y_coords - y)**2) / (2 * 50**2))
            prob_map *= (1 - 0.7 * ior_mask)

        # Normalize probability map
        prob_map = prob_map / (prob_map.sum() + 1e-10)

        # Sample next location
        flat_prob = prob_map.flatten()
        sampled_idx = np.random.choice(len(flat_prob), p=flat_prob)

        next_y, next_x = np.unravel_index(sampled_idx, (h, w))

        return int(next_x), int(next_y)

    def _sample_fixation_duration(self, order: int = 0) -> float:
        """
        Sample fixation duration from distribution.

        Early fixations tend to be longer (taking in information).
        Later fixations may be shorter (scanning mode).

        Args:
            order: Fixation order (0 = first)

        Returns:
            Duration in milliseconds
        """
        # Base duration from normal distribution
        duration = np.random.normal(self.fixation_duration_mean, self.fixation_duration_std)

        # Ensure positive
        duration = max(duration, 50)

        # Apply vigilance decrement (if simulating long inspection)
        # Later fixations are shorter due to fatigue
        if order > 20:
            fatigue_factor = 0.8 + 0.2 * np.exp(-(order - 20) / 30)
            duration *= fatigue_factor

        return duration

    def calculate_dwell_time_map(self,
                                 fixations: List[Fixation],
                                 shape: Tuple[int, int],
                                 kernel_size: int = 50) -> np.ndarray:
        """
        Calculate dwell time map (total time spent looking at each region).

        Args:
            fixations: List of fixations
            shape: (height, width) of output map
            kernel_size: Size of attention kernel around each fixation

        Returns:
            Dwell time map (in milliseconds)
        """
        h, w = shape
        dwell_map = np.zeros((h, w), dtype=np.float32)

        for fixation in fixations:
            # Create Gaussian kernel around fixation
            y, x = np.ogrid[:h, :w]
            distance = np.sqrt((x - fixation.x)**2 + (y - fixation.y)**2)

            # Gaussian falloff
            kernel = np.exp(-(distance**2) / (2 * kernel_size**2))

            # Add weighted by duration
            dwell_map += kernel * fixation.duration

        return dwell_map

    def identify_attention_hotspots(self,
                                   fixations: List[Fixation],
                                   shape: Tuple[int, int],
                                   threshold_percentile: int = 75) -> List[Tuple[int, int, int, int]]:
        """
        Identify regions that received high attention (hotspots).

        Args:
            fixations: List of fixations
            shape: (height, width) of document
            threshold_percentile: Percentile threshold for hotspot

        Returns:
            List of bounding boxes for hotspots
        """
        dwell_map = self.calculate_dwell_time_map(fixations, shape)

        # Threshold at percentile
        threshold = np.percentile(dwell_map, threshold_percentile)

        # Find contours of high-attention regions
        mask = (dwell_map > threshold).astype(np.uint8)
        contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        hotspots = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            hotspots.append((x, y, x + w, y + h))

        return hotspots

    def identify_cold_spots(self,
                           fixations: List[Fixation],
                           shape: Tuple[int, int],
                           threshold_percentile: int = 25) -> List[Tuple[int, int, int, int]]:
        """
        Identify regions that received little/no attention (potential blind spots).

        These are high-risk areas where critical information might be missed.

        Args:
            fixations: List of fixations
            shape: (height, width) of document
            threshold_percentile: Percentile threshold for cold spot

        Returns:
            List of bounding boxes for cold spots
        """
        dwell_map = self.calculate_dwell_time_map(fixations, shape)

        # Threshold at percentile
        threshold = np.percentile(dwell_map, threshold_percentile)

        # Find contours of low-attention regions
        mask = (dwell_map < threshold).astype(np.uint8)
        contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        cold_spots = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            # Only consider reasonably sized regions
            if w > 20 and h > 20:
                cold_spots.append((x, y, x + w, y + h))

        return cold_spots

    def calculate_scan_efficiency(self, fixations: List[Fixation], shape: Tuple[int, int]) -> Dict:
        """
        Calculate metrics for scan efficiency.

        Metrics:
        - Coverage: % of document scanned
        - Path length: Total saccade distance
        - Revisit ratio: How often areas are revisited
        - Scan time: Total time

        Args:
            fixations: List of fixations
            shape: (height, width) of document

        Returns:
            Dictionary of efficiency metrics
        """
        h, w = shape

        # Calculate coverage
        coverage_map = np.zeros((h, w), dtype=np.uint8)
        for fixation in fixations:
            cv2.circle(coverage_map, (fixation.x, fixation.y), 30, 1, -1)

        coverage_ratio = np.sum(coverage_map > 0) / (h * w)

        # Calculate path length (sum of saccade distances)
        path_length = 0
        for i in range(1, len(fixations)):
            prev = fixations[i-1]
            curr = fixations[i]
            distance = np.sqrt((curr.x - prev.x)**2 + (curr.y - prev.y)**2)
            path_length += distance

        # Calculate revisit ratio
        visited_cells = set()
        revisits = 0
        cell_size = 50  # pixels

        for fixation in fixations:
            cell = (fixation.x // cell_size, fixation.y // cell_size)
            if cell in visited_cells:
                revisits += 1
            else:
                visited_cells.add(cell)

        revisit_ratio = revisits / len(fixations) if fixations else 0

        # Total scan time
        total_time = sum(f.duration for f in fixations)

        return {
            'coverage_ratio': coverage_ratio,
            'path_length': path_length,
            'revisit_ratio': revisit_ratio,
            'total_time_ms': total_time,
            'total_time_s': total_time / 1000,
            'num_fixations': len(fixations),
            'avg_fixation_duration': total_time / len(fixations) if fixations else 0
        }

    def visualize_scan_path(self,
                           image: np.ndarray,
                           fixations: List[Fixation],
                           saccades: List[Saccade],
                           show_order: bool = True) -> np.ndarray:
        """
        Visualize scan path on document image.

        Args:
            image: Document image
            fixations: List of fixations
            saccades: List of saccades
            show_order: Whether to show fixation order numbers

        Returns:
            Visualization image
        """
        vis = image.copy()

        # Draw saccades (lines between fixations)
        for saccade in saccades:
            pt1 = (saccade.from_fixation.x, saccade.from_fixation.y)
            pt2 = (saccade.to_fixation.x, saccade.to_fixation.y)

            # Draw line
            cv2.line(vis, pt1, pt2, (0, 255, 0), 2)

            # Draw arrow
            cv2.arrowedLine(vis, pt1, pt2, (0, 255, 0), 2, tipLength=0.2)

        # Draw fixations (circles)
        for fixation in fixations:
            # Size proportional to duration
            radius = int(5 + fixation.duration / 50)

            # Color based on saliency
            color_intensity = int(fixation.saliency * 255)
            color = (0, color_intensity, 255 - color_intensity)  # Blue to red

            cv2.circle(vis, (fixation.x, fixation.y), radius, color, -1)
            cv2.circle(vis, (fixation.x, fixation.y), radius + 2, (255, 255, 255), 2)

            # Show order
            if show_order:
                cv2.putText(vis, str(fixation.order),
                          (fixation.x + 10, fixation.y - 10),
                          cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 2)

        return vis

    def simulate_vigilance_decrement(self,
                                    fixations: List[Fixation],
                                    inspection_duration_s: float) -> Dict:
        """
        Simulate vigilance decrement effect.

        Based on research:
        - Vigilance decrement starts around 15 minutes
        - Performance degradation in quality inspection tasks

        Args:
            fixations: List of fixations
            inspection_duration_s: Total inspection duration in seconds

        Returns:
            Dict with vigilance metrics and degradation analysis
        """
        # Calculate when vigilance decrement starts affecting performance
        vigilance_onset = self.vigilance_decrement_start

        if inspection_duration_s < vigilance_onset:
            return {
                'vigilance_affected': False,
                'performance_degradation': 0.0,
                'risk_level': 'low'
            }

        # Calculate performance degradation
        # Exponential decay after onset
        time_over_threshold = inspection_duration_s - vigilance_onset
        degradation = 1 - np.exp(-time_over_threshold / 600)  # 10-minute half-life

        risk_level = 'low'
        if degradation > 0.5:
            risk_level = 'critical'
        elif degradation > 0.3:
            risk_level = 'high'
        elif degradation > 0.1:
            risk_level = 'medium'

        return {
            'vigilance_affected': True,
            'performance_degradation': degradation,
            'risk_level': risk_level,
            'recommended_break': True if degradation > 0.3 else False
        }
