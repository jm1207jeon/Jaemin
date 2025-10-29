import { FileText, Tag, AlertCircle, CheckCircle2, FileType } from 'lucide-react';
import { Document } from '../../types/document';
import { cn } from '../../lib/utils';

interface DocumentCardProps {
  document: Document;
  onClick?: () => void;
}

const getDocumentTypeColor = (type: string): string => {
  const colors: Record<string, string> = {
    Manual: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-300',
    Procedure: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300',
    Form: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-300',
    Guideline: 'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-300',
    Regulation: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300',
    Law: 'bg-red-200 text-red-900 dark:bg-red-950 dark:text-red-400',
    Standard: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-900 dark:text-indigo-300',
    'Customer Requirement': 'bg-pink-100 text-pink-800 dark:bg-pink-900 dark:text-pink-300',
    'Risk Management': 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-300',
    'Design & Development': 'bg-cyan-100 text-cyan-800 dark:bg-cyan-900 dark:text-cyan-300',
    Others: 'bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-300',
  };
  return colors[type] || colors.Others;
};

const getStatusIcon = (status: string) => {
  switch (status) {
    case 'completed':
      return <CheckCircle2 className="h-4 w-4 text-green-500" />;
    case 'processing':
      return <AlertCircle className="h-4 w-4 text-yellow-500" />;
    case 'error':
      return <AlertCircle className="h-4 w-4 text-red-500" />;
    default:
      return <FileType className="h-4 w-4 text-gray-400" />;
  }
};

export function DocumentCard({ document, onClick }: DocumentCardProps) {
  const { metadata, analysis } = document;

  return (
    <div
      onClick={onClick}
      className={cn(
        "border rounded-lg p-4 hover:bg-accent cursor-pointer transition-all hover:shadow-md",
        "flex flex-col gap-3"
      )}
    >
      {/* Header */}
      <div className="flex items-start gap-3">
        <FileText className="h-5 w-5 text-primary mt-1 flex-shrink-0" />
        <div className="flex-1 min-w-0">
          <h4 className="font-medium truncate" title={metadata.fileName}>
            {metadata.fileName}
          </h4>
          <div className="flex items-center gap-2 mt-1">
            {getStatusIcon(metadata.status)}
            <span className="text-xs text-muted-foreground">
              {metadata.fileType.toUpperCase()} • {(metadata.size / 1024).toFixed(1)} KB
            </span>
          </div>
        </div>
      </div>

      {/* Document Type Badge */}
      <div>
        <span className={cn("text-xs px-2 py-1 rounded-full font-medium", getDocumentTypeColor(metadata.type))}>
          {metadata.type}
        </span>
      </div>

      {/* Analysis Summary */}
      {analysis && (
        <div className="space-y-2">
          {analysis.summary && (
            <p className="text-xs text-muted-foreground line-clamp-2">
              {analysis.summary}
            </p>
          )}

          {/* Keywords */}
          {analysis.keywords.length > 0 && (
            <div className="flex flex-wrap gap-1">
              {analysis.keywords.slice(0, 3).map((keyword, idx) => (
                <span
                  key={idx}
                  className="inline-flex items-center gap-1 text-xs bg-secondary px-2 py-0.5 rounded"
                >
                  <Tag className="h-3 w-3" />
                  {keyword.text}
                </span>
              ))}
              {analysis.keywords.length > 3 && (
                <span className="text-xs text-muted-foreground px-2 py-0.5">
                  +{analysis.keywords.length - 3} more
                </span>
              )}
            </div>
          )}

          {/* Regulations */}
          {analysis.regulations.length > 0 && (
            <div className="flex flex-wrap gap-1">
              {analysis.regulations.slice(0, 2).map((reg, idx) => (
                <span
                  key={idx}
                  className="text-xs bg-primary/10 text-primary px-2 py-0.5 rounded font-medium"
                >
                  {reg.name}
                </span>
              ))}
              {analysis.regulations.length > 2 && (
                <span className="text-xs text-muted-foreground px-2 py-0.5">
                  +{analysis.regulations.length - 2} more
                </span>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
