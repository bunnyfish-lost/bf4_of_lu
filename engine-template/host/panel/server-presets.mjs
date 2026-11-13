export const serverPresets=['CUSTOM','NORMAL','HARDCORE'];
export function readServerPreset(text){
 const commands=[...text.replace(/^\uFEFF/,'').matchAll(/^[\t ]*vars\.preset[\t ]+(\S+)/gmi)];
 return commands.length?commands.at(-1)[1].toUpperCase():'CUSTOM';
}
export function renderServerPreset(text,preset){
 if(!serverPresets.includes(preset))throw Error('请选择自定义、普通或硬核预设');
 const newline=text.includes('\r\n')?'\r\n':'\n';
 let body=text.replace(/^\uFEFF/,'').replace(/^[\t ]*vars\.(?:serverType|preset)[\t ]+[^\r\n]*(?:\r?\n|$)/gmi,'');
 if(body&&!body.endsWith('\n'))body+=newline;
 const header='vars.serverType UNRANKED'+newline+'vars.preset CUSTOM false'+newline;
 return header+body+(preset==='CUSTOM'?'':'vars.preset '+preset+' true'+newline);
}
