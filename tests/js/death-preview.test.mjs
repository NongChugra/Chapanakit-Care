import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';

test('changing certificate date expires the displayed benefit until recalculated', async () => {
    const handlers = new Map();
    const element = (value = '') => ({ value, dataset: {}, hidden: false, disabled: false,
        addEventListener(event, handler) { handlers.set(this, { ...handlers.get(this), [event]: handler }); } });
    const checkbox = element();
    checkbox.dataset = { coverageStart: '2026-01-01', windowDays: '180' };
    const runNo = element('000123');
    const date = element('2026-03-01');
    const panel = element();
    const confirm = element();
    const staleNotice = element();
    const warning = element();
    const reason = element();
    const lookup = new Map([
        ['[data-nonpay-toggle]', checkbox], ['[data-nonpay-reason]', reason],
        ['#CertificateDate', date], ['#RunNo', runNo], ['[data-special-window-warning]', warning],
        ['[data-death-calculation]', panel], ['[data-death-confirm]', confirm],
        ['[data-preview-stale]', staleNotice]
    ]);
    const path = new URL('../../src/ChapanakitCare.Web/wwwroot/js/death-preview.js', import.meta.url);
    vm.runInNewContext(await readFile(path, 'utf8'), {
        Date, Number, document: { querySelector: selector => lookup.get(selector), querySelectorAll: () => [] }
    });

    assert.equal(panel.hidden, false);
    assert.equal(confirm.disabled, false);
    date.value = '2025-12-31';
    handlers.get(date).change();
    assert.equal(panel.hidden, true);
    assert.equal(confirm.disabled, true);
    assert.equal(staleNotice.hidden, false);
    assert.equal(checkbox.disabled, true);

    date.value = '2026-03-01';
    handlers.get(date).change();
    assert.equal(panel.hidden, false);
    assert.equal(confirm.disabled, false);
    assert.equal(staleNotice.hidden, true);
    runNo.value = '000124';
    handlers.get(runNo).input();
    assert.equal(panel.hidden, true);
    assert.equal(confirm.disabled, true);
});
