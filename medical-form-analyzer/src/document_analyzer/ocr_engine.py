"""
OCR Engine
~~~~~~~~~~

Extracts text from document images using OCR.
Supports multiple OCR engines (Tesseract, EasyOCR).
"""

import cv2
import numpy as np
from typing import List, Dict, Tuple, Optional
from dataclasses import dataclass
import pytesseract
from PIL import Image


@dataclass
class TextRegion:
    """Represents extracted text with location information."""

    text: str
    bbox: Tuple[int, int, int, int]  # (x1, y1, x2, y2)
    confidence: float
    language: str = "eng"

    @property
    def is_empty(self) -> bool:
        """Check if text is empty or whitespace."""
        return len(self.text.strip()) == 0


class OCREngine:
    """
    OCR Engine for text extraction from documents.

    Supports multiple OCR backends:
    - Tesseract (default, open-source)
    - EasyOCR (deep learning-based)
    - DocTR (document-specific)
    """

    def __init__(self, config: Dict):
        """
        Initialize OCR engine.

        Args:
            config: Configuration dictionary
        """
        self.config = config
        self.engine = config.get('ocr_engine', 'tesseract')
        self.languages = config.get('ocr_languages', ['eng'])

        # Initialize engine
        if self.engine == 'tesseract':
            self._init_tesseract()
        elif self.engine == 'easyocr':
            self._init_easyocr()
        else:
            raise ValueError(f"Unsupported OCR engine: {self.engine}")

    def _init_tesseract(self):
        """Initialize Tesseract OCR."""
        # Test if tesseract is installed
        try:
            pytesseract.get_tesseract_version()
        except Exception as e:
            print(f"Warning: Tesseract not found. {e}")
            print("Please install tesseract: https://github.com/tesseract-ocr/tesseract")

    def _init_easyocr(self):
        """Initialize EasyOCR."""
        try:
            import easyocr
            self.easyocr_reader = easyocr.Reader(self.languages)
        except ImportError:
            print("Warning: EasyOCR not installed. Falling back to Tesseract.")
            self.engine = 'tesseract'
            self._init_tesseract()

    def extract_text(self, image: np.ndarray, bbox: Optional[Tuple] = None) -> List[TextRegion]:
        """
        Extract text from image or specific region.

        Args:
            image: Input image
            bbox: Optional bounding box (x1, y1, x2, y2) to extract text from

        Returns:
            List of text regions with bounding boxes
        """
        # Crop if bbox provided
        if bbox is not None:
            x1, y1, x2, y2 = bbox
            image = image[y1:y2, x1:x2]

        # Preprocess image
        processed = self._preprocess_image(image)

        # Extract text based on engine
        if self.engine == 'tesseract':
            text_regions = self._extract_with_tesseract(processed)
        elif self.engine == 'easyocr':
            text_regions = self._extract_with_easyocr(processed)
        else:
            text_regions = []

        # Adjust coordinates if bbox was provided
        if bbox is not None:
            x_offset, y_offset = bbox[0], bbox[1]
            for region in text_regions:
                x1, y1, x2, y2 = region.bbox
                region.bbox = (x1 + x_offset, y1 + y_offset,
                              x2 + x_offset, y2 + y_offset)

        return text_regions

    def _preprocess_image(self, image: np.ndarray) -> np.ndarray:
        """
        Preprocess image for better OCR results.

        Techniques:
        - Grayscale conversion
        - Noise reduction
        - Binarization (Otsu's method)
        - Deskewing
        """
        # Convert to grayscale
        if len(image.shape) == 3:
            gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        else:
            gray = image.copy()

        # Denoise
        denoised = cv2.fastNlMeansDenoising(gray)

        # Binarization
        _, binary = cv2.threshold(denoised, 0, 255,
                                  cv2.THRESH_BINARY + cv2.THRESH_OTSU)

        # Deskew if needed
        # deskewed = self._deskew(binary)

        return binary

    def _extract_with_tesseract(self, image: np.ndarray) -> List[TextRegion]:
        """Extract text using Tesseract OCR."""
        # Get detailed data including bounding boxes
        lang = '+'.join(self.languages)

        try:
            data = pytesseract.image_to_data(image, lang=lang, output_type=pytesseract.Output.DICT)
        except Exception as e:
            print(f"OCR Error: {e}")
            return []

        text_regions = []
        n_boxes = len(data['text'])

        for i in range(n_boxes):
            # Filter out low confidence and empty text
            conf = float(data['conf'][i])
            text = data['text'][i]

            if conf > 0 and text.strip():
                x, y, w, h = (data['left'][i], data['top'][i],
                             data['width'][i], data['height'][i])

                region = TextRegion(
                    text=text,
                    bbox=(x, y, x + w, y + h),
                    confidence=conf / 100.0,  # Normalize to 0-1
                    language=self.languages[0]
                )
                text_regions.append(region)

        return text_regions

    def _extract_with_easyocr(self, image: np.ndarray) -> List[TextRegion]:
        """Extract text using EasyOCR."""
        try:
            results = self.easyocr_reader.readtext(image)

            text_regions = []
            for bbox_points, text, confidence in results:
                # Convert polygon to rectangle
                x_coords = [p[0] for p in bbox_points]
                y_coords = [p[1] for p in bbox_points]

                x1, y1 = int(min(x_coords)), int(min(y_coords))
                x2, y2 = int(max(x_coords)), int(max(y_coords))

                region = TextRegion(
                    text=text,
                    bbox=(x1, y1, x2, y2),
                    confidence=confidence,
                    language=self.languages[0]
                )
                text_regions.append(region)

            return text_regions

        except Exception as e:
            print(f"EasyOCR Error: {e}")
            return []

    def extract_full_text(self, image: np.ndarray) -> str:
        """
        Extract full text content from image (without bounding boxes).

        Args:
            image: Input image

        Returns:
            Extracted text as string
        """
        processed = self._preprocess_image(image)

        if self.engine == 'tesseract':
            lang = '+'.join(self.languages)
            try:
                text = pytesseract.image_to_string(processed, lang=lang)
                return text
            except Exception as e:
                print(f"OCR Error: {e}")
                return ""
        else:
            regions = self.extract_text(image)
            return '\n'.join([r.text for r in regions])

    def detect_text_fields(self, image: np.ndarray) -> List[Tuple[int, int, int, int]]:
        """
        Detect text input fields (blank areas meant for writing).

        Uses morphological operations to detect blank rectangular regions.

        Returns:
            List of bounding boxes for detected text fields
        """
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if len(image.shape) == 3 else image

        # Detect edges
        edges = cv2.Canny(gray, 50, 150)

        # Detect rectangles
        contours, _ = cv2.findContours(edges, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)

        text_fields = []
        for contour in contours:
            peri = cv2.arcLength(contour, True)
            approx = cv2.approxPolyDP(contour, 0.02 * peri, True)

            if len(approx) == 4:  # Rectangle
                x, y, w, h = cv2.boundingRect(contour)

                # Check if it's field-sized (not too small, not too large)
                if 50 < w < 500 and 20 < h < 100:
                    # Check if it's relatively empty (blank field)
                    roi = gray[y:y+h, x:x+w]
                    if self._is_blank_region(roi):
                        text_fields.append((x, y, x + w, y + h))

        return text_fields

    @staticmethod
    def _is_blank_region(roi: np.ndarray, threshold: float = 0.9) -> bool:
        """
        Check if a region is mostly blank (empty text field).

        Args:
            roi: Region of interest
            threshold: Ratio of white pixels to consider blank

        Returns:
            True if region is blank
        """
        if roi.size == 0:
            return False

        # Binarize
        _, binary = cv2.threshold(roi, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

        # Calculate ratio of white pixels
        white_ratio = np.sum(binary == 255) / binary.size

        return white_ratio > threshold

    def analyze_text_density(self, text_regions: List[TextRegion],
                            image_shape: Tuple[int, int]) -> float:
        """
        Calculate text density (coverage ratio).

        High text density can increase cognitive load.

        Args:
            text_regions: List of text regions
            image_shape: (height, width) of document

        Returns:
            Text density (0-1)
        """
        if not text_regions:
            return 0.0

        total_area = image_shape[0] * image_shape[1]
        text_area = sum([
            (r.bbox[2] - r.bbox[0]) * (r.bbox[3] - r.bbox[1])
            for r in text_regions
        ])

        return min(text_area / total_area, 1.0)

    def get_font_sizes(self, text_regions: List[TextRegion]) -> List[int]:
        """
        Estimate font sizes from text region heights.

        Args:
            text_regions: List of text regions

        Returns:
            List of estimated font sizes in pixels
        """
        font_sizes = []
        for region in text_regions:
            height = region.bbox[3] - region.bbox[1]
            # Rough estimate: font size ≈ height * 0.7
            font_size = int(height * 0.7)
            font_sizes.append(font_size)

        return font_sizes

    def check_readability(self, text_regions: List[TextRegion],
                         min_font_size: int = 10) -> Dict:
        """
        Check readability based on text characteristics.

        Args:
            text_regions: List of text regions
            min_font_size: Minimum acceptable font size

        Returns:
            Dictionary with readability metrics
        """
        if not text_regions:
            return {
                'readable': True,
                'issues': [],
                'average_font_size': 0
            }

        font_sizes = self.get_font_sizes(text_regions)
        avg_font_size = np.mean(font_sizes) if font_sizes else 0

        issues = []
        if avg_font_size < min_font_size:
            issues.append(f"Average font size ({avg_font_size:.1f}px) is below minimum ({min_font_size}px)")

        # Check for very small text
        small_text_count = sum(1 for size in font_sizes if size < min_font_size)
        if small_text_count > 0:
            issues.append(f"{small_text_count} text regions have font size below minimum")

        return {
            'readable': len(issues) == 0,
            'issues': issues,
            'average_font_size': avg_font_size,
            'min_font_size': min(font_sizes) if font_sizes else 0,
            'max_font_size': max(font_sizes) if font_sizes else 0
        }
