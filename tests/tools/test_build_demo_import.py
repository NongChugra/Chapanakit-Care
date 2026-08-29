import unittest
from datetime import date, datetime
from pathlib import Path

from openpyxl import Workbook

from tools.build_demo_import import build_from_workbooks


class DemoImportBuilderTests(unittest.TestCase):
    def test_excel_derived_fixture_is_complete_sanitized_and_active(self):
        root = Path(".codex-work")
        members_path = root / "test-members.xlsx"
        related_path = root / "test-related.xlsx"
        try:
            self._write_members(members_path)
            self._write_related(related_path)

            records = build_from_workbooks(members_path, related_path, limit=2, as_of=date(2026, 8, 26))
        finally:
            members_path.unlink(missing_ok=True)
            related_path.unlink(missing_ok=True)

        self.assertEqual(2, len(records))
        self.assertEqual("101", records[0]["GroupNo"])
        self.assertEqual("บ้านร้องกวาง ม.1", records[0]["Under"])
        self.assertIsNone(records[0]["Mobile"])
        self.assertEqual(13, len(records[0]["PersonalIdCard"]))
        self.assertTrue(records[0]["PersonalIdCard"].isdigit())
        self.assertEqual(2, len(records[0]["Beneficiaries"]))
        self.assertTrue(all(len(person["PersonalIdCard"]) == 13 for person in records[0]["Beneficiaries"]))
        self.assertTrue(all(person["Relationship"] for person in records[0]["Beneficiaries"]))
        self.assertLessEqual(date.fromisoformat(records[0]["ApplicationDate"]), date(2026, 8, 26).replace(year=2026) - __import__('datetime').timedelta(days=180))

    @staticmethod
    def _write_members(path):
        book = Workbook()
        sheet = book.active
        sheet.append(["run_no", "title", "first_name", "last_name", "gender", "personal_id_card", "birthday", "house_no", "under", "moo", "subdistrict", "district", "province", "postal_code", "mobile", "group_no", "member_enter_date"])
        sheet.append([1, "นาง", "จริง", "หนึ่ง", "หญิง", "3-5402-00018-17-5", datetime(2503, 9, 10), "176", "บ้านร้องกวาง ม.1", "1", "ร้องกวาง", "ร้องกวาง", "แพร่", "54140", None, "101", datetime(2017, 12, 31)])
        sheet.append([2, "นาย", "จริง", "สอง", "ชาย", None, None, None, "บ้านร้องเข็ม ม.1", "1", "ร้องเข็ม", "ร้องกวาง", "แพร่", "54140", None, "201", datetime(2026, 8, 1)])
        book.save(path)

    @staticmethod
    def _write_related(path):
        book = Workbook()
        sheet = book.active
        sheet.append(["member_run_no", "related_order", "related_full_name_original", "related_title", "related_first_name", "related_last_name", "relationship", "house_no", "under", "moo", "subdistrict", "district", "province", "postal_code"])
        sheet.append([1, 1, "นาง ผู้รับ หนึ่ง", "นาง", "ผู้รับ", "หนึ่ง", None, None, None, None, None, None, None, None])
        sheet.append([1, 2, "นาย ผู้รับ สอง", "นาย", "ผู้รับ", "สอง", "บุตร", "2", "บ้านร้องกวาง", "1", "ร้องกวาง", "ร้องกวาง", "แพร่", "54140"])
        sheet.append([2, 1, "นาง ผู้รับ สาม", "นาง", "ผู้รับ", "สาม", None, None, None, None, None, None, None, None])
        book.save(path)


if __name__ == "__main__":
    unittest.main()
