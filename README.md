# DLSS Swapper · FG Preview 0.2

基于 [DLSS Swapper](https://github.com/beeradmoore/dlss-swapper) 的个人实验分支。保留原来的游戏库、DLSS 版本选择、预设和恢复界面，增加 **帧生成解锁 / 倍率设置**。

> 这是非官方 Fork，不是原作者发布的 DLSS Swapper。首版面向 **RTX 3080** 和用户的 **CMP 40HX 解锁版（按 SM75 / 2060S 路线试验）**，测试游戏为 **《黑神话：悟空》与《明末：渊虚之羽》**。这些是待实测目标，不是已经通过的性能/兼容性认证。不会修改驱动、刷固件、伪装显卡或绕过反作弊。

## 0.2：后端选择、日志诊断与完整回退

- 原 Native 0.2.3 仍为默认后端；新增可选 Native 0.2.4，修复上游记录的显存资源滞留和历史帧问题。同一个代理入口可以直接切换版本再应用，异常时选择旧版回退；始终保留首次安装前的原 INI。
- 倍率上限增加 3×。2×/3×/4× 分别写入最多额外生成 1/2/3 帧，仍由游戏实际请求倍率。
- “检查 / 打开后端日志”区分代理重定向、功能创建、生成帧记录和错误事件；早于本次安装的日志不用于验证新后端。最多读取文件前 8 MiB，日志摘要不代表实际呈现倍率、画质或稳定性。
- 包含下述游戏库扫描修复，封面下载不再阻塞本地扫描完成。
- Native 0.2.4 仍有[宽屏/过场画幅变化崩溃反馈](https://github.com/sdli1995/dlssg_for_sm86/issues/325)。先测试 2×；错误时退出游戏、选择 Native 0.2.3 后重新应用。切换代理文件名时仍须先卸载恢复。

40HX 的驱动与自动启动记录见独立的 [CMP40HX-Unlock](https://github.com/ygyp57pxkf-cloud/CMP40HX-Unlock/tree/main/automatic-boot)。该仓库 2026-09-13 记录了特定机器的算力/Gen2 双解锁及 NVENC 主机端验证；这不等于本 Swapper、Native 0.2.4 或两款目标游戏已完成 40HX 实测。

## 0.1.1：修复游戏库一直扫描 / 游戏处理中

- 本地 DLL 扫描不再等待在线封面下载，封面离线也可进入完成本地扫描的游戏。
- 移植并核对上游 [PR #933](https://github.com/beeradmoore/dlss-swapper/pull/933) 的界面线程修复、加载状态清理和扫描并发限制；修复 Steam 等游戏库在后台线程更新绑定属性的问题。
- 封面请求独立限流，Steam 封面元数据和图片下载使用 20 秒请求取消预算；不改 DLSS 文件下载的全局超时。
- 这是已确认代码缺陷的修复，截图不能确定用户机器实际命中哪条故障；需要重新运行确认。若仍卡住，请提供 `StoredData-FG-Preview/logs` 中本次运行日志（实际日志文件位置见下方说明）。

### 从 0.1 升级

退出旧程序，把新版完整解压到一个新目录运行，不覆盖或删除旧目录。先验证游戏库能扫描完成。要沿用设置，可在两版都退出时复制旧目录的 `StoredData-FG-Preview` 到新版旁；游戏目录内的帧生成安装记录与原件备份不受程序解压目录变化影响。不要为了清除转圈而删除游戏文件或 FG 备份。

## 下载与启动

到本 Fork 的 [Releases](https://github.com/ygyp57pxkf-cloud/dlss-swapper/releases) 下载 `DLSS-Swapper-FG-Preview-0.2-win-x64.zip`。这是未签名的 Windows x64 便携测试包，包含 .NET / Windows App SDK 运行文件；解压整个压缩包至可写目录，再运行 `DLSS Swapper.exe`，不要在 ZIP 内直接运行或只拷贝 EXE。

如 Release 尚未提供，打开 [FG Preview Windows build](https://github.com/ygyp57pxkf-cloud/dlss-swapper/actions/workflows/fg-preview.yml)，选择成功构建并下载同名 artifact。Artifacts 下载可能需要登录 GitHub。

系统要求：Windows 10/11 x64（至少 Windows 10 build 19041）、正常工作的 NVIDIA 驱动，以及可通过该后端接入 DLSS FG 的 **D3D12** 游戏。CUDA Toolkit / Python 不需要安装。上游记录使用驱动 591.86，但这不是声明的最低版本；本 Fork 不自动更新驱动。

便携配置放在 `StoredData-FG-Preview`，与官方便携版 `StoredData` 分开。程序更新检查指向本 Fork，避免用官方包覆盖实验功能。新增界面首版使用中文，原有界面语言选择不变。

## 实际包含的功能

| 操作 | 首版行为 |
|---|---|
| 替换 DLSS / DLSSG / RR 等文件 | 保留 Swapper 原功能；仍按原界面选择版本并 Swap |
| 帧生成按钮 | 游戏详情中新增入口，安装固定版本后端并写入配置 |
| 倍率 | 选择 2× 或 4× **上限**，分别写入 `MaxGeneratedFrames=1/3` |
| GPU 推荐 | 尝试读取 `nvidia-smi` 型号、显存和驱动；单 GPU 时建议配置，多 GPU/检测失败由用户确认 |
| 两款目标游戏 | 识别已知渲染 EXE 目录；未验证的版本/显卡组合明确标注 |
| 未适配游戏 | 默认继续普通 DLSS 替换；主动选择 EXE、显卡并确认警告后才尝试 FG 安装 |
| 代理冲突 | 不覆盖其他 Mod；支持上游五个不同的代理文件，切换前先卸载 |
| 恢复 | 备份原 INI，记录本工具安装文件；恢复时保留其他 Mod 与手动修改 |
| 排障 | 内置手动指引、后端日志目录入口和可读错误信息 |

**首版没有自动修改游戏的内部图形设置。** “一键应用”指后端获取、核验、安装和 INI 配置，普通 DLSS 版本仍先在原界面选择。进入游戏后需要启用 DLSS 帧生成；游戏提供倍率菜单时再选择倍率。没有多帧请求接口的游戏，不能靠本工具强制获得 4×。

## 第一次使用：按这几步操作

1. **先退出游戏**。启动本工具，让原游戏库扫描完成；没识别到游戏时使用原有 Add Game 手动添加安装目录。
2. 在游戏详情中，按原流程选择需要的 **DLSS 超分版本** 并 Swap。新版本不保证更快或更清晰；建议先保留一个可稳定运行的版本，只改变一个变量。
3. 点击新增的 **“帧生成解锁 / 倍率设置（实验）”**。
4. 确认实际渲染 EXE。自动匹配失败时点击“选择渲染 EXE”，不要选顶层启动器：

   | 游戏 | 相对游戏安装目录的渲染 EXE |
   |---|---|
   | 黑神话：悟空 | `b1\Binaries\Win64\b1-Win64-Shipping.exe` |
   | 明末：渊虚之羽 | `Project_Plague\Binaries\Win64\Project_Plague-Win64-Shipping.exe` |

   这属于目录适配，不是游戏版本和显卡的运行认证。如果发行平台/版本布局不同，选择其真实渲染 EXE；程序只允许游戏根目录内的 x64 EXE，并拒绝符号链接/重解析点路径。常见兼容性反馈来源：[悟空上游说明](https://github.com/sdli1995/dlssg_for_sm86/blob/1fb9ecbd980b1f191c092dce1b072b3cb9bd8984/README.md)、[明末渲染进程线索](https://github.com/cursey/safetyhook/issues/106)。这些资料不构成本 Fork 的实机验收。
5. 选择显卡配置和 **2×上限**。首轮保留 PTX、精确模式；40HX 单列实验档，不把型号伪装识别结果当成硬件能力证明。
6. 在后端下拉框选择版本（默认 0.2.3，可选 0.2.4），应用时会按所选提交下载并验证。代理入口先用 `version.dll`。本地 DLL 留空时，点击应用会从上游**固定提交**下载对应文件并核验；也可先下载该提交文件，再用本地 DLL 选项导入。
7. 阅读警告，勾选已退出游戏/单机实验确认，点击 **“解锁 / 应用帧生成配置”**。下载可取消；写入时不能关闭窗口。成功提示只表示文件安装和配置写入完成。
8. 启动游戏，使用 D3D12，在图形/显示设置启用 DLSS 帧生成。若有 2×/3×/4×选项，再选择不超过上限的倍率。游戏未提供该菜单时，不要把工具选了 4×当作实际运行 4×。
9. 检查画面、流畅度、输入响应、显存和日志。失败时按下方指引恢复。

## 推荐配置与性能验收

| 设备 | 后端配置起点 | 首轮测试建议 |
|---|---|---|
| RTX 3080（10GB/12GB 以实卡为准） | `Router=SM86`、`KernelImage=PTX`、`HardwareBilinear=0`、2× | 先在固定场景关闭 FG 测基础帧率；2×稳定后再试 4×；4K 注意显存余量 |
| CMP 40HX 解锁版 | `Router=SM75`、`KernelImage=PTX`、`HardwareBilinear=0`、2× | 先确认设备管理器无错误、驱动能提供 NGX/CUDA；1080p、关闭光追开始，不宣称与普通 2060 SUPER 完全等效 |

20 系列/SM75 的物理显卡验证在上游仍不足；3080 Ti 上跑 SM75 PTX 不等于真实 40HX 已验证。若 40HX 有 Code 43 或驱动无法初始化，本工具无法解决。

`HardwareBilinear=1` 仅用于 SM86 近似采样，可能改变生成帧像素，默认不推荐；SM75 禁用此选项。首版日志为 `Level=2`，方便测试，长期使用可在完成测试并卸载本工具托管后手动部署 `Level=1`；手动改动托管 INI 会触发恢复保护。

两款游戏分别在 **相同场景、分辨率、DLSS 超分版本/档位、贴图与光追设置** 测：FG 关闭 → 2× → 4×（游戏实际支持时）。首次 PTX JIT/着色器编译可能有冷启动开销，先预热再记录。建议固定场景记录约 60 秒，再实际游玩并测试过场/切场景。

| 游戏 / 游戏版本 | GPU / 显存 / 驱动 | 输出分辨率 / 超分档位 | FG 关闭 FPS | 2×实际倍率 / FPS | 4×实际倍率 / FPS | 显存峰值 / 卡顿 / UI 闪烁 / 崩溃 |
|---|---|---|---|---|---|---|
| 黑神话：悟空 | RTX 3080 / 待填 | 待填 | 待测 | 待测 | 待测 | 待测 |
| 黑神话：悟空 | CMP 40HX / 待填 | 待填 | 待测 | 待测 | 待测 | 待测 |
| 明末：渊虚之羽 | RTX 3080 / 待填 | 待填 | 待测 | 待测 | 待测 | 待测 |
| 明末：渊虚之羽 | CMP 40HX / 待填 | 待填 | 待测 | 待测 | 待测 | 待测 |

把渲染 FPS 与包含生成帧的显示 FPS 分开，注明计数器来源；一个 FPS 数字或日志文件存在不能确认真实倍率、帧节奏或响应改善。4×不是四倍操作响应。显存紧张时，降低倍率未必大幅降低开销，还应调整输出分辨率、贴图或光追。

## 自动解锁失败：手动操作

### 没有日志 / 没有加载

- 检查真实渲染 EXE 目录，而不是启动器目录。日志应在 EXE 旁 `dlssg_sm86\logs\native_<PID>.jsonl`；注意时间戳，旧日志不是新安装的证据。
- 点击本工具“卸载并恢复原件”，再尝试 `winmm.dll`、`dinput8.dll`、`winhttp.dll` 或 `dxgi.dll` 入口。选择的是上游对应名称的**独立文件**；不能直接给 `version.dll` 改名。
- 同一游戏目录只放本后端的一个代理。不要覆盖 ReShade、OptiScaler、RenoDX 等已有文件；上游旧版或手动安装的 Mod 需先备份并移走，不能与本版混装。
- 手动获取路径：[Native 0.2.3](https://github.com/sdli1995/dlssg_for_sm86/tree/1fb9ecbd980b1f191c092dce1b072b3cb9bd8984) / [Native 0.2.4](https://github.com/sdli1995/dlssg_for_sm86/tree/5f62ff44a9c08f9841fa605e7b7160f79ccd2c40)。下载所选版本对应的同名 DLL；本地导入也会验证，不能混用版本或重命名代理。

### FG 选项灰色 / 无倍率 / 有日志但不插帧

- 确认 D3D12、驱动正常和游戏实际使用的 GPU。后端接入还取决于游戏提供的矩阵、资源与调用流程。
- 查看上游对应游戏 Issues；部分游戏即使 DLL 已加载，仍会在自身设置层限制显卡或不请求多帧。首版不包含额外显卡伪装、Streamline 替换或游戏专用 hook。
- 先回退 2×。**`MaxGeneratedFrames=3` 只是 4×上限**；不能通过随意添加未知 INI 键强制游戏请求多帧。
- 记录游戏/驱动/Mod 版本、配置、错误日志；不要默认关闭安全软件或绕过反作弊。文件核验只证明来源版本一致，不证明绝对安全或兼容。

### 崩溃 / 卡顿 / 显存不足

完全退出游戏，先卸载本工具的 FG 后端，验证原游戏能正常运行；随后在较低输出分辨率/光追设置下试 2×。保留日志与出问题的场景信息。游戏更新后先恢复旧修改，再针对新版本重新安装，避免旧代理混用。

### 一键恢复与手动恢复

帧生成窗口点击 **卸载并恢复原件**：移除本工具安装且内容未被外部修改的代理；有原 INI 则恢复，无原 INI 则移除本工具新增 INI。不会删除游戏原有 `nvngx_dlssg.dll`，不会重置 Swapper 独立替换的 DLSS DLL；后者仍通过原界面的 Reset 恢复。

若自动恢复因文件改动或备份异常而拒绝：

1. 退出游戏，把 EXE 旁 `.dlss-swapper-fg` 文件夹及当前代理 DLL/INI 复制到安全位置。
2. 读取 `.dlss-swapper-fg\state.json` 的 `Proxy`，确认本工具安装的是哪一个代理；将它移到备份目录，不动其他 Mod。
3. 有 `.dlss-swapper-fg\original.ini` 时将其复制回 `dlssg_sm86.ini`。没有原件时，移走本工具新建的 INI；保留自己的手动修改备份。
4. `.fg-stage` 是中断写入时可能留下的暂存文件，确认归属后单独移走，不批量删除游戏 DLL。
5. 验证原游戏恢复后，将 `.dlss-swapper-fg` 记录目录移到备份位置，之后可重新安装。

程序退出/断电可能留下安装记录，记录会先于游戏文件写入。不要删除记录后直接覆盖安装；先恢复。安装失败若未修改游戏文件也可能留下空锁文件，它不属于代理 DLL。

## 后端来源与许可证边界

- Swapper 基于上游提交 `61752b6479e676d7176ada2566f3569bedc4b002`，沿用仓库 [GPLv3 LICENSE](LICENSE)。原说明保留在 [README.upstream.md](docs/README.upstream.md)。
- FG 后端：[sdli1995/dlssg_for_sm86](https://github.com/sdli1995/dlssg_for_sm86)，固定提交 `1fb9ecbd980b1f191c092dce1b072b3cb9bd8984`，默认 Native 0.2.3 / 内置模型 310.1；可选 Native 0.2.4 固定为 `5f62ff44a9c08f9841fa605e7b7160f79ccd2c40`。
- 本仓库与便携包**不捆绑或重新发布 FG DLL、NVIDIA 模型/内核资源**。用户主动应用时才从上游获取；也可以导入固定版本的本地 DLL。下载文件按已核对的 SHA-256 验证，不自动追随上游变化。
- 当前后端公开仓库主要是 DLL/文档，缺少其 C++ 源码与构建工程。[第三方声明](https://github.com/sdli1995/dlssg_for_sm86/blob/1fb9ecbd980b1f191c092dce1b072b3cb9bd8984/THIRD_PARTY_NOTICES.txt)提及 GPLv3 源码和单独的 NVIDIA 资源许可；这不代表可自由重分发所有二进制资源。后续打包分发后端或修改其内部实现，需要先厘清授权与源码可用性。
- 内置后端模型版本与 Swapper 替换的官方 DLSSG DLL 版本是两回事；后端原生推理不会因单独更新官方 DLL 自动升级其模型。

## 构建与验证

Windows 上安装 .NET 10 SDK 后：

```powershell
dotnet run --project tests/FrameGeneration/FrameGeneration.Tests.csproj
dotnet publish "src/DLSS Swapper.csproj" --runtime win-x64 --self-contained --configuration Release_Portable -p:PublishDir=bin/publish/portable/
```

验证程序用临时模拟文件检查安装、重复应用、原件恢复、第三方冲突、外部修改、文件校验失败和中断恢复。它不会加载后端 DLL，不是 GPU 测试。Windows Actions 负责构建完整 WinUI 便携包。真实 Windows UI、两款游戏及两张显卡的运行性能以使用者实测为准。
