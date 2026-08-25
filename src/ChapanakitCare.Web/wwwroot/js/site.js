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

document.querySelectorAll('[data-birth-date-be]').forEach(input => {
    const target = document.querySelector(input.dataset.birthTarget);
    if (!target) return;
    const showBuddhistDate = () => {
        if (!target.value) return;
        const [year, month, day] = target.value.split('-');
        input.value = `${day}/${month}/${Number(year) + 543}`;
    };
    const saveBuddhistDate = () => {
        const match = input.value.trim().match(/^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/);
        if (!match) { target.value = ''; return; }
        const day = Number(match[1]); const month = Number(match[2]); const buddhistYear = Number(match[3]);
        const date = new Date(buddhistYear - 543, month - 1, day);
        if (date.getFullYear() !== buddhistYear - 543 || date.getMonth() !== month - 1 || date.getDate() !== day) { target.value = ''; return; }
        target.value = `${date.getFullYear()}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
        target.dispatchEvent(new Event('change'));
    };
    input.addEventListener('change', saveBuddhistDate);
    input.addEventListener('blur', saveBuddhistDate);
    showBuddhistDate();
});

document.querySelectorAll('[data-be-date-output]').forEach(output => {
    const target = document.querySelector(output.dataset.beTarget);
    if (!target) return;
    const update = () => {
        if (!target.value) { output.value = ''; return; }
        const [year, month, day] = target.value.split('-');
        output.value = `${day}/${month}/${Number(year) + 543}`;
    };
    target.addEventListener('change', update);
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
        output.dispatchEvent(new Event('change'));
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

const groupPrefixes = { 'ร้องกวาง': '01', 'ร้องเข็ม': '02', 'น้ำเลา': '03', 'บ้านเวียง': '04', 'ทุ่งศรี': '05', 'แม่ยางตาล': '06', 'แม่ยางฮ่อ': '07', 'ไผ่โทน': '08', 'ห้วยโรง': '09', 'แม่ทราย': '10', 'แม่ยางร้อง': '11' };
const villagesByMoo = {
    'ร้องกวาง': { '1': 'บ้านร้องกวาง', '2': 'บ้านร้องกวาง', '4': 'บ้านกาศผาแพร่', '5': 'บ้านวังโป่ง', '7': 'บ้านร้องกวาง', '9': 'บ้านร้องกวาง', '12': 'บ้านกาศใต้', '13': 'บ้านร้องกวาง' },
    'ร้องเข็ม': { '1': 'บ้านร้องเข็ม', '2': 'บ้านร้องเข็ม', '3': 'บ้านน้ำโค้ง', '4': 'บ้านดอนมูล', '5': 'บ้านใหม่จัดสรร', '6': 'บ้านปากทางร้องเข็ม', '7': 'บ้านร้องเข็ม', '8': 'บ้านหัวดง', '9': 'บ้านร้องเข็ม' },
    'ทุ่งศรี': { '1': 'บ้านวังหม้อ', '2': 'บ้านผาราง', '3': 'บ้านทุ่งศรี', '4': 'บ้านต้นเดื่อ', '5': 'บ้านปากทางทุ่งศรี' }
};

document.querySelectorAll('[data-member-group]').forEach(memberGroup => {
    const group = memberGroup.closest('[data-address-group]');
    const subdistrict = group?.querySelector('[data-member-subdistrict]');
    const moo = group?.querySelector('[data-moo]');
    const village = group?.querySelector('[data-village]');
    if (!subdistrict || !moo || !village) return;
    const updateFromMoo = () => {
        const code = groupPrefixes[subdistrict.value.trim()];
        if (code && /^\d{1,2}$/.test(moo.value.trim())) memberGroup.value = `${code}${moo.value.trim().padStart(2, '0')}`;
        const villageName = villagesByMoo[subdistrict.value.trim()]?.[moo.value.trim()];
        if (villageName) village.value = villageName;
    };
    const updateFromVillage = () => {
        const matches = Object.entries(villagesByMoo[subdistrict.value.trim()] || {}).filter(([, name]) => name === village.value.trim());
        if (matches.length === 1) { moo.value = matches[0][0]; updateFromMoo(); }
    };
    subdistrict.addEventListener('input', updateFromMoo);
    subdistrict.addEventListener('change', updateFromMoo);
    moo.addEventListener('input', updateFromMoo);
    moo.addEventListener('change', updateFromMoo);
    village.addEventListener('input', updateFromVillage);
    village.addEventListener('change', updateFromVillage);
    updateFromMoo();
});

document.querySelectorAll('[data-demo-toggle]').forEach(button => {
    const panel = document.querySelector('[data-demo-data]');
    const update = () => button.textContent = panel?.hidden ? 'แสดงข้อมูลสาธิต' : 'ซ่อนข้อมูลสาธิต';
    button.addEventListener('click', () => { if (panel) panel.hidden = !panel.hidden; update(); });
    update();
});
