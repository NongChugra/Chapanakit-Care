"""Freeze/check all tracked and untracked repository inputs; exclude only this assurance directory."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[3]
evidence = Path(__file__).resolve().parent
prefix = evidence.relative_to(root).as_posix() + "/"
base = json.loads((evidence / "base-state.json").read_text(encoding="utf-8-sig"))
baseline = {x["path"]: x["sha256"] for x in base["files"]}
paths = subprocess.check_output(
    ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=root
).decode().split("\0")
records = []
for relative in sorted(set(paths) | set(baseline)):
    if not relative or relative.startswith(prefix):
        continue
    path = root / relative
    records.append({"path": relative, "sha256": hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None})
canonical = json.dumps(records, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()
identity = "sha256:" + hashlib.sha256(canonical).hexdigest()
manifest = {
    "schemaVersion": "coordinator-candidate-v1", "baseHead": base["baseHead"],
    "recipe": "git ls-files --cached --others --exclude-standard -z union base-state paths; sorted unique paths; SHA256 raw bytes (null for deletion); exclude docs/assurance/coordinator-selection/**; aggregate canonical UTF8 JSON file records",
    "frozenCandidateId": identity, "files": records,
    "changedSinceTaskBase": [x["path"] for x in records if baseline.get(x["path"]) != x["sha256"]],
    "preservedBaseFiles": [x["path"] for x in records if x["path"] in baseline and baseline[x["path"]] == x["sha256"]],
    "deliveryArtifacts": "Not applicable: source delivery. Temporary published output is verification evidence, not an installed or delivered application."
}
target = evidence / "candidate-manifest.json"
if "--check" in sys.argv:
    frozen = json.loads(target.read_text(encoding="utf-8"))
    if frozen["frozenCandidateId"] != identity:
        raise SystemExit("Candidate changed: " + identity)
    print("Candidate unchanged: " + identity)
else:
    target.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(identity)
    print("Changed since task base: " + str(len(manifest["changedSinceTaskBase"])))
