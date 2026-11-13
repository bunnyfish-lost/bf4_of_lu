using System;
namespace BF4Launcher;
public enum RuntimeReadiness { Stopped, Preparing, Ready }
public enum OperationKind { General, Initialize, StartServer, StopServer, RestartServer, LaunchGame }
public sealed class OperationProgress {
 public string Detail{get;init;}="";public int Step{get;init;}public int Total{get;init;}
}
public static class RuntimeStatusLogic {
 public static RuntimeReadiness Evaluate(bool online,bool blaze,string? gameState,bool botReady){
  if(online&&blaze&&gameState=="IN_GAME"&&botReady)return RuntimeReadiness.Ready;
  return online||blaze?RuntimeReadiness.Preparing:RuntimeReadiness.Stopped;
 }
}
