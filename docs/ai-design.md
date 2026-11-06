# AI design

The FastAPI service runs separately from the .NET application. It has no PostgreSQL credentials and does not write to application tables. Resume files are retrieved by the authenticated .NET API from private storage and sent to the parser over the internal Compose network.

## Resume parser

`POST /v1/resume/parse?filename=...` accepts raw PDF or DOCX bytes. The service checks the extension and file signature, limits input to 10 MB, extracts text with PyMuPDF or python-docx, and returns validated Pydantic JSON. The current deterministic parser extracts likely name, email, phone, explicitly stated experience, a conservative technical skill vocabulary, and clearly formatted education/company entries. It reports warnings when information is absent or uncertain instead of inventing candidate details.

Scanned PDFs with no selectable text return `needs_ocr` and an `ocr_required` warning. Empty documents return `empty`; invalid and corrupted documents return a 422 response. OCR is an optional future extension. The authenticated .NET API queues parsing in Hangfire using PostgreSQL-backed job storage; a worker retrieves the private resume, calls the parser, and persists results to `resume_parsing_results`. Candidate-authorized endpoints expose job and parsing status.

## Candidate matching

The initial matcher is implemented as `deterministic-1.0`. It scores required skills (50%), experience (25%), project description evidence (15%), and education (10%). Each run persists the score, matched and missing required skills, category explanations, data warnings, and algorithm version. Weights are configurable and invalid configurations fall back to defaults. Scores assist recruiters and never automatically reject candidates. Semantic sentence-transformer matching remains a later extension.

