# QMS Document Analyzer

A professional web application for analyzing Quality Management System (QMS) documents for medical devices. This tool uses AI to automatically extract keywords, identify regulatory requirements, and visualize document relationships through an interactive network graph.

## Features

### Phase 1 - Completed ✅
- **Modern Tech Stack**: React 18+ with TypeScript, Vite, Tailwind CSS
- **Professional UI**: Clean, modern interface with dark mode support
- **Document Upload**: Drag & drop interface supporting PDF, DOCX, DOC, and TXT files
- **File Processing**: Automatic text extraction from various document formats
- **Document Library**: Grid and list views with search and filtering

### Phase 2 - Completed ✅
- **AI-Powered Analysis**: Integration with Claude API for intelligent document analysis
- **Keyword Extraction**: Automatically identifies 15-20 relevant technical keywords
- **Topic Classification**: Categorizes documents by QMS topics
- **Regulation Detection**: Identifies standards (ISO 13485, ISO 14971, IEC, FDA regulations, EU MDR)
- **Document Type Classification**: Auto-categorizes as Manual, Procedure, Form, etc.
- **Smart Summaries**: 2-3 sentence AI-generated summaries

### Phase 3 - Coming Soon
- Interactive network graph visualization with D3.js
- Document relationship analysis
- Connection strength indicators
- Graph layouts: Force-directed, Hierarchical, Circular, Radial

## Getting Started

### Prerequisites
- Node.js 18+ and npm
- Anthropic API key (optional, works with mock data without it)

### Installation

1. Clone the repository
```bash
git clone <repository-url>
cd Jaemin
```

2. Install dependencies
```bash
npm install
```

3. Configure environment variables (optional)
```bash
cp .env.example .env
```

Edit `.env` and add your Claude API key:
```
VITE_CLAUDE_API_KEY=your_anthropic_api_key_here
```

4. Start the development server
```bash
npm run dev
```

5. Open your browser and navigate to `http://localhost:5173`

## Usage

### Uploading Documents

1. Click on the upload zone or drag and drop your QMS documents
2. Supported formats: PDF, DOCX, DOC, TXT (max 50MB)
3. Documents are automatically processed and analyzed
4. View analysis results in the document library

### Testing with Sample Documents

Sample QMS documents are provided in the `sample-documents/` directory:
- `QMS-Manual.txt` - Quality Management System Manual
- `Risk-Management-Procedure.txt` - Risk Management Procedure (ISO 14971)
- `ISO-13485-Checklist.txt` - ISO 13485 Compliance Checklist

### Viewing Document Analysis

Each document card shows:
- Document type and status
- AI-generated summary
- Top 3 keywords
- Related regulations and standards
- File metadata (size, type, upload date)

### View Modes

Toggle between Grid and List views using the buttons in the document library header.

## Project Structure

```
src/
├── components/
│   ├── DocumentUpload/   # File upload components
│   ├── DocumentLibrary/  # Document listing and cards
│   ├── Layout/          # Header, Sidebar, MainContent
│   └── UI/              # Reusable UI components (Button, Card, Input)
├── services/
│   ├── fileProcessor.ts  # PDF, DOCX, TXT text extraction
│   └── claudeAPI.ts     # AI analysis and relationship detection
├── store/
│   ├── documentStore.ts # Document state management
│   └── uiStore.ts       # UI state (theme, sidebar, view mode)
├── types/
│   ├── document.ts      # Document type definitions
│   └── graph.ts         # Graph type definitions
├── hooks/
│   └── useDocumentUpload.ts  # Document upload hook
├── lib/
│   └── utils.ts         # Utility functions
└── App.tsx              # Main application component
```

## Technology Stack

- **Frontend**: React 18+, TypeScript
- **Build Tool**: Vite
- **Styling**: Tailwind CSS
- **UI Components**: Custom components based on Shadcn/ui design
- **State Management**: Zustand
- **AI Integration**: Anthropic Claude API
- **File Processing**:
  - PDF: pdfjs-dist
  - DOCX: mammoth.js
- **Icons**: lucide-react
- **Graph Visualization**: D3.js (coming in Phase 3)

## Development Phases

### ✅ Phase 1: Basic Infrastructure
- Project setup with Vite, React, TypeScript
- Tailwind CSS and UI components
- Basic layout and navigation
- File upload with drag & drop
- File parsing (PDF, DOCX, TXT)

### ✅ Phase 2: AI Analysis Integration
- Claude API integration
- Document analysis (keywords, topics, regulations)
- Smart document type classification
- Enhanced document library UI

### 🚧 Phase 3: Network Graph Visualization (Next)
- D3.js integration
- Interactive graph with nodes and edges
- Relationship strength visualization
- Multiple layout algorithms
- Node interactions (drag, zoom, pan)

### 📅 Phase 4: Advanced Features (Planned)
- Search and filtering
- Statistics dashboard
- Document detail modals
- Export functionality
- Settings and preferences

## API Configuration

The application can work in two modes:

### 1. With Claude API (Recommended)
Set your API key in `.env`:
```
VITE_CLAUDE_API_KEY=sk-ant-...
```

Features:
- Advanced AI analysis
- Accurate keyword extraction
- Better document classification
- Relationship detection

### 2. Without API (Mock Mode)
If no API key is configured, the app uses mock analysis:
- Basic keyword frequency analysis
- Pattern-based regulation detection
- Simple document type classification
- Limited relationship analysis

## Sample Data

Three sample QMS documents are provided:

1. **QMS Manual** - Comprehensive quality management system documentation
2. **Risk Management Procedure** - ISO 14971 risk management process
3. **ISO 13485 Checklist** - Compliance audit checklist

These demonstrate the analyzer's ability to:
- Extract QMS-specific terminology
- Identify regulatory references
- Detect document relationships
- Classify document types

## Contributing

This is a specialized medical device QMS tool. Contributions should focus on:
- Regulatory compliance accuracy
- Medical device industry standards
- QMS best practices
- User experience improvements

## Roadmap

- [x] Phase 1: Basic infrastructure and file handling
- [x] Phase 2: AI analysis integration
- [ ] Phase 3: Network graph visualization
- [ ] Phase 4: Advanced search and filtering
- [ ] Phase 5: Statistics and analytics
- [ ] Phase 6: Export and reporting
- [ ] Phase 7: Team collaboration features
- [ ] Phase 8: Optimization and deployment

## License

This project is proprietary software developed for medical device QMS documentation analysis.

## Support

For issues, questions, or feature requests, please open an issue in the repository.

---

**Note**: This application is designed for medical device quality management professionals and requires understanding of ISO 13485, ISO 14971, and related regulatory standards.
