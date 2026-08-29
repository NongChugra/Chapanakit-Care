window.ThaiValidation = (() => {
    const messageFor = validity => {
        if (validity.valueMissing) return 'กรุณากรอกข้อมูลในช่องนี้';
        if (validity.patternMismatch) return 'รูปแบบข้อมูลไม่ถูกต้อง กรุณาตรวจสอบอีกครั้ง';
        if (validity.typeMismatch || validity.badInput) return 'ข้อมูลที่กรอกไม่ถูกต้อง กรุณาตรวจสอบอีกครั้ง';
        if (validity.tooShort) return 'ข้อมูลสั้นเกินไป กรุณากรอกให้ครบ';
        if (validity.tooLong) return 'ข้อมูลยาวเกินกำหนด';
        if (validity.rangeUnderflow || validity.rangeOverflow) return 'ข้อมูลอยู่นอกช่วงที่กำหนด';
        return 'กรุณาตรวจสอบข้อมูลในช่องนี้';
    };

    document.addEventListener('invalid', event => {
        const input = event.target;
        if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement || input instanceof HTMLTextAreaElement)) return;
        if (!input.validity.customError) input.setCustomValidity(messageFor(input.validity));
    }, true);
    document.addEventListener('input', event => {
        const input = event.target;
        if (input?.setCustomValidity) input.setCustomValidity('');
    }, true);

    return { messageFor };
})();
