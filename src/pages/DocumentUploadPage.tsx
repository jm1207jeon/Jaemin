import { UploadZone } from '../components/DocumentUpload/UploadZone';
import { DocumentLibrary } from '../components/DocumentLibrary/DocumentLibrary';

export function DocumentUploadPage() {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-bold tracking-tight">Document Management</h2>
        <p className="text-muted-foreground mt-2">
          Upload and manage your QMS documents
        </p>
      </div>

      <UploadZone />
      <DocumentLibrary />
    </div>
  );
}
