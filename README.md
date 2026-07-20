# Claude Desktop 简体中文补丁

为 [Claude 桌面版](https://claude.ai/download) 增加 **zh-CN** 简体中文界面。

- 只**新增**中文语言包，不覆盖 `en-US` / `ja-JP` 等现有语言
- **绿色免安装**：运行即打补丁
- 支持 **Windows** 与 **macOS（含 Apple Silicon M4）**
- 翻译缓存内置 / 随包提供，无需 Python 机器学习环境，也无需在线翻译

> 非官方第三方工具。Claude 更新后界面可能恢复英文，再运行一次即可。

---

## 目录结构

```
claude-desktop-zh-cn/
├── windows/                 # Windows 补丁
│   ├── ClaudeZhPatch.exe    # 一键 GUI 补丁（推荐，内置翻译缓存）
│   ├── Program.cs           # 源码
│   ├── app.manifest         # 请求管理员权限
│   ├── build.ps1 / csc.rsp  # 本地编译
│   ├── install-zh-cn-no-python.ps1
│   └── 一键安装中文-免Python.bat
├── mac/                     # macOS 补丁
│   ├── 一键汉化-Mac.command
│   ├── install_mac.py
│   └── translation-memory.json
├── shared/
│   └── translation-memory.json   # EN→ZH 翻译缓存（约 1.9 万条）
└── README.md
```

---

## Windows 用法（推荐）

1. 安装 [Claude Desktop](https://claude.ai/download)
2. **完全退出** Claude（托盘图标也要退出；任务管理器无 `Claude.exe`）
3. 右键 [`windows/ClaudeZhPatch.exe`](windows/ClaudeZhPatch.exe) → **以管理员身份运行**
4. UAC 点「是」→ 点「一键打补丁（汉化）」
5. 重新打开 Claude

只需这一个 exe 即可分享给别人（翻译缓存已内置，约 1.9MB）。

### 没有权限写入？

- 必须**右键管理员运行**，UAC 点「是」
- 确认 Claude 已完全退出
- 暂时关闭杀毒 / Defender 实时保护后再试
- 公司电脑若锁定 `WindowsApps`，需要管理员策略放行

### 可选：PowerShell 脚本版

若不想用 exe，可把 `shared/translation-memory.json` 与  
`windows/install-zh-cn-no-python.ps1`、`windows/一键安装中文-免Python.bat` 放同一目录，  
右键 bat → 以管理员运行。

### 从源码编译

需要本机 .NET Framework 4.x（Win10/11 自带）：

```powershell
cd windows
# 确保 ..\shared\translation-memory.json 存在，或改 csc.rsp 中路径
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

---

## macOS 用法（M4 Mini / M1–M3 / Intel）

1. 安装 Claude 到「应用程序」
2. 完全退出 Claude
3. 打开 `mac` 文件夹，双击 **`一键汉化-Mac.command`**
4. 若提示未识别开发者：系统设置 → 隐私与安全性 → 仍要打开  
   或在终端执行：
   ```bash
   cd mac
   xattr -dr com.apple.quarantine .
   chmod +x 一键汉化-Mac.command install_mac.py
   ./一键汉化-Mac.command
   ```
5. 需要时输入开机密码
6. 看到「汉化完成」后重新打开 Claude

### 若语言文件在 `app.asar` 内

部分版本需要 [Node.js LTS](https://nodejs.org)（Apple Silicon 选 **ARM64**）才能安全解包/重打包。  
安装 Node 后重新运行脚本即可。

---

## 原理（简要）

1. 定位 Claude 安装目录  
   - Windows：`C:\Program Files\WindowsApps\Claude_*\app\resources`  
   - macOS：`/Applications/Claude.app/Contents/Resources`（或 `app.asar`）
2. 读取 `en-US.json`（shell / ion / dynamic）
3. 用 `translation-memory.json` 生成对应 `zh-CN.json`
4. 补丁前端 JS 语言白名单：加入 `zh-CN` / langmap / persona
5. Windows 会尝试 `takeown`/`icacls` 修复写入权限；macOS 修改后 ad-hoc `codesign`

---

## 注意事项

- 仅供学习交流；修改客户端可能影响自动更新或完整性校验
- 请遵守 Anthropic 服务条款与当地法律
- 本工具不收集个人信息，默认不联网（macOS 走 asar 且首次 `npx asar` 时可能短暂联网）

---

## License

MIT
