from __future__ import annotations

import io
import re
from dataclasses import dataclass
from pathlib import PurePath

import fitz
from docx import Document
from fastapi import FastAPI, HTTPException, Request
from pydantic import BaseModel, Field

app = FastAPI(title="HireFlow AI Service", version="0.2.0")
PARSER_VERSION = "heuristic-1.1"
MAX_BYTES = 10 * 1024 * 1024
MAX_TEXT_CHARS = 100_000
MAX_PDF_PAGES = 300


class EducationEntry(BaseModel):
    institution: str | None = None
    degree: str | None = None
    field_of_study: str | None = None


class ExperienceEntry(BaseModel):
    company: str | None = None
    job_title: str | None = None
    date_range: str | None = None


class ProjectEntry(BaseModel):
    name: str | None = None
    date_range: str | None = None
    description: str | None = None


class ParsedResume(BaseModel):
    parser_version: str = PARSER_VERSION
    status: str
    name: str | None = None
    email: str | None = None
    phone: str | None = None
    total_experience: float | None = None
    skills: list[str] = Field(default_factory=list)
    education: list[EducationEntry] = Field(default_factory=list)
    experience: list[ExperienceEntry] = Field(default_factory=list)
    projects: list[ProjectEntry] = Field(default_factory=list)
    previous_companies: list[str] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)


@dataclass(frozen=True)
class ExtractedDocument:
    text: str
    warnings: tuple[str, ...] = ()


SKILL_ALIASES = {
    "C#": (r"\bc\s*#", r"\bcsharp\b"),
    ".NET": (r"\.net\b", r"\bdotnet\b", r"\basp\.net(?: core)?\b"),
    "Java": (r"\bjava\b",),
    "Python": (r"\bpython\b",),
    "JavaScript": (r"\bjavascript\b",),
    "TypeScript": (r"\btypescript\b",),
    "React": (r"\breact(?:\.js)?\b",),
    "Angular": (r"\bangular\b",),
    "Vue": (r"\bvue(?:\.js)?\b",),
    "Node.js": (r"\bnode(?:\.js)?\b",),
    "PostgreSQL": (r"\bpostgres(?:ql)?\b",),
    "SQL Server": (r"\bsql server\b", r"\bmssql\b"),
    "MySQL": (r"\bmysql\b",),
    "MongoDB": (r"\bmongodb\b",),
    "Redis": (r"\bredis\b",),
    "AWS": (r"\baws\b", r"\bamazon web services\b"),
    "Azure": (r"\bazure\b",),
    "Docker": (r"\bdocker\b",),
    "Kubernetes": (r"\bkubernetes\b", r"\bk8s\b"),
    "Git": (r"\bgit\b",),
    "Terraform": (r"\bterraform\b",),
    "FastAPI": (r"\bfastapi\b",),
    "ASP.NET Core": (r"\basp\.net core\b",),
    "Entity Framework": (r"\bentity framework(?: core)?\b", r"\bef core\b"),
    "Machine Learning": (r"\bmachine learning\b",),
    "scikit-learn": (r"\bscikit[ -]learn\b",),
}

EMAIL_PATTERN = re.compile(r"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", re.IGNORECASE)
PHONE_PATTERN = re.compile(r"(?<!\w)(?:\+?\d[\d(). \-]{7,}\d)(?!\w)")
EXPERIENCE_DURATION_PATTERN = re.compile(
    r"\b(?:(?P<years>\d{1,2}(?:\.\d+)?)\s*(?:years?|yrs?)(?:\s*(?:and\s*)?(?P<months>\d{1,2})\s*(?:months?|mos?))?|(?P<months_only>\d{1,2})\s*(?:months?|mos?))"
    r"\s*(?:of\s+)?(?P<label>professional|internship|work)?\s*experience\b",
    re.IGNORECASE,
)
MONTH_NAME = r"(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)"
DATE_VALUE = rf"(?:{MONTH_NAME}\.?\s+)?(?:19|20)\d{{2}}"
YEAR_RANGE_PATTERN = re.compile(rf"\b({DATE_VALUE})\s*[-–—]\s*(Present|Current|{DATE_VALUE})\b", re.IGNORECASE)
DEGREE_PATTERN = re.compile(r"\b(Ph\.?D\.?|M\.?B\.?A\.?|M\.?S\.?|M\.?Sc\.?|B\.?S\.?|B\.?Sc\.?|B\.?A\.?|B\.?Tech\.?|M\.?Tech\.?|B\.?E\.?|M\.?E\.?|BCA|MCA|Bachelor(?:'s)?|Master(?:'s)?|Associate(?:'s)?|Diploma)\b", re.IGNORECASE)
COMPANY_LINE_PATTERN = re.compile(
    rf"^\s*(?P<left>[^|•]{{2,100}}?)\s*[|•]\s*(?P<right>[^|•]{{2,100}}?)\s*[|•]\s*(?P<dates>{DATE_VALUE}\s*[-–—]\s*(?:Present|Current|{DATE_VALUE}))\s*$",
    re.IGNORECASE,
)
ROLE_HINT_PATTERN = re.compile(r"\b(engineer|developer|analyst|manager|consultant|designer|intern|trainee|associate|specialist|administrator|architect|lead|officer|executive|scientist|technician)\b", re.IGNORECASE)


