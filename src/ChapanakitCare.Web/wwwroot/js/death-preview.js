(() => {
    const checkbox = document.querySelector('[data-nonpay-toggle]');
    const reason = document.querySelector('[data-nonpay-reason]');
    const certificateDate = document.querySelector('#CertificateDate');
    const runNo = document.querySelector('#RunNo');
    const warning = document.querySelector('[data-special-window-warning]');
    const calculation = document.querySelector('[data-death-calculation]');
    const confirm = document.querySelector('[data-death-confirm]');
    const staleNotice = document.querySelector('[data-preview-stale]');
    if (!checkbox || !certificateDate) return;

    const previewDate = certificateDate.value;
    const previewRunNo = runNo?.value;
    const update = () => {
        const stale = certificateDate.value !== previewDate || runNo?.value !== previewRunNo;
        if (calculation) calculation.hidden = stale;
        if (confirm) confirm.disabled = stale;
        if (staleNotice) staleNotice.hidden = !stale;

        const start = Date.parse(`${checkbox.dataset.coverageStart}T00:00:00Z`);
        const death = Date.parse(`${certificateDate.value || ''}T00:00:00Z`);
        const elapsed = (death - start) / 86400000;
        const eligible = Number.isFinite(elapsed) && elapsed >= 0 && elapsed < Number(checkbox.dataset.windowDays);
        checkbox.disabled = stale || !eligible;
        if (stale || !eligible) checkbox.checked = false;
        if (warning) warning.hidden = stale || !eligible;
        document.querySelectorAll('[data-money-value]').forEach(cell => {
            const raw = Number(checkbox.checked ? cell.dataset.nonpay : cell.dataset.pay);
            const value = cell.dataset.satang === 'true' ? raw / 100 : raw;
            cell.textContent = `${value.toLocaleString('th-TH', { minimumFractionDigits: cell.dataset.satang === 'true' ? 2 : 0, maximumFractionDigits: 2 })} ${cell.dataset.unit}`;
        });
        if (reason) {
            reason.required = checkbox.checked;
            reason.disabled = !checkbox.checked;
        }
    };
    checkbox.addEventListener('change', update);
    certificateDate.addEventListener('change', update);
    runNo?.addEventListener('input', update);
    update();
})();
