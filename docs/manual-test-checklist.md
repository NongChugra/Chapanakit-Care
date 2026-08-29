# Manual acceptance checklist

Use a new database or the **ล้างข้อมูลสมาชิก** action before the test. Do not use real member data.

## Member library and entry

- Import the synthetic fixture twice; the second import must not create duplicates.
- Create a complete member, verify a five-digit run number, then edit the address without changing it.
- Leave a required field empty and use Enter; no member should be created. Complete fields and use the button.
- Select a Thai title, verify gender is suggested but remains editable.
- Check birth-date age, approval-date default, editable approval date, and coverage date = approval date + configured wait days (the 181st day when the wait is 180 days).
- Enter a postal code and verify address assistance; manually edit the suggested district/province.
- Search by name, address components, group, and beneficiary fields. Filter by dates/status/location/group.
- Reorder, hide, select components, and sort columns. Close/reopen and confirm the preference remains. Use reset columns.

## Death event and advance counter

- Preview a normal member and verify member facts, two beneficiaries, equal-share display, dynamic service fee, and certificate upload.
- Confirm a payable death; verify the member becomes deceased, advance units become 0, survivors decrement, and the PDF downloads from the death registry.
- Preview a case inside the configured 365-day safeguard. Confirm that it warns only; choosing **เคสไม่จ่าย** requires a reason and changes calculated money to zero.
- Check that the death report shows a second beneficiary on a continuation row with only the beneficiary name.
- Reset advance units. Verify active notifications are acknowledged and pressing the same reset submission twice does not create two resets.
- Confirm the first-day notice and the death notice only appear when deaths are over the configured threshold since the most recent reset.

## Reports and recovery

- Download monthly, death, and ส.ฌ.ก.๑ reports as PDFs; verify Thai text, repeated headers, and page numbers.
- Create a database backup, keep it outside the application folder, and confirm it is a readable SQLite file.
- Verify the history page uses Thai action labels, including the death event.
