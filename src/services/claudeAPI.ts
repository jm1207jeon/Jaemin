import Anthropic from '@anthropic-ai/sdk';
import { DocumentAnalysis, DocumentType, Keyword, Topic, Regulation } from '../types/document';

const CLAUDE_API_KEY = import.meta.env.VITE_CLAUDE_API_KEY;
const CLAUDE_MODEL = import.meta.env.VITE_CLAUDE_MODEL || 'claude-3-5-sonnet-20241022';

// Check if API key is available
const isAPIConfigured = (): boolean => {
  return !!CLAUDE_API_KEY && CLAUDE_API_KEY !== 'your_anthropic_api_key_here';
};

/**
 * Initialize Anthropic client
 */
function getClient(): Anthropic | null {
  if (!isAPIConfigured()) {
    console.warn('Claude API key not configured. Using mock analysis.');
    return null;
  }

  return new Anthropic({
    apiKey: CLAUDE_API_KEY,
    dangerouslyAllowBrowser: true, // Note: In production, use a backend proxy
  });
}

/**
 * Analyze document content using Claude API
 */
export async function analyzeDocument(content: string, fileName: string): Promise<DocumentAnalysis> {
  const client = getClient();

  // If API is not configured, return mock analysis
  if (!client) {
    return generateMockAnalysis(content, fileName);
  }

  try {
    const prompt = `You are an expert in Quality Management Systems (QMS) for medical devices. Analyze the following QMS document and provide a structured analysis.

Document Name: ${fileName}
Document Content:
${content.substring(0, 10000)} ${content.length > 10000 ? '...(truncated)' : ''}

Please provide a JSON response with the following structure:
{
  "summary": "A 2-3 sentence summary of the document's core purpose and content",
  "documentType": "One of: Manual, Procedure, Form, Guideline, Regulation, Law, Standard, Customer Requirement, Risk Management, Design & Development, Others",
  "keywords": [
    {"text": "keyword1", "frequency": 5, "importance": 0.9},
    {"text": "keyword2", "frequency": 3, "importance": 0.7}
  ],
  "topics": [
    {"name": "Quality Management", "relevance": 0.95},
    {"name": "Risk Management", "relevance": 0.85}
  ],
  "regulations": [
    {"name": "ISO 13485", "type": "ISO", "sections": ["4.2", "7.3"]},
    {"name": "21 CFR Part 820", "type": "FDA", "sections": ["820.30"]}
  ],
  "references": ["Document IDs or names mentioned in the text"]
}

Focus on:
1. Extract 15-20 most relevant technical keywords (e.g., sterilization, biocompatibility, validation, CAPA)
2. Identify key topics from: Quality Management, Risk Management, Design & Development, Production, Measurement & Analysis, Regulatory Compliance, Supplier Management, Customer Relations
3. Identify specific regulations, standards, and laws mentioned (ISO 13485, ISO 14971, IEC standards, FDA regulations, EU MDR, etc.)
4. Find explicit references to other documents (e.g., "QP-001", "see procedure", etc.)

Return ONLY valid JSON, no additional text.`;

    const message = await client.messages.create({
      model: CLAUDE_MODEL,
      max_tokens: 4096,
      messages: [
        {
          role: 'user',
          content: prompt,
        },
      ],
    });

    // Extract text from response
    const responseText = message.content[0].type === 'text' ? message.content[0].text : '';

    // Parse JSON response
    const analysis = JSON.parse(responseText);

    // Convert to our DocumentAnalysis type
    return {
      summary: analysis.summary,
      keywords: analysis.keywords || [],
      topics: analysis.topics || [],
      regulations: analysis.regulations || [],
      references: analysis.references || [],
    };
  } catch (error) {
    console.error('Error analyzing document with Claude:', error);
    // Fallback to mock analysis
    return generateMockAnalysis(content, fileName);
  }
}

/**
 * Generate mock analysis for testing when API is not configured
 */