def extract_text(content: bytes, extension: str) -> ExtractedDocument:
    if not content:
        return ExtractedDocument("")
    if extension == ".pdf":
        if not content.startswith(b"%PDF-"):
            raise ValueError("The PDF signature is invalid.")
        try:
            with fitz.open(stream=content, filetype="pdf") as document:
                if document.page_count > MAX_PDF_PAGES:
                    raise ValueError("The PDF contains too many pages to process safely.")
                text = "\n".join(page.get_text("text") for page in document)
        except (fitz.FileDataError, RuntimeError) as exc:
            raise ValueError("The PDF is corrupted or unreadable.") from exc
    elif extension == ".docx":
        if not content.startswith(b"PK\x03\x04"):
            raise ValueError("The DOCX signature is invalid.")
        try:
            document = Document(io.BytesIO(content))
            parts = [paragraph.text for paragraph in document.paragraphs]
            parts.extend(cell.text for table in document.tables for row in table.rows for cell in row.cells)
            text = "\n".join(parts)
        except Exception as exc:
            raise ValueError("The DOCX is corrupted or unreadable.") from exc
    else:
        raise ValueError("Only PDF and DOCX documents are supported.")

    cleaned = "\n".join(line.strip() for line in text.replace("\x00", " ").splitlines() if line.strip())
    warnings: list[str] = []
    if len(cleaned) > MAX_TEXT_CHARS:
        cleaned = cleaned[:MAX_TEXT_CHARS]
        warnings.append("text_truncated")
    if extension == ".pdf" and not cleaned:
        warnings.append("ocr_required")
    return ExtractedDocument(cleaned, tuple(warnings))


def _extract_name(lines: list[str], email: str | None, phone: str | None) -> str | None:
    excluded = {"resume", "curriculum vitae", "cv", "professional profile", "contact"}
    for line in lines[:8]:
        candidate = EMAIL_PATTERN.sub("", line)
        candidate = PHONE_PATTERN.sub("", candidate).strip(" |,;:-")
        words = candidate.split()
        if candidate.lower() in excluded or not 2 <= len(words) <= 5:
            continue
        if any(char.isdigit() for char in candidate) or "@" in candidate or len(candidate) > 70:
            continue
        if all(re.fullmatch(r"[A-Za-z][A-Za-z'.-]*", word) for word in words):
            return candidate
    return None


