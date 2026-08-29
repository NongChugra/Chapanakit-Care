import assert from 'node:assert/strict';
import test from 'node:test';

globalThis.window = {};
globalThis.document = { addEventListener() {} };

await import('../../src/ChapanakitCare.Web/wwwroot/js/thai-validation.js');

test('required and malformed browser validation messages are Thai', () => {
    assert.equal(window.ThaiValidation.messageFor({ valueMissing: true }), 'กรุณากรอกข้อมูลในช่องนี้');
    assert.equal(window.ThaiValidation.messageFor({ patternMismatch: true }), 'รูปแบบข้อมูลไม่ถูกต้อง กรุณาตรวจสอบอีกครั้ง');
});
