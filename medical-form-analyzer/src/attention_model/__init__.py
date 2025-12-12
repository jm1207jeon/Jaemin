"""
Attention Model Module
~~~~~~~~~~~~~~~~~~~~~~

Models visual attention and eye tracking patterns based on cognitive science research.
"""

from .saliency_map import SaliencyMapGenerator
from .eye_tracking_simulator import EyeTrackingSimulator
from .visual_attention import VisualAttentionAnalyzer

__all__ = ["SaliencyMapGenerator", "EyeTrackingSimulator", "VisualAttentionAnalyzer"]
