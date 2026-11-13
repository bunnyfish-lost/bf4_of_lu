export function evaluateReadiness(result,records,gate){
 const game=records?.processes?.find(p=>p.role==='game');
 const botReady=!!(game&&gate?.phase==='ready'&&Number.isInteger(game.pid)&&game.pid>0&&gate.pid===game.pid&&typeof game.startTicks==='string'&&/^[1-9][0-9]*$/.test(game.startTicks)&&gate.startTicks===game.startTicks);
 return {botReady,fullyStarted:!!(result.online&&result.blaze&&result.gameState==='IN_GAME'&&botReady)};
}
