export async function synchronizeAssist(assist,desired){
 const current=await assist.read();
 const keys=['skipDeath','teamSpawn','autoSpot','skipKey'];
 if(keys.every(k=>current.value[k]===desired[k]))return {changed:false};
 await assist.save({...desired,revision:current.revision});
 return {changed:true};
}
export async function runIsolatedSteps(steps){
 const results={};
 for(const step of steps){
  try{results[step.key]=await step.run();}
  catch(error){results[step.key]={state:'error',message:error?.message||String(error)};}
 }
 return results;
}

export function deathPatchAlreadyApplied(state,pid){return pid>0&&state?.state==='applied'&&state.pid===pid;}

export function buildEffectsConfig(mods){
 const result={speedPercent:100,noRecoil:false,noSpread:false,noBulletDrop:false,fastBullets:false,flight:false,cameraHotkey:false,thirdPerson:false,cameraDistance:3,cameraHeight:0.6};
 for(const mod of mods.filter(m=>m.enabled&&m.manifest.adapter==='player-effects')){
  const fields=new Set((mod.manifest.fields||[]).map(f=>f.id));
  for(const key of Object.keys(result))if(fields.has(key)&&Object.hasOwn(mod.config||{},key))result[key]=mod.config[key];
 }
 return result;
}
