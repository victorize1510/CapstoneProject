"""Export and verify valuable stash contents before removing generated QA data."""
from pathlib import Path
import subprocess,json,hashlib,zipfile,sys,os

REPO=Path('D:/Download/ProjectVTC/CapstoneProject')
EXPECTED='7a181c5083758095e59a3bd475723234525acc43'
OUT=Path('E:/UnityWork/GitRescue/CapstoneProject-2026-10-21-stash7a181c50')
def git(*args):return subprocess.check_output(['git',*args],cwd=REPO)
def tree(ref):
    out={}
    for row in git('ls-tree','-r','-l','-z',ref).split(b'\0'):
        if not row:continue
        meta,path=row.split(b'\t',1);mode,kind,oid,size=meta.decode().split()
        out[path.decode('utf-8')]={'mode':mode,'kind':kind,'oid':oid,'size':int(size) if size!='-' else 0}
    return out
def disposable(path):
    return path.startswith('artifacts/') or path.startswith('Capstone/Assets/R3AuditSnapshot/') or path=='Capstone/Assets/R3AuditSnapshot.meta'

if git('rev-parse','refs/stash').decode().strip()!=EXPECTED:raise SystemExit('Stash changed; refusing export')
if OUT.exists():raise SystemExit('Backup directory already exists; refusing overwrite')
base=tree(EXPECTED+'^1');stashed=tree(EXPECTED);head=tree('HEAD')
preserve=[];excluded=[]
for path in sorted(set(base)|set(stashed)):
    if base.get(path)==stashed.get(path):continue
    record={'path':path,'base':base.get(path),'stash':stashed.get(path),'head':head.get(path)}
    if disposable(path):excluded.append(record);continue
    if head.get(path) not in (base.get(path),stashed.get(path)):raise SystemExit('Current HEAD conflicts: '+path)
    if path.startswith('/') or '..' in path.split('/') or ':' in path:raise SystemExit('Unsafe archive path')
    if stashed.get(path) and stashed[path]['kind']!='blob':raise SystemExit('Non-blob needs separate handling: '+path)
    preserve.append(record)
OUT.mkdir(parents=True)
batch=subprocess.Popen(['git','cat-file','--batch'],cwd=REPO,stdin=subprocess.PIPE,stdout=subprocess.PIPE)
archive=OUT/'source-and-assets.zip'
with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=3,allowZip64=True) as z:
    for record in preserve:
        blob=record['stash']
        if blob is None:continue
        batch.stdin.write((blob['oid']+'\n').encode());batch.stdin.flush()
        header=batch.stdout.readline().decode().strip().split()
        if len(header)!=3 or header[1]!='blob':raise RuntimeError('Unexpected cat-file response: '+str(header))
        size=int(header[2]);data=batch.stdout.read(size)
        if len(data)!=size or batch.stdout.read(1)!=b'\n':raise RuntimeError('Truncated blob')
        if hashlib.sha1(f'blob {size}\0'.encode()+data).hexdigest()!=blob['oid']:raise RuntimeError('Blob verification failed')
        record['sha256']=hashlib.sha256(data).hexdigest();z.writestr(record['path'],data)
batch.stdin.close();batch.wait()
with zipfile.ZipFile(archive) as z:
    for record in preserve:
        if record['stash'] and hashlib.sha256(z.read(record['path'])).hexdigest()!=record['sha256']:raise RuntimeError('Archive verification failed')
manifest={'original_stash':EXPECTED,'head':git('rev-parse','HEAD').decode().strip(),'branch':git('branch','--show-current').decode().strip(),'archive_sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'preserve':preserve,'excluded':excluded}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'refs-before.txt').write_bytes(git('show-ref'))
(OUT/'VERIFIED.txt').write_text(f'Verified every blob and every ZIP entry.\nPreserved files={len(preserve)}\nExcluded generated QA files={len(excluded)}\nWorking tree, index, stash and history were not modified.\n',encoding='utf-8')
print(json.dumps({'backup':str(OUT),'preserved_files':len(preserve),'preserved_MB':round(sum((r['stash'] or {}).get('size',0) for r in preserve)/1e6,2),'excluded_QA_files':len(excluded),'archive_MB':round(archive.stat().st_size/1e6,2),'verified':True},ensure_ascii=False,indent=2))
