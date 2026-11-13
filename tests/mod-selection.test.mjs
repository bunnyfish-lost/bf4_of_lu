import test from 'node:test';
import assert from 'node:assert/strict';
import {applyModSelection} from '../engine-template/engine/mod-selection.mjs';
import {resolvePlan} from '../engine-template/engine/bridge.mjs';
const mod=(id,version,value,enabled=true)=>({key:`${id}@${version}`,enabled,manifest:{id,version,name:id,operations:[{field:'value',value:'$value'}]},config:{value}});
const group=(id,selectedVersion,enabled=true)=>({id,selectedVersion,enabled});
test('legacy schema without groups retains old plan and ordering',()=>{
 const c={schemaVersion:1,server:{value:0},mods:[mod('a','1',2),mod('a','2',6)]};assert.equal(applyModSelection(c),c);assert.equal(resolvePlan(c).values.value,6);
});
test('grouped selection enables only the chosen version, never merges its siblings',()=>{
 const c={schemaVersion:2,server:{value:0},mods:[mod('a','1',2),mod('a','2',6)],modGroups:[group('a','1')]};const r=applyModSelection(c);assert.deepEqual(r.mods.map(m=>m.enabled),[true,false]);assert.equal(resolvePlan(c).values.value,2);assert.deepEqual(c.mods.map(m=>m.enabled),[true,true]);
});
test('group order is the actual patch override order',()=>{
 const c={server:{value:0},mods:[mod('a','1',2),mod('b','1',4),mod('a','2',6)],modGroups:[group('b','1'),group('a','1')]};assert.deepEqual(applyModSelection(c).mods.map(m=>m.key),['b@1','a@1','a@2']);assert.equal(resolvePlan(c).values.value,2);
});
test('turning a group off disables every installed version',()=>{
 const c={server:{value:0},mods:[mod('a','1',2),mod('a','2',6)],modGroups:[group('a','2',false)]};assert(applyModSelection(c).mods.every(m=>!m.enabled));assert.equal(resolvePlan(c).values.value,0);
});
test('missing selected version never activates another installed version',()=>{
 const c={server:{value:0},mods:[mod('a','1',2)],modGroups:[group('a','missing')]};assert.equal(resolvePlan(c).values.value,0);
});
test('unlisted and newly discovered mods stay off until a group is created',()=>{
 const c={mods:[mod('a','1',2),mod('b','1',5)],modGroups:[group('a','1')]};assert.deepEqual(applyModSelection(c).mods.map(m=>m.enabled),[true,false]);
});
test('duplicate group metadata does not schedule a version twice',()=>{
 const c={mods:[mod('a','1',2),mod('a','2',6)],modGroups:[group('a','1'),group('a','2')]};const r=applyModSelection(c);assert.equal(r.mods.length,2);assert.deepEqual(r.mods.filter(m=>m.enabled).map(m=>m.key),['a@1']);
});
test('disabled-version dependencies and operations are not applied',()=>{
 const stale=mod('a','2',6);stale.manifest.dependencies=['not-installed'];stale.manifest.operations=[{field:'not-a-field',value:9}];const c={server:{value:0},mods:[mod('a','1',2),stale],modGroups:[group('a','1')]};assert.equal(resolvePlan(c).values.value,2);
});
test('normalization is idempotent',()=>{
 const c={mods:[mod('a','1',2),mod('a','2',6),mod('b','1',5)],modGroups:[group('a','1')]};assert.deepEqual(applyModSelection(applyModSelection(c)),applyModSelection(c));
});
