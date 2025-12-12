"""
Structure Detector
~~~~~~~~~~~~~~~~~~

Detects document structure, hierarchy, and relationships between elements.
Identifies tables, forms, sections, and their logical organization.
"""

import cv2
import numpy as np
from typing import List, Dict, Tuple, Optional
from dataclasses import dataclass, field
from .layout_parser import LayoutElement
from .ocr_engine import TextRegion


@dataclass
class DocumentSection:
    """Represents a logical section of the document."""

    title: str
    bbox: Tuple[int, int, int, int]
    elements: List[LayoutElement] = field(default_factory=list)
    subsections: List['DocumentSection'] = field(default_factory=list)
    level: int = 0

    @property
    def element_count(self) -> int:
        """Count of elements in this section."""
        return len(self.elements)


@dataclass
class TableStructure:
    """Represents a detected table with rows and columns."""

    bbox: Tuple[int, int, int, int]
    rows: int
    columns: int
    cells: List[Tuple[int, int, int, int]] = field(default_factory=list)
    headers: List[str] = field(default_factory=list)


@dataclass
class FormStructure:
    """Represents a form with fields and labels."""

    bbox: Tuple[int, int, int, int]
    fields: List[Dict] = field(default_factory=list)  # {label, field_bbox, field_type}


class StructureDetector:
    """
    Detects and analyzes document structure.

    Capabilities:
    - Hierarchical section detection
    - Table structure analysis
    - Form field relationships
    - Reading order determination
    """

    def __init__(self, config: Dict):
        """
        Initialize structure detector.

        Args:
            config: Configuration dictionary
        """
        self.config = config

    def analyze_structure(self,
                         image: np.ndarray,
                         elements: List[LayoutElement],
                         text_regions: List[TextRegion]) -> Dict:
        """
        Analyze overall document structure.

        Args:
            image: Document image
            elements: Detected layout elements
            text_regions: Extracted text regions

        Returns:
            Dictionary containing structure analysis results
        """
        # Detect sections
        sections = self._detect_sections(elements, text_regions)

        # Detect tables
        tables = self._analyze_tables(image, elements)

        # Detect forms
        forms = self._detect_forms(elements, text_regions)

        # Calculate structural metrics
        metrics = self._calculate_structure_metrics(sections, tables, forms)

        return {
            'sections': sections,
            'tables': tables,
            'forms': forms,
            'metrics': metrics
        }

    def _detect_sections(self,
                        elements: List[LayoutElement],
                        text_regions: List[TextRegion]) -> List[DocumentSection]:
        """
        Detect logical sections based on titles and spatial layout.

        Uses heuristics:
        - Larger text = likely a title
        - Horizontal lines = section separators
        - Whitespace = section boundaries
        """
        sections = []

        # Find title elements (typically larger, bold text)
        title_candidates = [e for e in elements if e.type == "Title"]

        # If no titles detected, try to infer from text size
        if not title_candidates:
            title_candidates = self._infer_titles_from_text(text_regions)

        # Create sections based on titles
        for i, title in enumerate(title_candidates):
            # Determine section boundary
            if i < len(title_candidates) - 1:
                next_title = title_candidates[i + 1]
                section_bottom = next_title.bbox[1]
            else:
                section_bottom = max([e.bbox[3] for e in elements]) if elements else 0

            section_bbox = (
                0,
                title.bbox[1],
                max([e.bbox[2] for e in elements]) if elements else 0,
                section_bottom
            )

            # Find elements in this section
            section_elements = [
                e for e in elements
                if self._is_within_vertical_range(e.bbox, title.bbox[1], section_bottom)
            ]

            section = DocumentSection(
                title=title.text if hasattr(title, 'text') else "Section",
                bbox=section_bbox,
                elements=section_elements,
                level=0
            )
            sections.append(section)

        return sections

    def _infer_titles_from_text(self, text_regions: List[TextRegion]) -> List[LayoutElement]:
        """Infer titles from text regions based on font size."""
        if not text_regions:
            return []

        # Calculate font sizes
        font_sizes = [(r.bbox[3] - r.bbox[1]) for r in text_regions]
        if not font_sizes:
            return []

        # Find outliers (larger text)
        mean_size = np.mean(font_sizes)
        std_size = np.std(font_sizes)

        titles = []
        for region in text_regions:
            height = region.bbox[3] - region.bbox[1]
            if height > mean_size + 1.5 * std_size:  # 1.5 sigma threshold
                title = LayoutElement(
                    type="Title",
                    bbox=region.bbox,
                    confidence=0.7,
                    text=region.text
                )
                titles.append(title)

        return titles

    def _analyze_tables(self,
                       image: np.ndarray,
                       elements: List[LayoutElement]) -> List[TableStructure]:
        """
        Analyze table structure (rows, columns, cells).

        Detection strategy:
        1. Find table elements from layout detection
        2. Detect grid lines
        3. Identify cells
        4. Extract headers
        """
        tables = []

        table_elements = [e for e in elements if e.type == "Table"]

        for table_elem in table_elements:
            x1, y1, x2, y2 = table_elem.bbox
            table_roi = image[y1:y2, x1:x2]

            # Detect rows and columns
            rows, columns = self._detect_table_grid(table_roi)

            # Detect cells
            cells = self._detect_table_cells(table_roi, rows, columns)

            # Adjust cell coordinates to image space
            cells_adjusted = [(x1 + cx1, y1 + cy1, x1 + cx2, y1 + cy2)
                            for (cx1, cy1, cx2, cy2) in cells]

            table = TableStructure(
                bbox=table_elem.bbox,
                rows=len(rows),
                columns=len(columns),
                cells=cells_adjusted,
                headers=[]  # TODO: Extract headers from first row
            )
            tables.append(table)

        return tables

    def _detect_table_grid(self, table_roi: np.ndarray) -> Tuple[List[int], List[int]]:
        """
        Detect table grid lines (rows and columns).

        Returns:
            (row_positions, column_positions)
        """
        gray = cv2.cvtColor(table_roi, cv2.COLOR_BGR2GRAY) if len(table_roi.shape) == 3 else table_roi

        # Detect horizontal lines (rows)
        horizontal_kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (40, 1))
        horizontal = cv2.morphologyEx(gray, cv2.MORPH_OPEN, horizontal_kernel)

        # Find horizontal line positions
        horizontal_sum = np.sum(horizontal, axis=1)
        row_positions = np.where(horizontal_sum > 0.5 * horizontal.shape[1] * 255)[0].tolist()

        # Detect vertical lines (columns)
        vertical_kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (1, 40))
        vertical = cv2.morphologyEx(gray, cv2.MORPH_OPEN, vertical_kernel)

        # Find vertical line positions
        vertical_sum = np.sum(vertical, axis=0)
        col_positions = np.where(vertical_sum > 0.5 * vertical.shape[0] * 255)[0].tolist()

        return row_positions, col_positions

    def _detect_table_cells(self,
                           table_roi: np.ndarray,
                           rows: List[int],
                           cols: List[int]) -> List[Tuple[int, int, int, int]]:
        """
        Detect individual table cells based on grid lines.

        Args:
            table_roi: Table region of interest
            rows: Row positions
            cols: Column positions

        Returns:
            List of cell bounding boxes
        """
        cells = []

        # Group consecutive positions (to handle thick lines)
        row_groups = self._group_consecutive(rows, gap=5)
        col_groups = self._group_consecutive(cols, gap=5)

        # Create cells from grid intersections
        for i in range(len(row_groups) - 1):
            for j in range(len(col_groups) - 1):
                cell = (
                    col_groups[j],
                    row_groups[i],
                    col_groups[j + 1],
                    row_groups[i + 1]
                )
                cells.append(cell)

        return cells

    @staticmethod
    def _group_consecutive(positions: List[int], gap: int = 5) -> List[int]:
        """Group consecutive positions (handle thick lines)."""
        if not positions:
            return []

        groups = []
        current_group = [positions[0]]

        for pos in positions[1:]:
            if pos - current_group[-1] <= gap:
                current_group.append(pos)
            else:
                # Take middle of group
                groups.append(int(np.mean(current_group)))
                current_group = [pos]

        # Add last group
        groups.append(int(np.mean(current_group)))

        return groups

    def _detect_forms(self,
                     elements: List[LayoutElement],
                     text_regions: List[TextRegion]) -> List[FormStructure]:
        """
        Detect form structures (label-field pairs).

        Form fields typically have:
        - A label (text)
        - An input field (checkbox, text field, signature)
        - Close proximity
        """
        forms = []

        # Find writable elements
        writable_elements = [
            e for e in elements
            if e.type in ['Checkbox', 'SignatureField', 'TextField']
        ]

        # Group nearby writable elements
        form_groups = self._group_nearby_elements(writable_elements, threshold=200)

        for group in form_groups:
            # Find bounding box of group
            x_min = min(e.bbox[0] for e in group)
            y_min = min(e.bbox[1] for e in group)
            x_max = max(e.bbox[2] for e in group)
            y_max = max(e.bbox[3] for e in group)

            form_bbox = (x_min, y_min, x_max, y_max)

            # Find labels for each field
            fields = []
            for element in group:
                label = self._find_label_for_field(element, text_regions)
                fields.append({
                    'label': label,
                    'field_bbox': element.bbox,
                    'field_type': element.type
                })

            form = FormStructure(
                bbox=form_bbox,
                fields=fields
            )
            forms.append(form)

        return forms

    def _find_label_for_field(self,
                             field: LayoutElement,
                             text_regions: List[TextRegion]) -> str:
        """
        Find the text label associated with a form field.

        Heuristic: Label is typically the nearest text to the left or above the field.
        """
        field_center = field.center

        # Find nearby text
        candidates = []
        for text in text_regions:
            text_center = (
                (text.bbox[0] + text.bbox[2]) // 2,
                (text.bbox[1] + text.bbox[3]) // 2
            )

            # Calculate distance
            distance = np.sqrt(
                (field_center[0] - text_center[0]) ** 2 +
                (field_center[1] - text_center[1]) ** 2
            )

            # Check if text is to the left or above
            is_left = text_center[0] < field_center[0]
            is_above = text_center[1] < field_center[1]

            if (is_left or is_above) and distance < 150:
                candidates.append((distance, text.text))

        if candidates:
            # Return closest
            candidates.sort(key=lambda x: x[0])
            return candidates[0][1]

        return "Unknown"

    @staticmethod
    def _group_nearby_elements(elements: List[LayoutElement],
                               threshold: int = 200) -> List[List[LayoutElement]]:
        """Group nearby elements into clusters."""
        if not elements:
            return []

        groups = []
        used = set()

        for i, elem1 in enumerate(elements):
            if i in used:
                continue

            group = [elem1]
            used.add(i)

            for j, elem2 in enumerate(elements):
                if j in used:
                    continue

                # Calculate distance between centers
                dist = np.sqrt(
                    (elem1.center[0] - elem2.center[0]) ** 2 +
                    (elem1.center[1] - elem2.center[1]) ** 2
                )

                if dist < threshold:
                    group.append(elem2)
                    used.add(j)

            groups.append(group)

        return groups

    @staticmethod
    def _is_within_vertical_range(bbox: Tuple, y_min: int, y_max: int) -> bool:
        """Check if bounding box is within vertical range."""
        elem_y_center = (bbox[1] + bbox[3]) // 2
        return y_min <= elem_y_center <= y_max

    def _calculate_structure_metrics(self,
                                    sections: List[DocumentSection],
                                    tables: List[TableStructure],
                                    forms: List[FormStructure]) -> Dict:
        """
        Calculate structural complexity metrics.

        Metrics:
        - Section count
        - Average elements per section
        - Table complexity (rows × columns)
        - Form complexity (field count)
        - Overall hierarchy depth
        """
        metrics = {
            'section_count': len(sections),
            'table_count': len(tables),
            'form_count': len(forms),
            'avg_elements_per_section': 0,
            'max_table_complexity': 0,
            'total_form_fields': 0,
            'hierarchy_depth': 1
        }

        if sections:
            metrics['avg_elements_per_section'] = np.mean([s.element_count for s in sections])

        if tables:
            complexities = [t.rows * t.columns for t in tables]
            metrics['max_table_complexity'] = max(complexities)

        if forms:
            metrics['total_form_fields'] = sum(len(f.fields) for f in forms)

        return metrics

    def determine_reading_order(self, elements: List[LayoutElement]) -> List[int]:
        """
        Determine optimal reading order for elements.

        Based on:
        - Spatial layout (top-to-bottom, left-to-right)
        - Logical grouping (sections, tables)
        - Visual hierarchy (titles first, then content)

        Returns:
            List of element indices in reading order
        """
        # Sort by position (top-to-bottom, left-to-right)
        indexed_elements = list(enumerate(elements))

        # Sort by Y coordinate primarily, X coordinate secondarily
        sorted_elements = sorted(
            indexed_elements,
            key=lambda x: (x[1].bbox[1], x[1].bbox[0])
        )

        # Return indices only
        return [idx for idx, _ in sorted_elements]

    def calculate_visual_flow(self, elements: List[LayoutElement]) -> np.ndarray:
        """
        Calculate visual flow matrix (transition probabilities between elements).

        Used for gaze path simulation.

        Returns:
            Transition probability matrix (N×N)
        """
        n = len(elements)
        if n == 0:
            return np.array([])

        flow_matrix = np.zeros((n, n))

        for i, elem1 in enumerate(elements):
            for j, elem2 in enumerate(elements):
                if i == j:
                    continue

                # Calculate transition probability based on:
                # 1. Distance (closer = higher probability)
                # 2. Direction (natural reading direction = higher)
                # 3. Element type (related types = higher)

                distance = np.sqrt(
                    (elem1.center[0] - elem2.center[0]) ** 2 +
                    (elem1.center[1] - elem2.center[1]) ** 2
                )

                # Direction preference (prefer down and right)
                dx = elem2.center[0] - elem1.center[0]
                dy = elem2.center[1] - elem1.center[1]

                direction_weight = 1.0
                if dx > 0:  # Right
                    direction_weight *= 1.2
                if dy > 0:  # Down
                    direction_weight *= 1.5

                # Type similarity
                type_weight = 1.0
                if elem1.type == elem2.type:
                    type_weight = 1.3

                # Calculate probability (inverse of distance, with weights)
                if distance > 0:
                    prob = (1.0 / distance) * direction_weight * type_weight
                    flow_matrix[i, j] = prob

        # Normalize rows to sum to 1
        row_sums = flow_matrix.sum(axis=1, keepdims=True)
        row_sums[row_sums == 0] = 1  # Avoid division by zero
        flow_matrix = flow_matrix / row_sums

        return flow_matrix