function generateMockAnalysis(content: string, fileName: string): DocumentAnalysis {
  // Simple keyword extraction
  const words = content
    .toLowerCase()
    .replace(/[^\w\s]/g, ' ')
    .split(/\s+/)
    .filter((w) => w.length > 4);

  const wordFreq: Record<string, number> = {};
  words.forEach((word) => {
    wordFreq[word] = (wordFreq[word] || 0) + 1;
  });

  const keywords: Keyword[] = Object.entries(wordFreq)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 15)
    .map(([text, frequency]) => ({
      text,
      frequency,
      importance: frequency / words.length,
    }));

  // Detect document type based on content
  let documentType = DocumentType.OTHERS;
  const lowerContent = content.toLowerCase();

  if (lowerContent.includes('procedure') || lowerContent.includes('process')) {
    documentType = DocumentType.PROCEDURE;
  } else if (lowerContent.includes('manual') || lowerContent.includes('quality management system')) {
    documentType = DocumentType.MANUAL;
  } else if (lowerContent.includes('risk') || lowerContent.includes('hazard')) {
    documentType = DocumentType.RISK_MANAGEMENT;
  } else if (lowerContent.includes('checklist') || lowerContent.includes('audit')) {
    documentType = DocumentType.GUIDELINE;
  }

  // Detect topics
  const topics: Topic[] = [];
  if (lowerContent.includes('quality')) {
    topics.push({ name: 'Quality Management', relevance: 0.9 });
  }
  if (lowerContent.includes('risk')) {
    topics.push({ name: 'Risk Management', relevance: 0.85 });
  }
  if (lowerContent.includes('design') || lowerContent.includes('development')) {
    topics.push({ name: 'Design & Development', relevance: 0.8 });
  }
  if (lowerContent.includes('regulatory') || lowerContent.includes('compliance')) {
    topics.push({ name: 'Regulatory Compliance', relevance: 0.85 });
  }

  // Detect regulations
  const regulations: Regulation[] = [];
  if (content.includes('ISO 13485')) {
    regulations.push({ name: 'ISO 13485', type: 'ISO', sections: [] });
  }
  if (content.includes('ISO 14971')) {
    regulations.push({ name: 'ISO 14971', type: 'ISO', sections: [] });
  }
  if (content.includes('21 CFR') || content.includes('CFR Part 820')) {
    regulations.push({ name: '21 CFR Part 820', type: 'FDA', sections: [] });
  }
  if (content.includes('IEC 62304')) {
    regulations.push({ name: 'IEC 62304', type: 'IEC', sections: [] });
  }
  if (content.includes('IEC 60601')) {
    regulations.push({ name: 'IEC 60601', type: 'IEC', sections: [] });
  }
  if (content.includes('MDR') || content.includes('EU 2017/745')) {
    regulations.push({ name: 'EU MDR 2017/745', type: 'EU', sections: [] });
  }

  // Extract references (simple pattern matching)
  const refPattern = /\b(QP-\d+|QMS-[A-Z]+-\d+|FM-\d+-\d+|CHK-\d+)\b/g;
  const references = [...new Set(content.match(refPattern) || [])];

  return {
    summary: `This document (${fileName}) contains information about ${topics.map((t) => t.name.toLowerCase()).join(', ')} in the context of medical device QMS.`,
    keywords,
    topics,
    regulations,
    references,
  };
}

/**
 * Analyze relationships between documents
 */
export async function analyzeRelationships(
  documents: Array<{ id: string; content: string; analysis?: DocumentAnalysis }>
): Promise<Array<{ source: string; target: string; strength: number; commonElements: string[] }>> {
  const relationships: Array<{
    source: string;
    target: string;
    strength: number;
    commonElements: string[];
  }> = [];

  for (let i = 0; i < documents.length; i++) {
    for (let j = i + 1; j < documents.length; j++) {
      const doc1 = documents[i];
      const doc2 = documents[j];

      if (!doc1.analysis || !doc2.analysis) continue;

      const commonElements: string[] = [];
      let score = 0;

      // Check keyword overlap (Jaccard similarity)
      const keywords1 = new Set(doc1.analysis.keywords.map((k) => k.text));
      const keywords2 = new Set(doc2.analysis.keywords.map((k) => k.text));
      const intersection = new Set([...keywords1].filter((x) => keywords2.has(x)));
      const union = new Set([...keywords1, ...keywords2]);

      if (union.size > 0) {
        const keywordSimilarity = intersection.size / union.size;
        score += keywordSimilarity * 0.4;

        intersection.forEach((keyword) => commonElements.push(`keyword:${keyword}`));
      }

      // Check regulation overlap (weighted higher)
      const regs1 = new Set(doc1.analysis.regulations.map((r) => r.name));
      const regs2 = new Set(doc2.analysis.regulations.map((r) => r.name));
      const regIntersection = new Set([...regs1].filter((x) => regs2.has(x)));

      if (regs1.size > 0 && regs2.size > 0) {
        const regSimilarity = regIntersection.size / Math.max(regs1.size, regs2.size);
        score += regSimilarity * 0.3;

        regIntersection.forEach((reg) => commonElements.push(`regulation:${reg}`));
      }

      // Check topic overlap
      const topics1 = new Set(doc1.analysis.topics.map((t) => t.name));
      const topics2 = new Set(doc2.analysis.topics.map((t) => t.name));
      const topicIntersection = new Set([...topics1].filter((x) => topics2.has(x)));

      if (topics1.size > 0 && topics2.size > 0) {
        const topicSimilarity = topicIntersection.size / Math.max(topics1.size, topics2.size);
        score += topicSimilarity * 0.2;

        topicIntersection.forEach((topic) => commonElements.push(`topic:${topic}`));
      }

      // Check explicit references
      if (doc1.analysis.references.includes(doc2.id) || doc2.analysis.references.includes(doc1.id)) {
        score += 0.1;
        commonElements.push('explicit-reference');
      }

      // Only add relationship if there's meaningful connection
      if (score > 0.2 && commonElements.length > 0) {
        relationships.push({
          source: doc1.id,
          target: doc2.id,
          strength: Math.min(score, 1),
          commonElements,
        });
      }
    }
  }

  return relationships;
}

export { isAPIConfigured };
