import { useDocumentStore } from '../../store/documentStore';
import { Card, CardContent, CardHeader, CardTitle } from '../UI/Card';
import { FileText, Grid3x3, List } from 'lucide-react';
import { DocumentCard } from './DocumentCard';
import { Button } from '../UI/Button';
import { useUIStore } from '../../store/uiStore';

export function DocumentLibrary() {
  const documents = useDocumentStore((state) => state.documents);
  const setSelectedDocument = useDocumentStore((state) => state.setSelectedDocument);
  const { viewMode, setViewMode } = useUIStore();

  if (documents.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Document Library</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col items-center justify-center py-12 text-center">
            <FileText className="h-12 w-12 text-muted-foreground mb-4" />
            <h3 className="text-lg font-semibold mb-2">No documents yet</h3>
            <p className="text-sm text-muted-foreground">
              Upload your first QMS document to get started
            </p>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle>Document Library ({documents.length})</CardTitle>
          <div className="flex gap-1">
            <Button
              variant={viewMode === 'grid' ? 'default' : 'outline'}
              size="sm"
              onClick={() => setViewMode('grid')}
            >
              <Grid3x3 className="h-4 w-4" />
            </Button>
            <Button
              variant={viewMode === 'list' ? 'default' : 'outline'}
              size="sm"
              onClick={() => setViewMode('list')}
            >
              <List className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div className={viewMode === 'grid' ? 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4' : 'space-y-2'}>
          {documents.map((doc) => (
            <DocumentCard
              key={doc.metadata.id}
              document={doc}
              onClick={() => setSelectedDocument(doc.metadata.id)}
            />
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
