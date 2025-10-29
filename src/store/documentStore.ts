import { create } from 'zustand';
import { Document, DocumentConnection } from '../types/document';

interface DocumentStore {
  documents: Document[];
  connections: DocumentConnection[];
  selectedDocumentId: string | null;

  // Actions
  addDocument: (document: Document) => void;
  updateDocument: (id: string, updates: Partial<Document>) => void;
  removeDocument: (id: string) => void;
  setSelectedDocument: (id: string | null) => void;
  addConnection: (connection: DocumentConnection) => void;
  updateConnections: (connections: DocumentConnection[]) => void;
  clearAll: () => void;
}

export const useDocumentStore = create<DocumentStore>((set) => ({
  documents: [],
  connections: [],
  selectedDocumentId: null,

  addDocument: (document) =>
    set((state) => ({
      documents: [...state.documents, document],
    })),

  updateDocument: (id, updates) =>
    set((state) => ({
      documents: state.documents.map((doc) =>
        doc.metadata.id === id ? { ...doc, ...updates } : doc
      ),
    })),

  removeDocument: (id) =>
    set((state) => ({
      documents: state.documents.filter((doc) => doc.metadata.id !== id),
      connections: state.connections.filter(
        (conn) => conn.source !== id && conn.target !== id
      ),
      selectedDocumentId: state.selectedDocumentId === id ? null : state.selectedDocumentId,
    })),

  setSelectedDocument: (id) =>
    set({ selectedDocumentId: id }),

  addConnection: (connection) =>
    set((state) => ({
      connections: [...state.connections, connection],
    })),

  updateConnections: (connections) =>
    set({ connections }),

  clearAll: () =>
    set({
      documents: [],
      connections: [],
      selectedDocumentId: null,
    }),
}));
