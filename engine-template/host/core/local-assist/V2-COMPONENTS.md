# V2 shared client components

BF4VisionAssist.exe serves automatic spotting and team-wide deployment. BF4PlayerEffects.exe serves movement/flight and third-person camera. Both are rebuilt from sources matching the previously installed executables so existing behavior fixes are retained. Their source directories and Build-V2Components.ps1 are shipped with the engine payload.

Only the exact upstream v2 client is accepted. Server validation remains bound to the unchanged dedicated-server image. Native instruction, prior-handler, patch readback and pointer/layout checks remain in force. The team-player extension size is initialized at runtime and is still checked live; a zero value in the on-disk PE data is not treated as a size override.

Camera/flight loaders now use data/environment.json (complete v2-1107 installation), not the obsolete host/managed-install.json marker; they select only this installation's managed client. PowerShell 5.1 loads its Utility module explicitly, with PowerShell 7 also supported.

The damage-context correction from the preceding update is retained. No UAC bypass or game source-file modification is used. Static signatures and isolated server loading are not substitutes for human-client gameplay acceptance.
