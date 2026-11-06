import io

import fitz
from docx import Document

from app.main import extract_text, parse_text


def pdf_bytes(text: str) -> bytes:
    document = fitz.open()
    page = document.new_page()
    page.insert_text((72, 72), text)
    content = document.tobytes()
    document.close()
    return content


def docx_bytes(text: str) -> bytes:
    document = Document()
    document.add_paragraph(text)
    stream = io.BytesIO()
    document.save(stream)
    return stream.getvalue()


def test_pdf_extracts_contact_skills_and_explicit_experience():
    content = pdf_bytes("Jordan Smith\nJORDAN.SMITH@example.com\n+1 (555) 123-4567\n5 years of experience\nC# ASP.NET Core React PostgreSQL")
    extracted = extract_text(content, ".pdf")
    parsed = parse_text(extracted.text, extracted.warnings)
    assert parsed.name == "Jordan Smith"
    assert parsed.email == "jordan.smith@example.com"
    assert parsed.phone == "+1 (555) 123-4567"
    assert parsed.total_experience == 5
    assert {"C#", "ASP.NET Core", "React", "PostgreSQL"}.issubset(parsed.skills)


def test_docx_extracts_skills_and_company_from_conservative_format():
    content = docx_bytes("Taylor Morgan\ntaylor@example.com\nSkills: Python and Docker\nSenior Engineer | Acme Labs | 2020 - Present")
    extracted = extract_text(content, ".docx")
    parsed = parse_text(extracted.text, extracted.warnings)
    assert parsed.name == "Taylor Morgan"
    assert parsed.skills == ["Python", "Docker"]
    assert parsed.previous_companies == ["Acme Labs"]


def test_empty_or_scanned_pdf_is_explicit_and_not_fabricated():
    assert parse_text("").status == "empty"
    scanned = parse_text("", ("ocr_required",))
    assert scanned.status == "needs_ocr"
    assert scanned.name is None and scanned.skills == []


def test_corrupted_pdf_signature_is_rejected():
    try:
        extract_text(b"not a pdf", ".pdf")
    except ValueError as error:
        assert "signature" in str(error)
    else:
        raise AssertionError("Invalid PDF was accepted")
