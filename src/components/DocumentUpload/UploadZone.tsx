import { useCallback, useState } from 'react';
import { useDropzone } from 'react-dropzone';
import { Upload, FileText, AlertCircle, CheckCircle, Loader2 } from 'lucide-react';
import { Card } from '../UI/Card';
import { cn } from '../../lib/utils';
import { useDocumentUpload } from '../../hooks/useDocumentUpload';

const MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
const ACCEPTED_FILE_TYPES = {
  'application/pdf': ['.pdf'],
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': ['.docx'],
  'application/msword': ['.doc'],
  'text/plain': ['.txt'],
};

export function UploadZone() {
  const [error, setError] = useState<string>('');
  const { uploadFiles, uploadProgress } = useDocumentUpload();

  const onDrop = useCallback((acceptedFiles: File[], rejectedFiles: any[]) => {
    setError('');

    if (rejectedFiles.length > 0) {
      const errorMessages = rejectedFiles.map(file => {
        if (file.file.size > MAX_FILE_SIZE) {
          return `${file.file.name}: File too large (max 50MB)`;
        }
        return `${file.file.name}: Invalid file type`;
      });
      setError(errorMessages.join(', '));
      return;
    }

    if (acceptedFiles.length > 0) {
      uploadFiles(acceptedFiles);
    }
  }, [uploadFiles]);

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    accept: ACCEPTED_FILE_TYPES,
    maxSize: MAX_FILE_SIZE,
    multiple: true,
  });

  return (
    <Card className={cn(
      "p-8 border-2 border-dashed transition-colors cursor-pointer",
      isDragActive && "border-primary bg-primary/5"
    )}>
      <div {...getRootProps()}>
        <input {...getInputProps()} />
        <div className="flex flex-col items-center justify-center gap-4 text-center">
          <div className={cn(
            "p-4 rounded-full transition-colors",
            isDragActive ? "bg-primary/10" : "bg-muted"
          )}>
            {isDragActive ? (
              <FileText className="h-10 w-10 text-primary" />
            ) : (
              <Upload className="h-10 w-10 text-muted-foreground" />
            )}
          </div>

          <div>
            <h3 className="text-lg font-semibold mb-2">
              {isDragActive ? 'Drop files here' : 'Upload Documents'}
            </h3>
            <p className="text-sm text-muted-foreground">
              Drag & drop files here, or click to select files
            </p>
            <p className="text-xs text-muted-foreground mt-2">
              Supported formats: PDF, DOCX, DOC, TXT (Max 50MB)
            </p>
          </div>

          {error && (
            <div className="flex items-center gap-2 text-destructive text-sm">
              <AlertCircle className="h-4 w-4" />
              <span>{error}</span>
            </div>
          )}

          {uploadProgress.length > 0 && (
            <div className="w-full max-w-md space-y-2">
              {uploadProgress.map((progress, idx) => (
                <div key={idx} className="flex items-center gap-2 text-sm">
                  {progress.status === 'processing' && (
                    <Loader2 className="h-4 w-4 animate-spin text-primary" />
                  )}
                  {progress.status === 'completed' && (
                    <CheckCircle className="h-4 w-4 text-green-500" />
                  )}
                  {progress.status === 'error' && (
                    <AlertCircle className="h-4 w-4 text-destructive" />
                  )}
                  <span className="flex-1 truncate">{progress.fileName}</span>
                  {progress.status === 'error' && progress.error && (
                    <span className="text-xs text-destructive">{progress.error}</span>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </Card>
  );
}
