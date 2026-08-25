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
SUBDISTRICTS = ["ร้องกวาง", "ร้องเข็ม", "ทุ่งศรี"]
GROUP_PREFIX = {"ร้องกวาง": "01", "ร้องเข็ม": "02", "น้ำเลา": "03", "บ้านเวียง": "04", "ทุ่งศรี": "05", "แม่ยางตาล": "06", "แม่ยางฮ่อ": "07", "ไผ่โทน": "08", "ห้วยโรง": "09", "แม่ทราย": "10", "แม่ยางร้อง": "11"}
VILLAGES_BY_MOO = {
    "ร้องกวาง": {"1": "บ้านร้องกวาง", "2": "บ้านร้องกวาง", "4": "บ้านกาศผาแพร่", "5": "บ้านวังโป่ง", "7": "บ้านร้องกวาง", "9": "บ้านร้องกวาง", "12": "บ้านกาศใต้", "13": "บ้านร้องกวาง"},
    "ร้องเข็ม": {"1": "บ้านร้องเข็ม", "2": "บ้านร้องเข็ม", "3": "บ้านน้ำโค้ง", "4": "บ้านดอนมูล", "5": "บ้านใหม่จัดสรร", "6": "บ้านปากทางร้องเข็ม", "7": "บ้านร้องเข็ม", "8": "บ้านหัวดง", "9": "บ้านร้องเข็ม"},
    "ทุ่งศรี": {"1": "บ้านวังหม้อ", "2": "บ้านผาราง", "3": "บ้านทุ่งศรี", "4": "บ้านต้นเดื่อ", "5": "บ้านปากทางทุ่งศรี"},
}
RELATIONSHIPS = ["บุตร", "คู่สมรส", "พี่น้อง", "หลาน"]


def beneficiary(member_index: int, slot: int) -> dict[str, object]:
    female = (member_index + slot) % 2 == 0
    subdistrict = SUBDISTRICTS[(member_index + slot) % len(SUBDISTRICTS)]
    moo, village = list(VILLAGES_BY_MOO[subdistrict].items())[(member_index + slot) % len(VILLAGES_BY_MOO[subdistrict])]
    return {
        "SlotNo": slot,
        "Title": "นาง" if female else "นาย",
        "FirstName": f"ผู้รับสาธิต{member_index:02d}{slot}",
        "LastName": LAST_NAMES[(member_index + slot) % len(LAST_NAMES)],
        "Relationship": RELATIONSHIPS[(member_index + slot) % len(RELATIONSHIPS)],
        "PersonalIdCard": f"2{member_index:010d}{slot:02d}",
        "Mobile": f"082{member_index:04d}{slot:03d}",
        "HouseNo": f"{20 + member_index}/{slot}",
        "Under": village,
        "Moo": moo,
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
    subdistrict = SUBDISTRICTS[(index - 1) % len(SUBDISTRICTS)]
    moo, village = list(VILLAGES_BY_MOO[subdistrict].items())[(index - 1) % len(VILLAGES_BY_MOO[subdistrict])]
    return {
        "Title": "นาง" if female else "นาย",
        "FirstName": FIRST_NAMES[(index - 1) % len(FIRST_NAMES)],
        "LastName": LAST_NAMES[(index - 1) % len(LAST_NAMES)],
        "Gender": "หญิง" if female else "ชาย",
        "PersonalIdCard": f"1{index:012d}",
        "BirthDate": birth.isoformat(),
        "HouseNo": f"{100 + index}/{index % 3 + 1}",
        "Under": village,
        "Moo": moo,
        "Subdistrict": subdistrict,
        "PostalCode": "54140",
        "Mobile": f"081{index:07d}",
        "GroupNo": f"{GROUP_PREFIX[subdistrict]}{int(moo):02d}",
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
