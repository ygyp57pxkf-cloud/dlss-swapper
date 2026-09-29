本版新增可选的最新帧生成后端 Proxy 0.3.5（内嵌 DLSSG 310.9.1），并将其设为新安装的默认选择。40HX 首次测试采用 SM75、PTX、原厂数值档位 `Optimized=0` 和 2×上限；优化档位 1、5× / 6×上限由用户明确选择，实际倍率仍取决于游戏。旧 Native 0.2.3 / 0.2.4 保留，可通过原有安装记录切换和恢复。

修复了本 Fork 的预发布版更新检查：从 Releases 列表查找 `fg-preview-*`，按标签比较版本，并纠正已提醒版本的比较方向。日志诊断增加 Proxy 的 loader/backend 文件；超过 8 MiB 的日志读取末尾，能看到游戏运行一段时间后出现的错误。

可选外部工具版本更新到 DLSS5-Swapper 2.2.9 和 Visual Enhancer 13.2。两者仅在用户操作时下载，不包含在本便携 ZIP 中；Visual Enhancer 13.2 改为桌面应用入口。

**安装：** 下载下方 `DLSS-Swapper-FG-Preview-0.4-win-x64.zip`，完整解压到新的可写目录，运行 `DLSS Swapper.exe`。两版程序都退出后，可复制旧版 `StoredData-FG-Preview`；保留旧目录便于回退。游戏内由本工具托管的 DLL/INI 和 `.dlss-swapper-fg` 记录不要手动删除或覆盖。详细步骤见 ZIP 内 `README-FG-Preview.md` 和 `Enhancement-Tools.md`。

Windows 核心测试、x64 构建和 ZIP 结构由本发布对应的 GitHub Actions 完成。后端 DLL 来源及哈希已核对，但 CI 不加载后端，也不能代替另一台 CMP 40HX 上的《黑神话：悟空》实测。0.3.5 修复的是上游 0.3.x 的重建后随机崩溃；若关闭 FG 正常、开启后仍报驱动崩溃，请保存该次 `loader_<PID>.jsonl` / `backend_<PID>.jsonl`、游戏版本、驱动版本、分辨率、光追及倍率，用 2×、关闭光追再做单变量对照。
