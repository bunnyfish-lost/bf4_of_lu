export function applyModSelection(config){
 if(!Array.isArray(config.modGroups))return config;
 const rows=Array.isArray(config.mods)?config.mods:[],ordered=[],seen=new Set();
 for(const group of config.modGroups){if(!group||seen.has(group.id))continue;seen.add(group.id);for(const row of rows.filter(m=>m.manifest?.id===group.id))ordered.push({...row,enabled:group.enabled===true&&row.manifest?.version===group.selectedVersion});}
 for(const row of rows)if(!seen.has(row.manifest?.id))ordered.push({...row,enabled:false});
 return {...config,mods:ordered};
}
