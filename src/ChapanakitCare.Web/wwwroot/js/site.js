document.addEventListener('click', event => {
    const trigger = event.target.closest('[data-dropdown-trigger]');
    document.querySelectorAll('.dropdown.open').forEach(item => {
        if (!trigger && item.contains(event.target)) return;
        if (!trigger || !item.contains(trigger)) item.classList.remove('open');
    });
    if (trigger) trigger.closest('.dropdown')?.classList.toggle('open');
});

document.querySelectorAll('[data-birth-date]').forEach(input => {
    const output = document.querySelector(input.dataset.ageTarget);
    const update = () => {
        if (!input.value || !output) return;
        const birth = new Date(`${input.value}T00:00:00`);
        const today = new Date();
        let age = today.getFullYear() - birth.getFullYear();
        if (today < new Date(today.getFullYear(), birth.getMonth(), birth.getDate())) age--;
        output.value = age >= 0 ? `${age} ปี` : '';
    };
    input.addEventListener('change', update);
    update();
});

document.querySelectorAll('[data-title-input]').forEach(titleInput => {
    const gender = document.querySelector('[data-gender-select]');
    const suggestedGender = title => ({
        'นาย': 'ชาย', 'เด็กชาย': 'ชาย',
        'นาง': 'หญิง', 'นางสาว': 'หญิง', 'น.ส.': 'หญิง', 'เด็กหญิง': 'หญิง'
    })[title.trim()] || '';
    const updateGenderSuggestion = () => {
        const suggestion = suggestedGender(titleInput.value);
        if (suggestion && gender) gender.value = suggestion;
    };
    titleInput.addEventListener('input', updateGenderSuggestion);
    titleInput.addEventListener('change', updateGenderSuggestion);
    if (gender && !gender.value) gender.value = suggestedGender(titleInput.value);
});

document.querySelectorAll('form[data-button-only-submit]').forEach(form => {
    form.addEventListener('keydown', event => {
        if (event.key === 'Enter' && event.target.tagName !== 'TEXTAREA') {
            event.preventDefault();
        }
    });
});

document.querySelectorAll('[data-approval-date]').forEach(input => {
    const output = document.querySelector(input.dataset.coverageTarget);
    const days = Number(input.dataset.waitDays || '180');
    const update = () => {
        if (!input.value || !output) return;
        const date = new Date(`${input.value}T00:00:00`);
        date.setDate(date.getDate() + days);
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        output.value = `${year}-${month}-${day}`;
    };
    input.addEventListener('change', update);
    update();
});

document.querySelectorAll('[data-address-group]').forEach(group => {
    const postal = group.querySelector('[data-address-postal]');
    const district = group.querySelector('[data-address-district]');
    const province = group.querySelector('[data-address-province]');
    if (!postal || !district || !province) return;

    const fromPostal = () => {
        if (postal.value.trim() === '54140') {
            district.value = 'ร้องกวาง';
            province.value = 'แพร่';
        }
    };
    const fromLocation = () => {
        if (district.value.trim() === 'ร้องกวาง' && province.value.trim() === 'แพร่') {
            postal.value = '54140';
        }
    };
    postal.addEventListener('input', fromPostal);
    postal.addEventListener('change', fromPostal);
    district.addEventListener('input', fromLocation);
    district.addEventListener('change', fromLocation);
    province.addEventListener('input', fromLocation);
    province.addEventListener('change', fromLocation);
    if (postal.value.trim()) fromPostal();
    else fromLocation();
});
