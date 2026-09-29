import assert from 'node:assert/strict';
import test from 'node:test';
globalThis.window = {};
globalThis.document = { addEventListener() {} };
await import('../../src/ChapanakitCare.Web/wwwroot/js/thai-date.js');
const dates = window.ThaiDate;

test('age uses registration date with matching leap-day birthday rules', () => {
    assert.equal(dates.completedAge('2004-07-19', '2026-09-08'), 22);
    assert.equal(dates.completedAge('2006-07-20', '2026-07-19'), 19);
    assert.equal(dates.completedAge('1965-07-20', '2026-07-19'), 60);
    assert.equal(dates.completedAge('2004-02-29', '2025-02-28'), 21);
    assert.equal(dates.completedAge('', '2026-09-08'), null);
});

test('Buddhist leap days and Gregorian transport round trip without year guessing', () => {
    assert.equal(dates.displayToIso('19/07/2547'), '2004-07-19');
    assert.equal(dates.isoToDisplay('2004-07-19'), '19/07/2547');
    assert.equal(dates.displayToIso('29/02/2567'), '2024-02-29');
    assert.equal(dates.displayToIso('29/02/2566'), '');
    assert.equal(dates.isoToDisplay('2400-01-01'), '01/01/2943');
    assert.equal(dates.isoToDisplay('2023-02-29'), '');
});

test('invalid partial input clears the previously valid hidden date', () => {
    const handlers = {};
    const target = { value: '2004-07-19', addEventListener() {}, dispatchEvent() {} };
    const input = { value: '', dataset: { thaiDateTarget: '#birth' },
        addEventListener(name, handler) { handlers[name] = handler; }, setCustomValidity(value) { this.error = value; } };
    let ready;
    const scriptUrl = new URL('../../src/ChapanakitCare.Web/wwwroot/js/thai-date.js', import.meta.url);
    // Execute the actual DOM binding with a minimal event surface.
    return import('node:fs/promises').then(async fs => {
        const vm = await import('node:vm');
        vm.runInNewContext(await fs.readFile(scriptUrl, 'utf8'), { window: {}, Date, Event,
            document: { addEventListener(_, handler) { ready = handler; }, querySelector() { return target; },
                querySelectorAll(selector) { return selector === '[data-thai-date]' ? [input] : []; } } });
        ready();
        input.value = '19/07/25';
        handlers.input();
        assert.equal(target.value, '');
        assert.ok(input.error);
        assert.equal(input.value, '19/07/25');
    });
});
