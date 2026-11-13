# 外部模组接口 v1

本版将 mods 文件夹与 EXE 完全分开；EXE 不内置模组包、不自动解压或恢复 mods。引擎公共组件仍由 EXE 提供。


本接口让模组携带自己的执行代码。启动器只负责目录识别、配置窗口、优先级、启动/停止和状态展示。
旧的内置适配器继续兼容；新的独立模组使用 `adapter: external`。

## 目录

```text
mods/
  example-mod/
    1.0.0/
      mod.json
      config.json
      bin/ExampleMod.exe
      source/
```

放入目录后，启动时自动识别，也可在“模组管理”点击“刷新目录”。新模组默认关闭；已安装模组导入其他版本时，只增加版本选项，保留当前版本和开关。
ID、版本必须与两层目录名称一致。压缩包根目录直接放 mod.json 和其余文件即可导入。

## mod.json 示例

```json
{
  "formatVersion": 1,
  "id": "example-mod",
  "name": "示例模组",
  "version": "1.0.0",
  "side": "server",
  "kind": "runtime",
  "adapter": "external",
  "description": "代码随模组提供。",
  "fields": [{"id":"amount","label":"倍率","type":"number","min":1,"max":10,"defaultValue":1}],
  "runtime": {
    "apiVersion": 1,
    "type": "executable",
    "entryPoint": "bin/ExampleMod.exe",
    "startWhen": "server",
    "group": "example.feature",
    "arguments": ["--context", "{context}"]
  }
}
```

- type：executable（.exe）、powershell（.ps1）、node（.js/.mjs/.cjs）。
- entryPoint：包内相对路径。
- startWhen：launcher、server、client。
- PowerShell 模组使用用户在程序设置中选定的 5.1 或 7；默认参数为 -ContextFile。
- Node 模组使用启动器已有的 Node 运行时；默认参数为 --context。
- 同一模组 ID 只显示一张卡片，当前版本在模组设置窗口选择；一次只加载所选版本。卡片顺序决定不同模组间的加载与覆盖优先级。
- runtime.group：不同模组 ID 需要共同提供同一运行能力时可以共享组。按卡片顺序合成启用模组的声明字段，由最后一个提供执行程序；同一 ID 的未选版本不参与合成。
- 不同组独立运行。group 省略时使用模组 ID。

## 配置

config.json 放在模组自己的版本目录。配置窗口会写入此文件，并在启动器配置中保留目录、开关和界面所需的配置快照。
每个版本的参数独立保存，不会自动复制到新版本；切换版本后读取该版本自己的配置。导入包包含 config.json 时以包内参数为初始值，否则使用字段默认值。
直接修改文件后点击“刷新目录”。修改或移动模组代码时先停用该模组。

## 执行上下文

启动器展开 {context} 为运行目录中的 context.json。主要字段：

- apiVersion / instanceId：接口版本和本次实例标识。
- launcherRoot / modDirectory：启动器目录、实际执行程序所属的模组目录。
- stateDirectory：该运行组的持久数据目录，模组更新时保留。
- statusFile / stopFile / logFile：状态文件、停止请求文件、程序日志。
- launcher：启动器 PID 和 UTC 启动 ticks。
- server / client：属于本实例的进程信息（pid、startTicks、executable），未运行时为 null。
- config：按列表顺序合成后的有效配置。
- contributors / providerKey / group：参与配置的条目及当前程序提供者。
- heartbeatAt：宿主更新时间，Unix 毫秒。配置变更时上下文也会更新。

模组应定期重新读取上下文，响应配置变化。检测到 stopFile、启动器退出或宿主心跳超时后，撤回自己的运行时改动并退出。
启动器捕获 stdout/stderr 到 logFile，并在停止超时后结束自己创建的进程树。

## 状态回传

原子写入 statusFile：

```json
{"apiVersion":1,"instanceId":"上下文中的标识","at":1793510000000,"state":"applied","message":"功能已应用","config":{"amount":2}}
```

state 可使用 loading、waiting、applied、default、error、stopped。message 用于模组列表。
config 应对应当前实际应用的有效配置。宿主校验实例标识与时间，不把旧进程状态当作本次加载结果。

