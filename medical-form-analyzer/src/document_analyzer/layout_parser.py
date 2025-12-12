"""
Layout Parser
~~~~~~~~~~~~~

Detects and analyzes document layout using deep learning models.
Uses LayoutParser library with pre-trained models.
"""

import cv2
import numpy as np
from typing import List, Dict, Tuple, Optional
from dataclasses import dataclass
import layoutparser as lp
from PIL import Image


@dataclass
class LayoutElement:
    """Represents a detected layout element in the document."""

    type: str  # table, text, title, list, figure, checkbox, signature
    bbox: Tuple[int, int, int, int]  # (x1, y1, x2, y2)
    confidence: float
    text: Optional[str] = None
    properties: Optional[Dict] = None

    @property
    def center(self) -> Tuple[int, int]:
        """Calculate center point of the element."""
        x1, y1, x2, y2 = self.bbox
        return ((x1 + x2) // 2, (y1 + y2) // 2)

    @property
    def area(self) -> int:
        """Calculate area of the element."""
        x1, y1, x2, y2 = self.bbox
        return (x2 - x1) * (y2 - y1)

    @property
    def width(self) -> int:
        """Width of the element."""
        return self.bbox[2] - self.bbox[0]

    @property
    def height(self) -> int:
        """Height of the element."""
        return self.bbox[3] - self.bbox[1]


class LayoutAnalyzer:
    """
    Analyzes document layout using deep learning.

    Based on research:
    - LayoutParser: A Unified Toolkit for DL-Based Document Image Analysis
    - PubLayNet dataset for document layout understanding
    """

    def __init__(self, config: Dict):
        """
        Initialize the layout analyzer.

        Args:
            config: Configuration dictionary with model settings
        """
        self.config = config
        self.model_path = config.get('layout_model',
                                     'lp://PubLayNet/mask_rcnn_X_101_32x8d_FPN_3x/config')
        self.confidence_threshold = config.get('confidence_threshold', 0.5)

        # Initialize model
        try:
            self.model = lp.Detectron2LayoutModel(
                self.model_path,
                extra_config=["MODEL.ROI_HEADS.SCORE_THRESH_TEST", self.confidence_threshold],
                label_map={0: "Text", 1: "Title", 2: "List", 3: "Table", 4: "Figure"}
            )
        except Exception as e:
            print(f"Warning: Could not load LayoutParser model: {e}")
            print("Falling back to basic layout detection.")
            self.model = None

    def analyze(self, image: np.ndarray) -> List[LayoutElement]:
        """
        Analyze document layout and detect elements.

        Args:
            image: Input document image (BGR format)

        Returns:
            List of detected layout elements
        """
        elements = []

        if self.model is not None:
            # Use deep learning model
            elements.extend(self._detect_with_model(image))
        else:
            # Fallback to heuristic detection
            elements.extend(self._detect_heuristic(image))

        # Detect special elements (checkboxes, signature fields)
        if self.config.get('detect_checkboxes', True):
            elements.extend(self._detect_checkboxes(image))

        if self.config.get('detect_signature_fields', True):
            elements.extend(self._detect_signature_fields(image))

        # Sort elements by reading order (top-to-bottom, left-to-right)
        elements = self._sort_by_reading_order(elements)

        return elements

    def _detect_with_model(self, image: np.ndarray) -> List[LayoutElement]:
        """Detect layout elements using deep learning model."""
        # Convert to RGB for LayoutParser
        image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

        # Detect layout
        layout = self.model.detect(image_rgb)

        elements = []
        for block in layout:
            element = LayoutElement(
                type=block.type,
                bbox=(int(block.block.x_1), int(block.block.y_1),
                      int(block.block.x_2), int(block.block.y_2)),
                confidence=block.score
            )
            elements.append(element)

        return elements

    def _detect_heuristic(self, image: np.ndarray) -> List[LayoutElement]:
        """
        Fallback heuristic detection using traditional CV.
        Detects text blocks, tables using contours and morphological operations.
        """
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)

        elements = []

        # Detect horizontal and vertical lines (for tables)
        horizontal_kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (40, 1))
        vertical_kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (1, 40))

        # Detect horizontal lines
        horizontal_lines = cv2.morphologyEx(gray, cv2.MORPH_OPEN, horizontal_kernel)
        horizontal_contours, _ = cv2.findContours(horizontal_lines, cv2.RETR_EXTERNAL,
                                                   cv2.CHAIN_APPROX_SIMPLE)

        # Detect vertical lines
        vertical_lines = cv2.morphologyEx(gray, cv2.MORPH_OPEN, vertical_kernel)
        vertical_contours, _ = cv2.findContours(vertical_lines, cv2.RETR_EXTERNAL,
                                                 cv2.CHAIN_APPROX_SIMPLE)

        # Combine to detect tables
        table_mask = cv2.add(horizontal_lines, vertical_lines)
        table_contours, _ = cv2.findContours(table_mask, cv2.RETR_EXTERNAL,
                                              cv2.CHAIN_APPROX_SIMPLE)

        for contour in table_contours:
            x, y, w, h = cv2.boundingRect(contour)
            if w > 100 and h > 50:  # Minimum table size
                element = LayoutElement(
                    type="Table",
                    bbox=(x, y, x + w, y + h),
                    confidence=0.8
                )
                elements.append(element)

        # Detect text blocks using MSER
        mser = cv2.MSER_create()
        regions, _ = mser.detectRegions(gray)

        # Group nearby text regions
        text_blocks = self._group_text_regions(regions)
        for bbox in text_blocks:
            element = LayoutElement(
                type="Text",
                bbox=bbox,
                confidence=0.7
            )
            elements.append(element)

        return elements

    def _detect_checkboxes(self, image: np.ndarray) -> List[LayoutElement]:
        """
        Detect checkbox elements using square detection.
        Checkboxes are critical for habituation error analysis.
        """
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        edges = cv2.Canny(gray, 50, 150)

        # Find contours
        contours, _ = cv2.findContours(edges, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)

        checkboxes = []
        for contour in contours:
            # Approximate contour to polygon
            peri = cv2.arcLength(contour, True)
            approx = cv2.approxPolyDP(contour, 0.04 * peri, True)

            # Check if it's a square/rectangle
            if len(approx) == 4:
                x, y, w, h = cv2.boundingRect(contour)

                # Check if it's checkbox-sized (typically 10-30 pixels)
                if 10 <= w <= 30 and 10 <= h <= 30:
                    aspect_ratio = float(w) / h

                    # Square-ish shape
                    if 0.8 <= aspect_ratio <= 1.2:
                        element = LayoutElement(
                            type="Checkbox",
                            bbox=(x, y, x + w, y + h),
                            confidence=0.85,
                            properties={'size': (w, h)}
                        )
                        checkboxes.append(element)

        return checkboxes

    def _detect_signature_fields(self, image: np.ndarray) -> List[LayoutElement]:
        """
        Detect signature fields (typically long horizontal lines).
        Signature fields are critical for compliance.
        """
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        edges = cv2.Canny(gray, 50, 150)

        # Detect lines using HoughLinesP
        lines = cv2.HoughLinesP(edges, 1, np.pi / 180, threshold=100,
                                minLineLength=100, maxLineGap=10)

        signature_fields = []
        if lines is not None:
            for line in lines:
                x1, y1, x2, y2 = line[0]

                # Check if it's a horizontal line
                if abs(y2 - y1) < 5:
                    length = abs(x2 - x1)

                    # Long enough for signature (typically > 150 pixels)
                    if length > 150:
                        element = LayoutElement(
                            type="SignatureField",
                            bbox=(min(x1, x2), y1 - 5, max(x1, x2), y1 + 5),
                            confidence=0.75,
                            properties={'length': length}
                        )
                        signature_fields.append(element)

        return signature_fields

    def _group_text_regions(self, regions: List) -> List[Tuple[int, int, int, int]]:
        """Group nearby text regions into text blocks."""
        if not regions:
            return []

        # Get bounding boxes
        bboxes = [cv2.boundingRect(region.reshape(-1, 1, 2)) for region in regions]

        # Simple grouping by proximity
        grouped = []
        used = set()

        for i, bbox1 in enumerate(bboxes):
            if i in used:
                continue

            group = [bbox1]
            used.add(i)

            for j, bbox2 in enumerate(bboxes):
                if j in used:
                    continue

                # Check proximity
                if self._are_nearby(bbox1, bbox2, threshold=50):
                    group.append(bbox2)
                    used.add(j)

            # Merge group into single bbox
            merged = self._merge_bboxes(group)
            grouped.append(merged)

        return grouped

    @staticmethod
    def _are_nearby(bbox1: Tuple, bbox2: Tuple, threshold: int = 50) -> bool:
        """Check if two bounding boxes are nearby."""
        x1, y1, w1, h1 = bbox1
        x2, y2, w2, h2 = bbox2

        center1 = (x1 + w1 // 2, y1 + h1 // 2)
        center2 = (x2 + w2 // 2, y2 + h2 // 2)

        distance = np.sqrt((center1[0] - center2[0])**2 + (center1[1] - center2[1])**2)
        return distance < threshold

    @staticmethod
    def _merge_bboxes(bboxes: List[Tuple]) -> Tuple[int, int, int, int]:
        """Merge multiple bounding boxes into one."""
        x_min = min(bbox[0] for bbox in bboxes)
        y_min = min(bbox[1] for bbox in bboxes)
        x_max = max(bbox[0] + bbox[2] for bbox in bboxes)
        y_max = max(bbox[1] + bbox[3] for bbox in bboxes)

        return (x_min, y_min, x_max, y_max)

    @staticmethod
    def _sort_by_reading_order(elements: List[LayoutElement]) -> List[LayoutElement]:
        """
        Sort elements by typical reading order (top-to-bottom, left-to-right).

        Based on research on reading patterns (F-pattern, Z-pattern).
        """
        return sorted(elements, key=lambda e: (e.bbox[1], e.bbox[0]))

    def calculate_density(self, image: np.ndarray,
                         elements: List[LayoutElement],
                         grid_size: int = 50) -> np.ndarray:
        """
        Calculate information density across the document.

        High density areas are prone to cognitive overload.

        Args:
            image: Input image
            elements: Detected layout elements
            grid_size: Grid cell size for density calculation

        Returns:
            Density map (2D array)
        """
        height, width = image.shape[:2]
        grid_h = height // grid_size + 1
        grid_w = width // grid_size + 1

        density_map = np.zeros((grid_h, grid_w), dtype=np.float32)

        for element in elements:
            x1, y1, x2, y2 = element.bbox

            # Convert to grid coordinates
            gx1 = x1 // grid_size
            gy1 = y1 // grid_size
            gx2 = min(x2 // grid_size + 1, grid_w)
            gy2 = min(y2 // grid_size + 1, grid_h)

            # Increment density
            density_map[gy1:gy2, gx1:gx2] += 1

        # Normalize
        if density_map.max() > 0:
            density_map = density_map / density_map.max()

        return density_map

    def classify_writable_areas(self, elements: List[LayoutElement]) -> Dict[str, List[LayoutElement]]:
        """
        Classify elements into read-only and writable areas.

        Writable areas are where human errors typically occur.

        Returns:
            Dictionary with 'read_only' and 'writable' lists
        """
        writable_types = {'Checkbox', 'SignatureField', 'TextField'}

        classification = {
            'read_only': [],
            'writable': []
        }

        for element in elements:
            if element.type in writable_types:
                classification['writable'].append(element)
            else:
                classification['read_only'].append(element)

        return classification
