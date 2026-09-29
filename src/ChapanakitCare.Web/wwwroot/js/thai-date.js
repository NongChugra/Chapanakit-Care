window.ThaiDate = (() => {
    const parseIso = iso => {
        if (!/^\d{4}-\d{2}-\d{2}$/.test(iso || '')) return null;
        const [year, month, day] = iso.split('-').map(Number);
        const date = new Date(0);
        date.setUTCFullYear(year, month - 1, day);
        if (year < 1 || year > 9456 || date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) return null;
        return { year, month, day };
    };

    const isoToDisplay = iso => {
        const date = parseIso(iso);
        return date ? `${String(date.day).padStart(2, '0')}/${String(date.month).padStart(2, '0')}/${date.year + 543}` : '';
    };

    const displayToIso = display => {
        if (!/^\d{2}\/\d{2}\/\d{4}$/.test(display || '')) return '';
        const [day, month, buddhistYear] = display.split('/').map(Number);
        const iso = `${String(buddhistYear - 543).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
        return parseIso(iso) ? iso : '';
    };

    const completedAge = (birthIso, asOfIso) => {
        const birth = parseIso(birthIso), asOf = parseIso(asOfIso);
        if (!birth || !asOf || birthIso > asOfIso) return null;
        let age = asOf.year - birth.year;
        const leap = asOf.year % 4 === 0 && (asOf.year % 100 !== 0 || asOf.year % 400 === 0);
        const anniversaryDay = birth.month === 2 && birth.day === 29 && !leap ? 28 : birth.day;
        if (asOf.month < birth.month || (asOf.month === birth.month && asOf.day < anniversaryDay)) age--;
        return age;
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
        let editing = false;
        const syncFromTarget = () => { if (!editing) input.value = isoToDisplay(target.value); };
        const syncToTarget = () => {
            input.value = mask(input.value);
            const iso = displayToIso(input.value);
            input.setCustomValidity(input.value && !iso ? 'กรุณากรอกวันที่เป็น วว/ดด/ปปปป' : '');
            if (target.value !== iso) {
                target.value = iso;
                editing = true;
                target.dispatchEvent(new Event('change'));
                editing = false;
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
            const gregorianYear = Number(year);
            target.value = `${gregorianYear}-${month}`;
            input.value = `${month}/${gregorianYear + 543}`;
        };
        const save = () => {
            input.value = monthMask(input.value);
            const match = input.value.match(/^(\d{2})\/(\d{4})$/);
            const month = match ? Number(match[1]) : 0;
            const year = match ? Number(match[2]) - 543 : 0;
            const valid = month >= 1 && month <= 12 && year >= 1 && year <= 9456;
            input.setCustomValidity(input.value && !valid ? 'กรุณากรอกเดือนเป็น ดด/ปปปป' : '');
            if (!input.value) { target.value = ''; return; }
            target.value = valid ? `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}` : '';
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

    return { isoToDisplay, displayToIso, completedAge, mask, monthMask };
})();
