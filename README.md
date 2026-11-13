# BF4 Offline BOT Launcher

面向 Windows 的《战地风云 4》本机离线 BOT 启动器，当前版本为 `0.3.0-1107`。
提供服务启动与检查、地图轮换、服务器设置、玩家管理和版本化模组管理。

> 这是源码上传整理目录，不是对所有第三方材料的授权确认。公开发布前请阅读 [第三方材料核对清单](docs/UPLOAD_REVIEW.md) 和 [许可状态](LICENSE-STATUS.md)。本项目不是 EA/DICE 官方产品。

## 目录结构

| 路径 | 内容 |
| --- | --- |
| 根目录 `*.cs` / `*.xaml` | WPF 程序及模型、界面回归测试 |
| `Launcher.csproj` | .NET 项目配置 |
| `Assets/` | 界面资源 |
| `engine-template/` | 基础引擎、Node 运行时和公共组件，不包含模组包 |
| `mods/<id>/<version>/` | 模组清单、默认参数、执行程序、依赖及源码 |
| `mods/catalog.json` | 新识别模组的初始版本，不自动升级已有选择 |
| `tests/` | Node 与发布包检查 |
| `EXTERNAL-MOD-API.md` | 模组接口与本地操作协议 |
| `先读我.txt` | 玩家安装、使用及更新说明 |

当前包含 13 种模组、17 个版本。一个模组只显示一张卡片，各版本参数独立保存。
模组文件夹与 EXE 分离，不由 EXE 自动解压或恢复。

## 开发环境

- Windows x64。
- .NET 8 SDK。
- Windows PowerShell 5.1 或 PowerShell 7。
- Python 3，用于制作和检查发布包。
- Git，用于提交源码。

仓库保留了当前构建流程必需的运行依赖。详情见 [依赖说明](docs/DEPENDENCIES.md)。
玩家不需要安装 .NET SDK。玩家需自行提供兼容的游戏和离线服务端 v2（1107），本仓库不包含完整游戏或完整服务端。

## 编译和打包

在仓库根目录执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
python .\package-release.py
```

编译输出为 `publish/BF4Launcher-0.3.0-1107.exe`。
完整发布 ZIP 输出到 `dist/`，顶层只包含本体 EXE、`mods/` 和 `先读我.txt`。
模组参数根据 `mod.json` 默认值生成，不打包个人参数、存档和运行日志。

`engine-payload.zip` 是构建中间文件，由脚本重新生成，不需要提交。
普通 `dotnet build` 之前也应先运行一次 `build.ps1`，以生成该资源文件。

## 测试

```powershell
node --test tests/*.test.mjs
python tests/pure-release-audit.py
```

发布包检查默认只使用独立测试目录，不读取开发者的实际游戏实例。
如需额外验证已有配置兼容性，可显式指定一个实例目录：

```powershell
python tests/pure-release-audit.py --existing-instance "E:\BF4-instance"
```

该选项只复制待测配置与模组到独立目录，不复制原实例的运行进程记录，不修改原实例。

启动器还提供以下开发检查参数：

- `--self-test`：模型与配置测试。
- `--desktop-self-test`：WPF 界面和本地音效试听流程检查；测试目录需另放外部 `mods`。
- `--bf4-ui-capture`：界面预览截图。

截图及界面测试模式不会启动游戏服务。模组的真人对局效果需要另外进行游戏内验证。

## 源码管理约定

- 根目录 `bin/`、`obj/`、`publish/`、`dist/`、`.qa/` 等是生成物，不提交。
- `mods/<id>/<version>/bin/` 是当前运行依赖的一部分，不能与根目录 `bin/` 一起忽略。
- 保留 `.gitattributes`，避免 Git 自动改写换行符后破坏模组文件校验和。
- 修改模组代码、辅助程序或依赖后，更新对应的 `checksums.json`；个人 `config.json` 不参与代码校验。
- 不提交实际运行的根 `config.json`、`host/`、`data/`、日志、个人音频或凭据。
- 已编译的玩家发布包单独放在 Release 附件，不混入源码文件列表。
