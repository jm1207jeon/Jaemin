"""
Saliency Map Generator
~~~~~~~~~~~~~~~~~~~~~

Generates saliency maps to predict where humans will look first.

Based on research:
- DeepGaze III: Modeling free-viewing human scanpaths (PMC 9055565)
- Deep saliency models learn low-, mid-, and high-level features (Nature Scientific Reports)
- Itti-Koch model for visual attention
"""

import cv2
import numpy as np
from typing import Tuple, Optional, Dict
import torch
import torch.nn as nn
import torch.nn.functional as F
from scipy.ndimage import gaussian_filter


class SaliencyMapGenerator:
    """
    Generates saliency maps to predict visual attention.

    Supports multiple models:
    - DeepGaze (deep learning-based, state-of-the-art)
    - Itti-Koch (traditional, computationally efficient)
    - Spectral Residual (fast, decent performance)
    """

    def __init__(self, config: Dict):
        """
        Initialize saliency map generator.

        Args:
            config: Configuration dictionary
        """
        self.config = config
        self.model_name = config.get('saliency_model', 'itti_koch')
        self.resolution = tuple(config.get('saliency_resolution', [384, 512]))
        self.blur_sigma = config.get('blur_sigma', 20)

        # Initialize model
        if self.model_name == 'deepgaze3':
            self.model = self._load_deepgaze_model()
        elif self.model_name == 'itti_koch':
            self.model = None  # Itti-Koch doesn't use a model
        else:
            print(f"Warning: Unknown saliency model '{self.model_name}'. Using Itti-Koch.")
            self.model = None
            self.model_name = 'itti_koch'

    def _load_deepgaze_model(self):
        """
        Load DeepGaze model.

        Note: In a production system, you would download pre-trained weights.
        For this implementation, we'll use a simplified version.
        """
        try:
            # In production, load actual DeepGaze weights
            # For now, return None and use fallback
            print("Note: Using simplified saliency model. For production, integrate DeepGaze III.")
            return None
        except Exception as e:
            print(f"Could not load DeepGaze model: {e}")
            return None

    def generate(self, image: np.ndarray) -> np.ndarray:
        """
        Generate saliency map for input image.

        Args:
            image: Input image (BGR format)

        Returns:
            Saliency map (normalized 0-1, same size as input)
        """
        if self.model_name == 'deepgaze3' and self.model is not None:
            saliency = self._generate_deepgaze(image)
        elif self.model_name == 'itti_koch':
            saliency = self._generate_itti_koch(image)
        elif self.model_name == 'spectral_residual':
            saliency = self._generate_spectral_residual(image)
        else:
            # Fallback to Itti-Koch
            saliency = self._generate_itti_koch(image)

        # Resize to match input image
        if saliency.shape[:2] != image.shape[:2]:
            saliency = cv2.resize(saliency, (image.shape[1], image.shape[0]))

        # Normalize
        saliency = self._normalize(saliency)

        # Apply Gaussian blur (eye fixations are not pixel-precise)
        saliency = gaussian_filter(saliency, sigma=self.blur_sigma)
        saliency = self._normalize(saliency)

        return saliency

    def _generate_deepgaze(self, image: np.ndarray) -> np.ndarray:
        """Generate saliency using DeepGaze model."""
        # Placeholder for DeepGaze implementation
        # In production, this would use the actual DeepGaze III model
        return self._generate_itti_koch(image)

    def _generate_itti_koch(self, image: np.ndarray) -> np.ndarray:
        """
        Generate saliency using Itti-Koch model.

        Based on:
        - A Model of Saliency-Based Visual Attention (Itti, Koch, Niebur, 1998)

        Features:
        - Color opponency (red-green, blue-yellow)
        - Intensity
        - Orientation (0°, 45°, 90°, 135°)
        """
        # Convert to Lab color space
        lab = cv2.cvtColor(image, cv2.COLOR_BGR2LAB)
        l_channel, a_channel, b_channel = cv2.split(lab)

        # 1. Intensity conspicuity
        intensity_map = self._compute_intensity_conspicuity(l_channel)

        # 2. Color conspicuity
        color_map = self._compute_color_conspicuity(a_channel, b_channel)

        # 3. Orientation conspicuity
        orientation_map = self._compute_orientation_conspicuity(l_channel)

        # Combine conspicuity maps
        saliency = (intensity_map + color_map + orientation_map) / 3.0

        return saliency

    def _compute_intensity_conspicuity(self, intensity: np.ndarray) -> np.ndarray:
        """Compute intensity conspicuity map."""
        # Create Gaussian pyramid
        pyramid = self._create_gaussian_pyramid(intensity, levels=5)

        # Center-surround differences
        conspicuity = np.zeros_like(intensity, dtype=np.float32)

        for c in range(2, 5):  # Center scales
            for s in range(c + 3, min(c + 5, 5)):  # Surround scales
                center = pyramid[c]
                surround = cv2.resize(pyramid[s], (center.shape[1], center.shape[0]))

                diff = np.abs(center.astype(np.float32) - surround.astype(np.float32))
                diff_resized = cv2.resize(diff, (intensity.shape[1], intensity.shape[0]))
                conspicuity += diff_resized

        return conspicuity

    def _compute_color_conspicuity(self, a_channel: np.ndarray, b_channel: np.ndarray) -> np.ndarray:
        """Compute color conspicuity map."""
        # Red-Green and Blue-Yellow opponency
        rg = np.abs(a_channel.astype(np.float32) - 128)  # Center around 0
        by = np.abs(b_channel.astype(np.float32) - 128)

        # Combine
        color_map = rg + by

        return color_map

    def _compute_orientation_conspicuity(self, intensity: np.ndarray) -> np.ndarray:
        """Compute orientation conspicuity map."""
        # Gabor filters at different orientations
        orientations = [0, 45, 90, 135]
        orientation_maps = []

        for angle in orientations:
            # Create Gabor kernel
            kernel = cv2.getGaborKernel(
                ksize=(21, 21),
                sigma=5.0,
                theta=np.deg2rad(angle),
                lambd=10.0,
                gamma=0.5,
                psi=0
            )

            # Apply filter
            filtered = cv2.filter2D(intensity, cv2.CV_32F, kernel)
            orientation_maps.append(np.abs(filtered))

        # Combine all orientations
        orientation_conspicuity = np.mean(orientation_maps, axis=0)

        return orientation_conspicuity

    @staticmethod
    def _create_gaussian_pyramid(image: np.ndarray, levels: int = 5) -> list:
        """Create Gaussian pyramid."""
        pyramid = [image]
        current = image

        for _ in range(levels - 1):
            current = cv2.pyrDown(current)
            pyramid.append(current)

        return pyramid

    def _generate_spectral_residual(self, image: np.ndarray) -> np.ndarray:
        """
        Generate saliency using Spectral Residual method.

        Fast and simple method based on frequency domain analysis.
        """
        # Convert to grayscale
        if len(image.shape) == 3:
            gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        else:
            gray = image

        # FFT
        fft = np.fft.fft2(gray)
        amplitude = np.abs(fft)
        phase = np.angle(fft)

        # Log amplitude
        log_amplitude = np.log(amplitude + 1e-5)

        # Spectral residual
        residual = log_amplitude - cv2.blur(log_amplitude, (3, 3))

        # Inverse FFT
        saliency_fft = np.exp(residual) * np.exp(1j * phase)
        saliency = np.abs(np.fft.ifft2(saliency_fft)) ** 2

        return saliency

    @staticmethod
    def _normalize(array: np.ndarray) -> np.ndarray:
        """Normalize array to 0-1 range."""
        min_val = array.min()
        max_val = array.max()

        if max_val - min_val > 0:
            return (array - min_val) / (max_val - min_val)
        else:
            return np.zeros_like(array)

    def apply_bias(self, saliency: np.ndarray, bias_map: np.ndarray, weight: float = 0.5) -> np.ndarray:
        """
        Apply cognitive bias to saliency map.

        Biases can include:
        - Reading patterns (F-pattern, Z-pattern)
        - Center bias (people look at center more)
        - Task-specific bias (looking for specific elements)

        Args:
            saliency: Base saliency map
            bias_map: Bias map (same size as saliency)
            weight: Weight of bias (0-1)

        Returns:
            Combined saliency map
        """
        # Combine saliency with bias
        combined = (1 - weight) * saliency + weight * bias_map

        return self._normalize(combined)

    def create_center_bias(self, shape: Tuple[int, int]) -> np.ndarray:
        """
        Create center bias map.

        People tend to look at the center of documents more.

        Args:
            shape: (height, width) of output map

        Returns:
            Center bias map (normalized 0-1)
        """
        h, w = shape
        y, x = np.ogrid[:h, :w]

        center_y, center_x = h // 2, w // 2

        # Gaussian centered at image center
        sigma_y, sigma_x = h / 3, w / 3

        bias = np.exp(-((x - center_x) ** 2 / (2 * sigma_x ** 2) +
                       (y - center_y) ** 2 / (2 * sigma_y ** 2)))

        return self._normalize(bias)

    def create_reading_pattern_bias(self,
                                    shape: Tuple[int, int],
                                    pattern: str = 'f_pattern') -> np.ndarray:
        """
        Create bias map based on reading patterns.

        Patterns:
        - f_pattern: Horizontal at top, then vertical down left side
        - z_pattern: Zigzag from top-left to bottom-right
        - gutenberg: Four quadrants with different weights

        Args:
            shape: (height, width) of output map
            pattern: Reading pattern type

        Returns:
            Reading pattern bias map
        """
        h, w = shape
        bias = np.zeros((h, w), dtype=np.float32)

        if pattern == 'f_pattern':
            # Strong attention at top
            bias[:h//4, :] = 1.0
            # Medium attention at top-middle
            bias[h//4:h//2, :w//2] = 0.7
            # Vertical attention on left
            bias[:, :w//5] = 0.8

        elif pattern == 'z_pattern':
            # Diagonal Z shape
            # Top horizontal
            bias[:h//5, :] = 1.0
            # Middle diagonal
            for i in range(h):
                start_col = int(i * w / h)
                end_col = min(start_col + w//10, w)
                bias[i, start_col:end_col] = 0.8
            # Bottom horizontal
            bias[4*h//5:, :] = 1.0

        elif pattern == 'gutenberg':
            # Four quadrants (Primary, Strong Deceleration, Weak Fallow, Terminal)
            # Top-left: Primary Optical Area (highest attention)
            bias[:h//2, :w//2] = 1.0
            # Top-right: Strong Deceleration
            bias[:h//2, w//2:] = 0.6
            # Bottom-left: Weak Fallow (blind spot)
            bias[h//2:, :w//2] = 0.3
            # Bottom-right: Terminal Area
            bias[h//2:, w//2:] = 0.8

        # Smooth transitions
        bias = gaussian_filter(bias, sigma=min(h, w) / 20)

        return self._normalize(bias)

    def combine_saliency_with_patterns(self,
                                      image: np.ndarray,
                                      patterns: list = None,
                                      pattern_weight: float = 0.3) -> np.ndarray:
        """
        Generate saliency map and combine with reading patterns.

        This creates a realistic attention prediction that considers both
        bottom-up (image features) and top-down (reading habits) factors.

        Args:
            image: Input image
            patterns: List of reading patterns to apply
            pattern_weight: Weight of reading patterns vs image saliency

        Returns:
            Combined saliency map
        """
        if patterns is None:
            patterns = ['f_pattern', 'center']

        # Generate base saliency from image
        saliency = self.generate(image)

        # Combine with reading patterns
        h, w = saliency.shape

        # Create combined bias
        combined_bias = np.zeros((h, w), dtype=np.float32)

        for pattern in patterns:
            if pattern == 'center':
                bias = self.create_center_bias((h, w))
            else:
                bias = self.create_reading_pattern_bias((h, w), pattern)

            combined_bias += bias

        # Normalize combined bias
        combined_bias = self._normalize(combined_bias)

        # Combine with saliency
        final_saliency = self.apply_bias(saliency, combined_bias, pattern_weight)

        return final_saliency

    def identify_blind_spots(self,
                            saliency: np.ndarray,
                            threshold: float = 0.2) -> np.ndarray:
        """
        Identify potential blind spots (areas unlikely to receive attention).

        Args:
            saliency: Saliency map
            threshold: Saliency threshold below which is considered blind spot

        Returns:
            Binary mask (1 = blind spot, 0 = visible)
        """
        blind_spots = (saliency < threshold).astype(np.uint8)

        return blind_spots

    def visualize_saliency(self, image: np.ndarray, saliency: np.ndarray) -> np.ndarray:
        """
        Create visualization of saliency map overlaid on image.

        Args:
            image: Original image
            saliency: Saliency map

        Returns:
            Visualization image
        """
        # Convert saliency to heatmap
        saliency_uint8 = (saliency * 255).astype(np.uint8)
        heatmap = cv2.applyColorMap(saliency_uint8, cv2.COLORMAP_JET)

        # Overlay on image
        overlay = cv2.addWeighted(image, 0.6, heatmap, 0.4, 0)

        return overlay
