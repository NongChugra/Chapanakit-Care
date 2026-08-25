"""Generate complete, disposable synthetic members for the local demo database."""

from __future__ import annotations

import argparse
import json
from datetime import date, timedelta
from pathlib import Path


FIRST_NAMES = [
    "กมลชนก", "ขวัญฤดี", "จิรายุ", "ชนากานต์", "ณัฐวุฒิ", "ดวงพร", "ธนกร", "นภัสสร",
    "ปวีณา", "พงศกร", "ภัทรวดี", "มณีรัตน์", "รวิภา", "วรเมธ", "ศิรินภา", "สุเมธ",
    "อรทัย", "อาทิตย์", "กัลยาณี", "ธีรภัทร",
]
LAST_NAMES = ["ใจดี", "สุขสันต์", "มั่นคง", "รุ่งเรือง", "ศรีแพร่", "บุญมี", "พูนผล", "ทองแท้"]
SUBDISTRICTS = ["ร้องกวาง", "ร้องเข็ม", "น้ำเลา", "บ้านเวียง", "ทุ่งศรี"]
VILLAGES = ["บ้านกลาง", "บ้านใหม่", "บ้านเหนือ", "บ้านใต้", "บ้านดอน"]
RELATIONSHIPS = ["บุตร", "คู่สมรส", "พี่น้อง", "หลาน"]


def beneficiary(member_index: int, slot: int) -> dict[str, object]:
    female = (member_index + slot) % 2 == 0
    subdistrict = SUBDISTRICTS[(member_index + slot) % len(SUBDISTRICTS)]
    return {
        "SlotNo": slot,
        "Title": "นาง" if female else "นาย",
        "FirstName": f"ผู้รับสาธิต{member_index:02d}{slot}",
        "LastName": LAST_NAMES[(member_index + slot) % len(LAST_NAMES)],
        "Relationship": RELATIONSHIPS[(member_index + slot) % len(RELATIONSHIPS)],
        "PersonalIdCard": f"2{member_index:010d}{slot:02d}",
        "Mobile": f"082{member_index:04d}{slot:03d}",
        "HouseNo": f"{20 + member_index}/{slot}",
        "Under": VILLAGES[(member_index + slot) % len(VILLAGES)],
        "Moo": str((member_index + slot) % 12 + 1),
        "Subdistrict": subdistrict,
        "District": "ร้องกวาง",
        "Province": "แพร่",
        "PostalCode": "54140",
    }


def member(index: int) -> dict[str, object]:
    female = index % 2 == 0
    birth = date(1955, 1, 1) + timedelta(days=index * 311)
    application = date(2025, 1, 5) + timedelta(days=index * 4)
    beneficiaries = [beneficiary(index, 1)]
    if index % 4 == 0:
        beneficiaries.append(beneficiary(index, 2))
    return {
        "Title": "นาง" if female else "นาย",
        "FirstName": FIRST_NAMES[(index - 1) % len(FIRST_NAMES)],
        "LastName": LAST_NAMES[(index - 1) % len(LAST_NAMES)],
        "Gender": "หญิง" if female else "ชาย",
        "PersonalIdCard": f"1{index:012d}",
        "BirthDate": birth.isoformat(),
        "HouseNo": f"{100 + index}/{index % 3 + 1}",
        "Under": VILLAGES[(index - 1) % len(VILLAGES)],
        "Moo": str(index % 12 + 1),
        "Subdistrict": SUBDISTRICTS[(index - 1) % len(SUBDISTRICTS)],
        "PostalCode": "54140",
        "Mobile": f"081{index:07d}",
        "GroupNo": f"G{(index - 1) // 10 + 1:02d}",
        "ApplicationDate": application.isoformat(),
        "ApprovalDate": (application + timedelta(days=7)).isoformat(),
        "Beneficiaries": beneficiaries,
        "District": "ร้องกวาง",
        "Province": "แพร่",
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("output")
    parser.add_argument("--limit", type=int, default=40)
    args = parser.parse_args()
    if args.limit < 1 or args.limit > 999:
        raise SystemExit("--limit must be between 1 and 999")

    records = [member(index) for index in range(1, args.limit + 1)]
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"synthetic_complete_records": len(records), "output": str(output)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
