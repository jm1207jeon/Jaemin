import { useState } from 'react';
import { useDocumentStore } from '../store/documentStore';
import { processFile, getFileType } from '../services/fileProcessor';
import { Document, DocumentType, ProcessingStatus } from '../types/document';
import { analyzeDocument } from '../services/claudeAPI';

interface UploadProgress {
  fileName: string;
  progress: number;
  status: 'processing' | 'completed' | 'error';
  error?: string;
}

export function useDocumentUpload() {
  const [uploadProgress, setUploadProgress] = useState<UploadProgress[]>([]);
  const { addDocument } = useDocumentStore();

  const uploadFiles = async (files: File[]) => {
    const progressMap = files.map((file) => ({
      fileName: file.name,
      progress: 0,
      status: 'processing' as const,
    }));

    setUploadProgress(progressMap);

    for (let i = 0; i < files.length; i++) {
      const file = files[i];

      try {
        // Update progress
        setUploadProgress((prev) =>
          prev.map((p, idx) =>
            idx === i ? { ...p, progress: 25 } : p
          )
        );

        // Process file
        const result = await processFile(file);

        if (result.error) {
          throw new Error(result.error);
        }

        setUploadProgress((prev) =>
          prev.map((p, idx) =>
            idx === i ? { ...p, progress: 50 } : p
          )
        );

        // Analyze document with AI
        const analysis = await analyzeDocument(result.content, file.name);

        setUploadProgress((prev) =>
          prev.map((p, idx) =>
            idx === i ? { ...p, progress: 75 } : p
          )
        );

        // Determine document type from analysis if available
        let documentType = DocumentType.OTHERS;
        if (analysis.keywords.some((k) => k.text.toLowerCase().includes('manual'))) {
          documentType = DocumentType.MANUAL;
        } else if (analysis.keywords.some((k) => k.text.toLowerCase().includes('procedure'))) {
          documentType = DocumentType.PROCEDURE;
        } else if (analysis.topics.some((t) => t.name.includes('Risk Management'))) {
          documentType = DocumentType.RISK_MANAGEMENT;
        }

        // Create document object
        const document: Document = {
          metadata: {
            id: `doc-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
            fileName: file.name,
            originalName: file.name,
            fileType: getFileType(file),
            size: file.size,
            uploadDate: new Date(),
            status: ProcessingStatus.COMPLETED,
            type: documentType,
            tags: [],
            notes: '',
          },
          content: result.content,
          analysis,
          connections: [],
        };

        // Add to store
        addDocument(document);

        // Complete
        setUploadProgress((prev) =>
          prev.map((p, idx) =>
            idx === i
              ? { ...p, progress: 100, status: 'completed' }
              : p
          )
        );
      } catch (error) {
        console.error(`Error processing file ${file.name}:`, error);
        setUploadProgress((prev) =>
          prev.map((p, idx) =>
            idx === i
              ? {
                  ...p,
                  status: 'error',
                  error: error instanceof Error ? error.message : 'Unknown error',
                }
              : p
          )
        );
      }
    }

    // Clear progress after 3 seconds
    setTimeout(() => {
      setUploadProgress([]);
    }, 3000);
  };

  return {
    uploadFiles,
    uploadProgress,
  };
}
