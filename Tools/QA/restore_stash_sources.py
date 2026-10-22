"""Restore verified source/asset blobs only; leave Git's index and history alone."""
from pathlib import Path
import json,hashlib,zipfile,os,subprocess

repo=Path('D:/Download/ProjectVTC/CapstoneProject')
backup=Path('E:/UnityWork/GitRescue/CapstoneProject-2026-10-21-stash7a181c50')
manifest=json.loads((backup/'manifest.json').read_text(encoding='utf-8'))
if subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo).decode().strip()!=manifest['head']:raise SystemExit('HEAD changed; refusing restore')
archive=backup/'source-and-assets.zip'
if hashlib.sha256(archive.read_bytes()).hexdigest()!=manifest['archive_sha256']:raise SystemExit('Backup hash mismatch')
def oid(data):return hashlib.sha1(f'blob {len(data)}\0'.encode()+data).hexdigest()
def current_oid(target,path,accepted):
    if not target.is_file():return None
    raw=oid(target.read_bytes())
    if raw in accepted:return raw
    # The repository uses core.autocrlf=true. Compare canonical Git content,
    # so an unchanged CRLF working file is not mistaken for a local edit.
    return subprocess.check_output(['git','hash-object','--path='+path,str(target)],cwd=repo).decode().strip()
actions=[];already=0
for record in manifest['preserve']:
    path=record['path'];target=repo/path
    if path.startswith('/') or '..' in path.split('/') or ':' in path or path.startswith('.git/'):raise SystemExit('Unsafe path')
    desired=record['stash'];head=record['head']
    if desired and desired['mode'] not in ('100644','100755'):raise SystemExit('Special file needs separate handling: '+path)
    expected=desired['oid'] if desired else None
    current=current_oid(target,path,{expected,head['oid'] if head else None})
    if current==expected:already+=1;continue
    if desired==head:raise SystemExit('Working tree changed after snapshot: '+path)
    if current!=(head['oid'] if head else None):raise SystemExit('New local contents would be overwritten: '+path)
    actions.append(record)
with zipfile.ZipFile(archive) as z:
    for record in actions:
        target=repo/record['path']
        if record['stash'] is None:
            target.unlink();continue
        data=z.read(record['path'])
        if hashlib.sha256(data).hexdigest()!=record['sha256']:raise RuntimeError('ZIP entry mismatch')
        target.parent.mkdir(parents=True,exist_ok=True)
        temporary=target.with_name(target.name+'.stash-rescue-tmp')
        with temporary.open('xb') as f:f.write(data)
        os.replace(temporary,target)
for record in manifest['preserve']:
    target=repo/record['path'];expected=record['stash']
    if expected is None:
        if target.exists():raise RuntimeError('Deletion not restored: '+record['path'])
    elif current_oid(target,record['path'],{expected['oid']})!=expected['oid']:raise RuntimeError('Restored blob mismatch: '+record['path'])
print(json.dumps({'verified_files':len(manifest['preserve']),'restored_files':len(actions),'already_matching':already,'qa_data_restored':False,'stash_removed':False},indent=2))
