export enum DocumentType {
  MANUAL = "Manual",
  PROCEDURE = "Procedure",
  FORM = "Form",
  GUIDELINE = "Guideline",
  REGULATION = "Regulation",
  LAW = "Law",
  STANDARD = "Standard",
  CUSTOMER_REQUIREMENT = "Customer Requirement",
  RISK_MANAGEMENT = "Risk Management",
  DESIGN_DEVELOPMENT = "Design & Development",
  OTHERS = "Others"
}

export enum ProcessingStatus {
  PENDING = "pending",
  PROCESSING = "processing",
  COMPLETED = "completed",
  ERROR = "error"
}

export interface Keyword {
  text: string;
  frequency: number;
  importance: number;
}

export interface Topic {
  name: string;
  relevance: number; // 0-1
}

export interface Regulation {
  name: string;
  type: "ISO" | "IEC" | "FDA" | "EU" | "OTHER";
  sections?: string[];
}

export interface DocumentMetadata {
  id: string;
  fileName: string;
  originalName: string;
  fileType: "pdf" | "docx" | "doc" | "txt";
  size: number;
  uploadDate: Date;
  status: ProcessingStatus;
  type: DocumentType;
  tags: string[];
  notes: string;
}

export interface DocumentAnalysis {
  summary: string;
  keywords: Keyword[];
  topics: Topic[];
  regulations: Regulation[];
  references: string[]; // Document IDs that this document references
}

export interface Document {
  metadata: DocumentMetadata;
  content: string;
  analysis?: DocumentAnalysis;
  connections: string[]; // IDs of connected documents
}

export interface DocumentConnection {
  source: string;
  target: string;
  strength: number; // 0-1
  type: "keyword" | "regulation" | "topic" | "explicit";
  commonElements: string[];
}
