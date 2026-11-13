from pathlib import Path
import datetime,hashlib,json,zipfile,subprocess,shutil,uuid,argparse
parser=argparse.ArgumentParser(description='Verify the clean release; optionally compare a read-only copy of an existing instance.');parser.add_argument('--existing-instance',type=Path);args=parser.parse_args()
root=Path(__file__).resolve().parents[1];read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();package=read(root/'dist/latest-package.json');archive=Path(package['archive']);checks={};locations={};qa=root/'.qa';qa.mkdir(exist_ok=True)
startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
expected={(m['id'],m['version']) for m in package['inventory']}
def folder(label):
 p=qa/('release-'+label+'-'+uuid.uuid4().hex[:8]);p.mkdir();locations[label]=str(p);return p
def capture(p):
 r=subprocess.run([str(p/package['executableName']),'--bf4-ui-capture'],cwd=p,startupinfo=startup,timeout=90)
 result=read(p/'bf4-ui-preview-result.json');assert r.returncode==0 and result['success'],result
 return read(p/'config.json'),result['library']
def relocate(p,relative,destination):
 source=(p/relative).resolve();target=(p/destination).resolve();assert source.is_relative_to(p.resolve()) and target.is_relative_to(p.resolve()) and source!=p.resolve() and target!=p.resolve();target.parent.mkdir(parents=True,exist_ok=True);source.rename(target)
with zipfile.ZipFile(archive) as z:
 checks['zip-top-level-is-exe-mods-readme']={n.split('/')[0] for n in z.namelist()}==set(package['topLevelEntries'])
 checks['zip-has-no-root-personal-config-runtime-or-logs']=not any(n.startswith(('host/','engine/','data/','logs/')) or n=='config.json' for n in z.namelist())
 for name in z.namelist():assert not Path(name).is_absolute() and '..' not in Path(name).parts
 fresh=folder('fresh');z.extractall(fresh)
 only=folder('exe-only');(only/package['executableName']).write_bytes(z.read(package['executableName']))
config,library=capture(fresh)
checks['fresh-actual-ui-has-13-mods-17-versions']=library['modCount']==package['modCount']==13 and library['versionCount']==package['versionCount']==17
checks['fresh-all-versions-exist-on-disk']={(p.parent.parent.name,p.parent.name) for p in (fresh/'mods').glob('*/*/mod.json')}==expected
checks['fresh-version-configs-are-manifest-defaults']=all(read(fresh/'mods'/m['id']/m['version']/'config.json')==m['config'] for m in package['inventory'])
checks['fresh-no-mods-are-auto-enabled']=library['enabledCount']==0
checks['fresh-pinned-v2-defaults-come-from-external-catalog']=library['versions']==read(fresh/'mods/catalog.json')['defaultVersions']
checks['fresh-external-entrypoints-exist']=all((fresh/m['directory']/m['manifest']['runtime']['entryPoint']).is_file() for m in config['mods'] if m['manifest']['adapter']=='external')
version_files={str(p.relative_to(fresh)):sha(p) for p in (fresh/'mods').rglob('*') if p.is_file()};capture(fresh)
checks['fresh-restart-does-not-rewrite-mod-files']=all(sha(fresh/p)==value for p,value in version_files.items())
checks['fresh-engine-has-no-builtin-mods-or-catalog']=not (fresh/'engine/builtin-mods').exists() and not (fresh/'engine/builtin-mod-catalog.json').exists()
blank,blank_library=capture(only)
checks['exe-only-does-not-create-mod-folder']=not (only/'mods').exists()
checks['exe-only-ui-has-zero-mods']=blank_library['modCount']==0 and blank_library['versionCount']==0
checks['exe-only-has-no-bundle-install-record']=not (only/'data/builtin-mods-installed.json').exists()
checks['exe-only-base-engine-still-extracts']=(only/'engine/bridge.mjs').exists() and (only/'host/node/node.exe').exists()

# Simulate moving a selected mod away while retaining its cached configuration.
config=read(fresh/'config.json');group=next(g for g in config['modGroups'] if g['id']=='enemy-display');group['enabled']=True
for m in config['mods']:
 if m['manifest']['id']=='enemy-display':m['enabled']=m['manifest']['version']==group['selectedVersion']
(fresh/'config.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8');relocate(fresh,'mods/enemy-display','detached/enemy-display');missing,missing_library=capture(fresh)
checks['removed-mod-is-not-restored']=not (fresh/'mods/enemy-display').exists() and missing_library['modCount']==12 and missing_library['versionCount']==15
checks['removed-mod-is-disabled-not-loaded']=not next(g for g in missing['modGroups'] if g['id']=='enemy-display')['enabled'] and all(not m['enabled'] for m in missing['mods'] if m['manifest']['id']=='enemy-display')
relocate(fresh,'mods','detached/all-other-mods');empty,empty_library=capture(fresh)
checks['whole-mods-folder-is-not-restored-with-existing-config']=not (fresh/'mods').exists() and empty_library['modCount']==0 and empty_library['versionCount']==0
checks['whole-folder-absence-disables-all-cached-mods']=all(not m['enabled'] for m in empty['mods'])

existing_library=None
if args.existing_instance is not None:
 # Read-only copy of the real user instance; no host/runtime or process ownership files are copied.
 prod=args.existing_instance.resolve();existing=folder('existing');shutil.copy2(prod/'config.json',existing/'config.json');shutil.copytree(prod/'mods',existing/'mods');shutil.copy2(fresh/package['executableName'],existing/package['executableName']);before=read(existing/'config.json');protected={str(p.relative_to(prod)):sha(p) for p in (prod/'mods').rglob('*') if p.is_file()};after,existing_library=capture(existing)
 checks['existing-keeps-version-choices-order-and-enabled-state']=after['modGroups']==before['modGroups']
 checks['existing-keeps-all-version-config-and-code-bytes']=all(sha(existing/p)==v for p,v in protected.items())
 checks['existing-keeps-server-settings']=all(after['server'].get(k)==v for k,v in before['server'].items())
 checks['existing-source-mod-files-untouched']=all(sha(prod/p)==v for p,v in protected.items())
checks['archive-unchanged-after-tests']=sha(archive)==package['archiveSha256']
with zipfile.ZipFile(root/'engine-payload.zip') as z:checks['embedded-payload-has-no-mod-packages']=not any(n.startswith(('mods/','engine/builtin-mods/')) or n=='engine/builtin-mod-catalog.json' for n in z.namelist())
report={'at':datetime.datetime.now().astimezone().isoformat(),'passed':sum(checks.values()),'total':len(checks),'checks':checks,'locations':locations,'archive':str(archive),'executableSha256':package['executableSha256'],'freshLibrary':library,'existingLibrary':existing_library,'gameStarted':False}
(root/'dist/pure-release-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8-sig');print(json.dumps(report,ensure_ascii=False,indent=2));assert all(checks.values())
