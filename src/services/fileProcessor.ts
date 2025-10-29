import * as pdfjsLib from 'pdfjs-dist';
import mammoth from 'mammoth';

// Set up PDF.js worker
pdfjsLib.GlobalWorkerOptions.workerSrc = `//cdnjs.cloudflare.com/ajax/libs/pdf.js/${pdfjsLib.version}/pdf.worker.min.js`;

export interface FileProcessingResult {
  content: string;
  pageCount?: number;
  error?: string;
}

/**
 * Extract text from a PDF file
 */
export async function extractPdfText(file: File): Promise<FileProcessingResult> {
  try {
    const arrayBuffer = await file.arrayBuffer();
    const pdf = await pdfjsLib.getDocument({ data: arrayBuffer }).promise;
    const pageCount = pdf.numPages;

    let fullText = '';

    for (let i = 1; i <= pageCount; i++) {
      const page = await pdf.getPage(i);
      const textContent = await page.getTextContent();
      const pageText = textContent.items
        .map((item: any) => item.str)
        .join(' ');
      fullText += pageText + '\n\n';
    }

    return {
      content: fullText.trim(),
      pageCount,
    };
  } catch (error) {
    console.error('Error extracting PDF text:', error);
    return {
      content: '',
      error: 'Failed to extract text from PDF',
    };
  }
}

/**
 * Extract text from a DOCX file
 */
export async function extractDocxText(file: File): Promise<FileProcessingResult> {
  try {
    const arrayBuffer = await file.arrayBuffer();
    const result = await mammoth.extractRawText({ arrayBuffer });

    return {
      content: result.value.trim(),
    };
  } catch (error) {
    console.error('Error extracting DOCX text:', error);
    return {
      content: '',
      error: 'Failed to extract text from DOCX',
    };
  }
}

/**
 * Extract text from a plain text file
 */
export async function extractTextFile(file: File): Promise<FileProcessingResult> {
  try {
    const text = await file.text();

    return {
      content: text.trim(),
    };
  } catch (error) {
    console.error('Error reading text file:', error);
    return {
      content: '',
      error: 'Failed to read text file',
    };
  }
}

/**
 * Process a file and extract its text content based on file type
 */
export async function processFile(file: File): Promise<FileProcessingResult> {
  const fileType = file.type;
  const fileName = file.name.toLowerCase();

  if (fileType === 'application/pdf' || fileName.endsWith('.pdf')) {
    return extractPdfText(file);
  } else if (
    fileType === 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' ||
    fileName.endsWith('.docx')
  ) {
    return extractDocxText(file);
  } else if (fileType === 'application/msword' || fileName.endsWith('.doc')) {
    // For .doc files, we'll try to use mammoth (it has limited support)
    return extractDocxText(file);
  } else if (fileType === 'text/plain' || fileName.endsWith('.txt')) {
    return extractTextFile(file);
  }

  return {
    content: '',
    error: 'Unsupported file type',
  };
}

/**
 * Preprocess text for analysis
 */
export function preprocessText(text: string): string {
  // Remove extra whitespace
  let processed = text.replace(/\s+/g, ' ');

  // Remove special characters but keep important punctuation
  processed = processed.replace(/[^\w\s.,;:()\-]/g, '');

  // Normalize line breaks
  processed = processed.replace(/\n+/g, '\n');

  return processed.trim();
}

/**
 * Get file type from file name or MIME type
 */
export function getFileType(file: File): 'pdf' | 'docx' | 'doc' | 'txt' | 'unknown' {
  const fileName = file.name.toLowerCase();

  if (fileName.endsWith('.pdf')) return 'pdf';
  if (fileName.endsWith('.docx')) return 'docx';
  if (fileName.endsWith('.doc')) return 'doc';
  if (fileName.endsWith('.txt')) return 'txt';

  return 'unknown';
}
