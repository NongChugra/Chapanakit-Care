import hashlib, json, subprocess
from pathlib import Path
d=Path(__file__).resolve().parent
root=d.parents[2]
files=[]
for p in sorted(set(subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','-z'],cwd=root).decode().split('\0'))):
    if p and not p.startswith('docs/assurance/report-completion/'):
        path=root/p
        files.append({'path':p,'sha256':hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None})
base={'baseHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root).decode().strip(),'status':subprocess.check_output(['git','status','--porcelain=v1'],cwd=root).decode(),'files':files}
(d/'base-state.json').write_text(json.dumps(base,ensure_ascii=False,indent=2),encoding='utf-8')
(d/'attempts.json').write_text(json.dumps(dict(assuranceUnitId='chapanakit-care/reports/reference-completion',reopenGeneration=0,reviewBudgetMode='default',maxReviewCalls=3,reviewCallsUsed=0,activeReviewReservation=None,attempts=[]),indent=2),encoding='utf-8')
print('Report baseline and zero-attempt journal recorded.')
