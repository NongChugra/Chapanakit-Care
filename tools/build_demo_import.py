"""Build a complete, privacy-safe demo fixture from the supplied legacy workbooks."""

from __future__ import annotations

import argparse
import json
from collections import defaultdict
from datetime import date, datetime, timedelta
from pathlib import Path
from typing import Any

import openpyxl


FIRST_NAMES = [
    "กมลชนก", "ขวัญฤดี", "จิรายุ", "ชนากานต์", "ณัฐวุฒิ", "ดวงพร", "ธนกร", "นภัสสร",
    "ปวีณา", "พงศกร", "ภัทรวดี", "มณีรัตน์", "รวิภา", "วรเมธ", "ศิรินภา", "สุเมธ",
    "อรทัย", "อาทิตย์", "กัลยาณี", "ธีรภัทร",
]
LAST_NAMES = ["ใจดี", "สุขสันต์", "มั่นคง", "รุ่งเรือง", "ศรีแพร่", "บุญมี", "พูนผล", "ทองแท้"]


def _rows(path: Path) -> list[dict[str, Any]]:
    workbook = openpyxl.load_workbook(path, read_only=True, data_only=True)
    try:
        sheet = workbook.worksheets[0]
        values = sheet.iter_rows(values_only=True)
        headers = [str(value) for value in next(values)]
        return [dict(zip(headers, row)) for row in values]
    finally:
        workbook.close()


def _text(value: Any, fallback: str) -> str:
    result = "" if value is None else str(value).strip()
    return result or fallback


def _date(value: Any, fallback: date) -> date:
    if isinstance(value, datetime):
        result = value.date()
    elif isinstance(value, date):
        result = value
    else:
        text = "" if value is None else str(value).strip()
        result = fallback
        for separator in ("-", "/"):
            parts = text.split(separator)
            if len(parts) == 3 and all(part.isdigit() for part in parts):
                numbers = [int(part) for part in parts]
                if len(parts[0]) == 4:
                    year, month, day = numbers
                else:
                    day, month, year = numbers
                if year > 2400:
                    year -= 543
                try:
                    result = date(year, month, day)
                except ValueError:
                    result = fallback
                break
    if result.year > 2400:
        result = result.replace(year=result.year - 543)
    return result


def _beneficiary(source: dict[str, Any] | None, member: dict[str, Any], member_index: int, slot: int) -> dict[str, Any]:
    source = source or {}
    title = _text(source.get("related_title"), "นาง" if (member_index + slot) % 2 == 0 else "นาย")
    return {
        "SlotNo": slot,
        "Title": title,
        "FirstName": f"ผู้รับสาธิต{member_index:03d}{slot}",
        "LastName": LAST_NAMES[(member_index + slot) % len(LAST_NAMES)],
        "Relationship": _text(source.get("relationship"), "บุตร"),
        "PersonalIdCard": f"2{member_index:009d}{slot:03d}",
        "Mobile": f"082{member_index:04d}{slot:03d}",
        "HouseNo": _text(source.get("house_no"), f"{20 + member_index}/{slot}"),
        "Under": _text(source.get("under"), _text(member.get("under"), "บ้านสาธิต")),
        "Moo": _text(source.get("moo"), _text(member.get("moo"), "1")),
        "Subdistrict": _text(source.get("subdistrict"), _text(member.get("subdistrict"), "ร้องกวาง")),
        "District": _text(source.get("district"), _text(member.get("district"), "ร้องกวาง")),
        "Province": _text(source.get("province"), _text(member.get("province"), "แพร่")),
        "PostalCode": _text(source.get("postal_code"), _text(member.get("postal_code"), "54140")),
    }


def build_from_workbooks(members_path: Path, related_path: Path, limit: int = 80, as_of: date | None = None) -> list[dict[str, Any]]:
    if limit < 1 or limit > 999:
        raise ValueError("limit must be between 1 and 999")
    as_of = as_of or date.today()
    members = _rows(Path(members_path))
    related_by_member: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for person in _rows(Path(related_path)):
        related_by_member[_text(person.get("member_run_no"), "")].append(person)

    selected = members[:limit]
    if selected and not any(len(related_by_member[_text(row.get("run_no"), "")]) >= 2 for row in selected):
        two_person_member = next((row for row in members[limit:] if len(related_by_member[_text(row.get("run_no"), "")]) >= 2), None)
        if two_person_member is not None:
            selected[-1] = two_person_member

    records: list[dict[str, Any]] = []
    latest_active_application = as_of - timedelta(days=180)
    for index, source in enumerate(selected, start=1):
        source_application = _date(source.get("member_enter_date"), date(2025, 1, 1) + timedelta(days=index))
        application = min(source_application, latest_active_application)
        birth = _date(source.get("birthday"), date(1950, 1, 1) + timedelta(days=index * 173))
        if birth >= application:
            birth = application.replace(year=max(1, application.year - 50))
        run_no = _text(source.get("run_no"), str(index))
        related = sorted(related_by_member[run_no], key=lambda row: int(row.get("related_order") or 0))[:2]
        if not related:
            related = [None]
        title = _text(source.get("title"), "นาง" if index % 2 == 0 else "นาย")
        gender = _text(source.get("gender"), "หญิง" if title in {"นาง", "นางสาว", "น.ส."} else "ชาย")
        records.append({
            "Title": title,
            "FirstName": FIRST_NAMES[(index - 1) % len(FIRST_NAMES)],
            "LastName": LAST_NAMES[(index - 1) % len(LAST_NAMES)],
            "Gender": gender,
            "PersonalIdCard": f"1{index:012d}",
            "BirthDate": birth.isoformat(),
            "HouseNo": f"{100 + index}/{index % 3 + 1}",
            "Under": _text(source.get("under"), "บ้านสาธิต"),
            "Moo": _text(source.get("moo"), "1"),
            "Subdistrict": _text(source.get("subdistrict"), "ร้องกวาง"),
            "PostalCode": _text(source.get("postal_code"), "54140"),
            "Mobile": None,
            "GroupNo": _text(source.get("group_no"), "101"),
            "ApplicationDate": application.isoformat(),
            "ApprovalDate": min(application + timedelta(days=7), as_of).isoformat(),
            "Beneficiaries": [_beneficiary(person, source, index, slot) for slot, person in enumerate(related, start=1)],
            "District": _text(source.get("district"), "ร้องกวาง"),
            "Province": _text(source.get("province"), "แพร่"),
        })
    return records


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("output")
    parser.add_argument("--members-xlsx", required=True)
    parser.add_argument("--related-xlsx", required=True)
    parser.add_argument("--limit", type=int, default=80)
    parser.add_argument("--as-of", type=date.fromisoformat, default=date.today())
    args = parser.parse_args()
    records = build_from_workbooks(Path(args.members_xlsx), Path(args.related_xlsx), args.limit, args.as_of)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({
        "excel_derived_sanitized_records": len(records),
        "members_with_two_beneficiaries": sum(len(record["Beneficiaries"]) == 2 for record in records),
        "all_coverage_active_as_of": args.as_of.isoformat(),
        "output": str(output),
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
