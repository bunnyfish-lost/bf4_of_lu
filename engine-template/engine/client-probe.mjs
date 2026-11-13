export async function probeClient({run,powerShell,script,expected,gate,onError=async()=>{}}){
 try{
  const result=await run(powerShell,['-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',script,'-ExpectedPath',expected],{windowsHide:true,timeout:6000});
  const text=result.stdout.trim().replace(/^\uFEFF/,'');const value=JSON.parse(text);
  if(value?.pid===0&&value.reason==='permission'){gate.observe(null);return {pid:0,ready:false,state:'permission-required',message:'请使用管理员运行，以读取客户端状态。'};}
  if(!value||!Number.isInteger(value.pid)||value.pid<0)throw Error('Invalid client probe response');
  const observed=gate.observe(value);return {...observed,message:observed.ready?'游戏窗口已就绪':observed.pid?'等待游戏窗口稳定':'等待游戏启动'};
 }catch(error){
  gate.observe(null);try{await onError(error);}catch{}
  return {pid:0,ready:false,state:'error',message:'客户端状态读取失败，请查看运行日志。'};
 }
}