外部程序以当前用户权限运行，本接口不提供沙箱。运行组优先级解决配置合成及执行提供者的选择，不自动合并不同原生补丁对同一地址的修改。


## 0.2.2：模组设置操作区与操作接口（API v1 扩展）

`mod.json` 可添加 `tool`：`id`、`title`、`fields` 与 `actions`。tool 定义的操作现在显示在对应版本的模组配置窗口“模组操作”页签；参数页仍使用模组自身的 fields。同名字段复用同一份草稿设置，操作临时输入不会写入模组 config.json。不创建额外导航页，不自动关闭其他版本。

```json
{"tool":{"id":"example","title":"工具名称","fields":[{"id":"player","label":"玩家ID","type":"text","defaultValue":""}],"actions":[{"id":"perform","label":"执行","confirm":"是否执行此操作？"}]}}
```

运行上下文新增 `actionDirectory`。启动器向该目录写入 `<32位十六进制requestId>.request.json`，含 `apiVersion:1`、`instanceId`、`requestId`、`action`、`body`、`at`（UTC毫秒时间戳）。只有已启用且实际提供当前组运行程序的版本能接收已声明的 action。按列表顺序合并配置，最后一个版本提供入口程序。

模组响应 `<requestId>.response.json`，回传相同 instanceId、requestId，另含 `ok:true,value:{message:"完成"}` 或 `ok:false,error:"错误说明"`。35秒内未返回、模组停止或回执身份不匹配时，界面显示对应结果。返回 `value.options` 可更新 select 字段的可选项，格式为 `{字段ID:[{label:"标签",value:"值"}]}`。

Node/PowerShell 子进程可通过 `BF4_LAUNCHER_PS` 读取用户选择的PowerShell路径。现有没有tool的模组无需修改；补给倍率仍可使用原有v1入口。

## 启动器配置迁移（界面调整版 2026-11-08）

根目录 config.json 使用 schemaVersion: 2；modGroups 按顺序记录每个 ID 的 selectedVersion 与 enabled。原 mods 列表作为运行数据快照保留，模组参数以版本目录 config.json 为准。

首次读取旧配置前，会把原文件备份到 data/migrations/config-before-mod-groups-*.json。原来同一模组启用了多个版本时，保留原顺序中最后生效的版本，不按版本号自动升级。

回退到旧界面需要先关闭新版、备份当前 config.json，再从上述迁移备份恢复旧 config.json，并使用原 EXE。旧界面不支持 schemaVersion: 2。版本文件保留原位，旧版运行时组件可从本次更新的 fix-backups 备份恢复。


## 外置初始版本清单

`mods/catalog.json` 可使用 `{"formatVersion":1,"defaultVersions":{"example-mod":"1.2.0"}}` 为首次识别的模组指定初始版本。对应文件必须已经在磁盘上；清单不会安装文件、创建版本目录或升级已有选择。

卸载后不会自动恢复。手动移走文件后刷新目录或重启，缺失版本会停用并从列表隐藏；放回后可重新启用。配置快照继续保留，现有版本参数不会被发布默认值覆盖。

## 单次本地操作

`tool.actions[].execution` 默认为 `runtime`，仍需要启用模组及对应运行实例。只有明确声明为 `local` 的操作可以在模组关闭、运行环境未初始化时使用。

独立模组可声明 `tool.localRuntime`，其 `apiVersion`、`type`、`entryPoint` 与 `arguments` 使用相同入口规范；本地入口始终按单次进程运行，忽略 startWhen，不创建常驻实例。

上下文包含 `apiVersion:1`、`mode:"local-action"`、`requestId`、`action`、`body`、`launcherRoot`、`moduleDirectory`、`modKey`、`stateDirectory` 与 `resultFile`。不会提供或要求服务端、客户端进程。状态目录沿用模组组 ID，便于共享既有个人素材。

入口结束前向 resultFile 写入 `{apiVersion:1,requestId,ok:true,value:{message:"操作完成"}}`；失败写 `ok:false,error`。启动器验证回执，结束后清理单次上下文。此接口没有放宽对局操作或进程归属检查。

战斗音效的试听和导入使用此接口；实际击杀/死亡事件监听继续使用 server 运行实例。
