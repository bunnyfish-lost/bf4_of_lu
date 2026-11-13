export function isCurrentHelper(live,context,helperPid,now=Date.now()){
 return Number.isInteger(helperPid)&&helperPid>0&&Number.isInteger(context.client?.pid)&&context.client.pid>0&&Number.isInteger(context.server?.pid)&&context.server.pid>0&&!!live&&live.apiVersion===1&&live.clientBuild==='v2-1107'&&live.instanceId===context.instanceId&&live.pid===helperPid&&live.clientPid===context.client?.pid&&live.serverPid===context.server?.pid&&Number.isSafeInteger(live.at)&&now-live.at>=0&&now-live.at<3000&&['waiting','applied','error'].includes(live.state);
}
