from pathlib import Path
import datetime,hashlib,json,zipfile
root=Path(__file__).resolve().parent;exe=root/'publish/BF4Launcher-0.3.0-1107.exe';mods=root/'mods';readme=root/'先读我.txt';out=root/'dist';out.mkdir(exist_ok=True)
sha=lambda b:hashlib.sha256(b).hexdigest()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
assert exe.is_file() and mods.is_dir() and readme.is_file()
with zipfile.ZipFile(root/'engine-payload.zip') as payload:
 forbidden=[n for n in payload.namelist() if n.lower().startswith(('mods/','engine/builtin-mods/')) or n.lower()=='engine/builtin-mod-catalog.json']
 assert not forbidden,forbidden
inventory=[];records=[];checksum_checks=0
for folder in sorted(mods.glob('*/*')):
 if not folder.is_dir() or not (folder/'mod.json').exists():continue
 manifest=read(folder/'mod.json');assert (manifest['id'],manifest['version'])==(folder.parent.name,folder.name)
 defaults={f['id']:f.get('defaultValue') for f in manifest.get('fields',[])}
 checksums=read(folder/'checksums.json') if (folder/'checksums.json').exists() else {}
 for rel,digest in checksums.items():
  if rel=='config.json':continue
  assert sha((folder/rel).read_bytes())==digest.lower(),str(folder/rel)
  checksum_checks+=1
 for file in sorted(folder.rglob('*')):
  if not file.is_file():continue
  assert not file.is_symlink()
  rel=file.relative_to(folder)
  if any(p in {'.git','.qa','obj','__pycache__'} for p in rel.parts) or file.suffix.lower() in {'.log','.tmp','.pdb'}:continue
  if rel.as_posix()=='config.json':continue
  records.append((file,'mods/'+file.relative_to(mods).as_posix()))
 inventory.append({'id':manifest['id'],'name':manifest['name'],'version':manifest['version'],'config':defaults})
assert len(inventory)==len({m['id']+'@'+m['version'] for m in inventory})
catalog=read(mods/'catalog.json')
for mid,version in catalog['defaultVersions'].items():assert any(m['id']==mid and m['version']==version for m in inventory)
now=datetime.datetime.now().astimezone();stamp=now.strftime('%Y%m%d-%H%M%S');archive=out/f'BF4离线BOT启动器-0.3.0-1107-独立模组版-{stamp}.zip';public_name='BF4Launcher-0.3.0-1107-BF4UI.exe'
with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 z.write(exe,public_name);z.write(readme,'先读我.txt');z.write(mods/'catalog.json','mods/catalog.json')
 for file,name in records:z.write(file,name)
 for item in inventory:z.writestr('mods/'+item['id']+'/'+item['version']+'/config.json',json.dumps(item['config'],ensure_ascii=False,indent=2))
with zipfile.ZipFile(archive) as z:
 assert z.testzip() is None
 assert {n.split('/')[0] for n in z.namelist()}=={public_name,'mods','先读我.txt'}
 assert sha(z.read(public_name))==sha(exe.read_bytes())
 assert not any(n.startswith(('host/','engine/','data/','logs/')) or n=='config.json' for n in z.namelist())
report={'createdAt':now.isoformat(),'archive':str(archive),'archiveSha256':sha(archive.read_bytes()),'executableName':public_name,'executableSha256':sha(exe.read_bytes()),'modCount':len({m['id'] for m in inventory}),'versionCount':len(inventory),'topLevelEntries':[public_name,'mods','先读我.txt'],'sourceChecksumChecks':checksum_checks,'embeddedModPackages':0,'personalConfigIncluded':False,'inventory':inventory}
(out/'latest-package.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8-sig')
print(json.dumps({k:v for k,v in report.items() if k!='inventory'},ensure_ascii=False,indent=2))
