window.ThaiDate = (() => {
    const isoToDisplay = iso => {
        if (!/^\d{4}-\d{2}-\d{2}$/.test(iso || '')) return '';
        const [year, month, day] = iso.split('-');
        const gregorianYear = Number(year) >= 2400 ? Number(year) - 543 : Number(year);
        return `${day}/${month}/${gregorianYear + 543}`;
    };

    const displayToIso = display => {
        if (!/^\d{2}\/\d{2}\/\d{4}$/.test(display || '')) return '';
        const [day, month, buddhistYear] = display.split('/').map(Number);
        const date = new Date(buddhistYear - 543, month - 1, day);
        if (date.getFullYear() !== buddhistYear - 543 || date.getMonth() !== month - 1 || date.getDate() !== day) return '';
        return `${date.getFullYear()}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    };

    const mask = value => {
        const digits = String(value || '').replace(/\D/g, '').slice(0, 8);
        if (digits.length <= 2) return digits;
        if (digits.length <= 4) return `${digits.slice(0, 2)}/${digits.slice(2)}`;
        return `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}`;
    };

    const monthMask = value => {
        const digits = String(value || '').replace(/\D/g, '').slice(0, 6);
        return digits.length <= 2 ? digits : `${digits.slice(0, 2)}/${digits.slice(2)}`;
    };

    const bind = input => {
        const target = document.querySelector(input.dataset.thaiDateTarget);
        if (!target) return;
        const syncFromTarget = () => { input.value = isoToDisplay(target.value); };
        const syncToTarget = () => {
            input.value = mask(input.value);
            const iso = displayToIso(input.value);
            input.setCustomValidity(input.value && !iso ? 'กรุณากรอกวันที่เป็น วว/ดด/ปปปป' : '');
            if (!input.value && target.value) {
                target.value = '';
                target.dispatchEvent(new Event('change'));
                return;
            }
            if (iso && target.value !== iso) {
                target.value = iso;
                target.dispatchEvent(new Event('change'));
            }
        };
        input.addEventListener('beforeinput', event => {
            if (event.inputType.startsWith('insert') && event.data && /\D/.test(event.data)) event.preventDefault();
        });
        input.addEventListener('input', syncToTarget);
        input.addEventListener('blur', syncToTarget);
        target.addEventListener('change', syncFromTarget);
        syncFromTarget();
    };

    const bindOutput = output => {
        const target = document.querySelector(output.dataset.thaiDateOutput);
        if (!target) return;
        const update = () => { output.value = isoToDisplay(target.value); };
        target.addEventListener('change', update);
        update();
    };

    const bindMonth = input => {
        const target = document.querySelector(input.dataset.thaiMonthTarget);
        if (!target) return;
        const render = () => {
            if (!/^\d{4}-\d{2}$/.test(target.value || '')) return;
            const [year, month] = target.value.split('-');
            const gregorianYear = Number(year) >= 2400 ? Number(year) - 543 : Number(year);
            target.value = `${gregorianYear}-${month}`;
            input.value = `${month}/${gregorianYear + 543}`;
        };
        const save = () => {
            input.value = monthMask(input.value);
            const match = input.value.match(/^(\d{2})\/(\d{4})$/);
            const month = match ? Number(match[1]) : 0;
            const year = match ? Number(match[2]) - 543 : 0;
            const valid = month >= 1 && month <= 12 && year >= 1;
            input.setCustomValidity(input.value && !valid ? 'กรุณากรอกเดือนเป็น ดด/ปปปป' : '');
            if (!input.value) { target.value = ''; return; }
            if (valid) target.value = `${year}-${String(month).padStart(2, '0')}`;
        };
        input.addEventListener('beforeinput', event => { if (event.inputType.startsWith('insert') && event.data && /\D/.test(event.data)) event.preventDefault(); });
        input.addEventListener('input', save);
        input.addEventListener('blur', save);
        render();
    };

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-thai-date]').forEach(bind);
        document.querySelectorAll('[data-thai-date-output]').forEach(bindOutput);
        document.querySelectorAll('[data-thai-month]').forEach(bindMonth);
    });

    return { isoToDisplay, displayToIso, mask, monthMask };
})();