def parse_text(text: str, extraction_warnings: tuple[str, ...] = ()) -> ParsedResume:
    if not text.strip():
        return ParsedResume(status="needs_ocr" if "ocr_required" in extraction_warnings else "empty",
                            warnings=list(extraction_warnings) or ["no_text_extracted"])

    email_match = EMAIL_PATTERN.search(text)
    email = email_match.group(0).lower() if email_match else None
    phone = next((match.group(0).strip() for match in PHONE_PATTERN.finditer(text)
                  if 10 <= len(re.sub(r"\D", "", match.group(0))) <= 15), None)
    duration_matches = list(EXPERIENCE_DURATION_PATTERN.finditer(text))
    years: float | None = None
    labelled_durations: dict[str, float] = {}
    all_durations: list[float] = []
    for match in duration_matches:
        duration = (float(match.group("years") or 0) + float(match.group("months") or 0) / 12
                   if match.group("years") else float(match.group("months_only")) / 12)
        all_durations.append(duration)
        label = (match.group("label") or "").casefold()
        if label:
            labelled_durations[label] = max(labelled_durations.get(label, 0), duration)
    if "professional" in labelled_durations and "internship" in labelled_durations:
        years = labelled_durations["professional"] + labelled_durations["internship"]
    elif all_durations:
        years = max(all_durations)

    found_skills: list[tuple[int, str]] = []
    lowered = text.lower()
    for canonical, patterns in SKILL_ALIASES.items():
        matches = [re.search(pattern, lowered) for pattern in patterns]
        positions = [match.start() for match in matches if match]
        if positions:
            found_skills.append((min(positions), canonical))
    skills = [name for _, name in sorted(found_skills)]

    education: list[EducationEntry] = []
    lines = [line.strip(" •\t") for line in text.splitlines() if line.strip()]
    seen_institutions: set[str] = set()
    for index, line in enumerate(lines):
        degree = DEGREE_PATTERN.search(line)
        institution_line = line if re.search(r"\b(university|college|institute|polytechnic)\b", line, re.IGNORECASE) else None
        if institution_line is None and degree and index + 1 < len(lines):
            following = lines[index + 1]
            if re.search(r"\b(university|college|institute|polytechnic)\b", following, re.IGNORECASE):
                institution_line = following
        if institution_line:
            institution_line = institution_line.split("|")[0].strip()
        if institution_line and institution_line.casefold() not in seen_institutions:
            seen_institutions.add(institution_line.casefold())
            field = re.search(r"\b(?:in|of)\s+(.+)$", line, re.IGNORECASE) if degree else None
            education.append(EducationEntry(institution=institution_line[:250], degree=degree.group(0) if degree else None,
                                            field_of_study=field.group(1).strip()[:150] if field else None))

    experience: list[ExperienceEntry] = []
    previous_companies: list[str] = []
    seen_companies: set[str] = set()
    for line in lines:
        match = COMPANY_LINE_PATTERN.search(line)
        if match:
            left = match.group("left").strip(" ,")
            right = match.group("right").strip(" ,")
            left_is_role = bool(ROLE_HINT_PATTERN.search(left))
            right_is_role = bool(ROLE_HINT_PATTERN.search(right))
            if not left_is_role and not right_is_role:
                continue
            if right_is_role or not left_is_role:
                company, title = left, right
            else:
                title, company = left, right
            experience.append(ExperienceEntry(company=company, job_title=title, date_range=match.group("dates").strip()))
            if company.casefold() not in seen_companies:
                seen_companies.add(company.casefold())
                previous_companies.append(company)

    projects: list[ProjectEntry] = []
    project_heading = re.compile(r"^(?:PROJECTS?|SELECTED PROJECTS|PERSONAL PROJECTS)$", re.IGNORECASE)
    section_heading = re.compile(r"^(?:PROFESSIONAL\s+)?(?:WORK\s+)?EXPERIENCE|EDUCATION|CERTIFICATIONS?|TECHNICAL SKILLS|SKILLS|LANGUAGES|SUMMARY|PROFILE|ACHIEVEMENTS|PUBLICATIONS|PROJECTS?$", re.IGNORECASE)
    for index, line in enumerate(lines):
        if not project_heading.fullmatch(line):
            continue
        end = index + 1
        while end < len(lines) and not section_heading.fullmatch(lines[end]):
            end += 1
        project_lines = lines[index + 1:end]
        if not project_lines:
            continue
        title_line = project_lines[0]
        date_match = YEAR_RANGE_PATTERN.search(title_line)
        date_range = date_match.group(0) if date_match else None
        name = title_line[:date_match.start()].strip(" |•-–—") if date_match else title_line.split("|")[0].strip(" |•-–—")
        description_lines = project_lines[1:]
        description = " ".join(description_lines)[:4000] or None
        if name:
            projects.append(ProjectEntry(name=name[:200], date_range=date_range, description=description))

    warnings = list(extraction_warnings)
    if email is None:
        warnings.append("email_not_found")
    if phone is None:
        warnings.append("phone_not_found")
    if years is None:
        warnings.append("experience_not_explicitly_stated")
    if not education:
        warnings.append("education_not_confidently_extracted")
    if not experience:
        warnings.append("experience_history_not_confidently_extracted")

    return ParsedResume(status="completed", name=_extract_name(lines, email, phone), email=email, phone=phone,
                        total_experience=years, skills=skills, education=education, experience=experience, projects=projects,
                        previous_companies=previous_companies, warnings=warnings)


@app.get("/health", tags=["health"])
def health() -> dict[str, str]:
    return {"status": "ok", "service": "ai-service"}


@app.post("/v1/resume/parse", response_model=ParsedResume)
async def parse_resume(request: Request, filename: str) -> ParsedResume:
    content = await request.body()
    if len(content) > MAX_BYTES:
        raise HTTPException(status_code=413, detail="The resume exceeds the 10 MB processing limit.")
    extension = PurePath(filename).suffix.lower()
    try:
        extracted = extract_text(content, extension)
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    return parse_text(extracted.text, extracted.warnings)

