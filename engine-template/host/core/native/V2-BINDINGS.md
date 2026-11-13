# Native v2 runtime bindings

The upstream v2 hook installs new detours into the unchanged dedicated-server executable. The previous helper DLLs keep instruction and predecessor-handler guards for v1; accepting the v2 file hash alone does not make those guards compatible.

NativeV2Bindings.cs verifies the exact server and v2 hook hashes, process image path/start time, preferred module bases and original helper hashes. It follows a bounded jump-thunk chain into the verified v2 hook module, then creates an instance-local helper image:

- BF4BotProgress.dll: update only the eight-byte expected signature for the v2-detoured player-identity method at server RVA 0x3F5EF0. The progress callsite and its original target remain unchanged. Conditional checks and executable helper code remain intact.
- BF4BotDamage.dll: bind the predecessor-handler RVA to the verified v2 handler at hook RVA 0xAB590. Also bind the thread-local index slot (v1 RVA 0xA8A8D0 -> v2 RVA 0xD607B0), attacker field (TLS +0x210 -> +0x2C0), and victim field (TLS +0x218 -> +0x2C8). The v2 handler's actual TLS lookup, scoped context writes, original-handler call, and context restore are checked before producing the helper image. The original callsite/calculation guards and predecessor chaining remain intact.

The startup gate records `damageBinding: v2-damage-context-2`. A previous build's percent acknowledgment without this attribution binding reports that a server restart is needed; it is not treated as confirmation of the corrected damage calculation.

The upstream hook, server executable and original helper images are not modified. The produced per-session DLLs are written only to the tracked managed GameServer directory; binding reports include PID/start ticks and source/output checksums.

The v1 AmmoReserveFix is no longer loaded: upstream v2 already fixes unlimited-ammo firing. Selection and fixed-accessory helpers passed their own runtime signature checks on v2. Bot progress flush and damage-configuration acknowledgments were observed in the isolated default-port instance. Acknowledgment is not a substitute for measuring damage against an actual human client.


2026-11-07 damage-context correction: the actual instance-generated helper was compared byte-for-byte with the four bounded replacements. Its compiled scaling routine was executed together with the captured upstream-v2 TLS context writer. The old binding reproduced 25 damage at a 20% setting; the corrected binding yielded 5. Same-instance configuration transitions, bot-to-bot/human-attacker exclusion, damage-object restoration, and upstream context restoration passed. This is a native-code harness, not a human-client bullet measurement. A live isolated server also acknowledged 20/50/0/100% without restarting (about 0.45-0.56s each).
