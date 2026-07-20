Claude 桌面版 · 中文补丁（macOS / Apple Silicon M4 Mini）
====================================================

一、这是什么？
  绿色免安装工具。在 Mac 上运行后，自动把简体中文语言包
  写入本机 Claude.app。不覆盖英文/日文等其他语言。

  适合：Mac mini M4 / M4 Pro、M1/M2/M3、Intel Mac
  系统：macOS 12 及以上（建议 14/15）

二、文件夹里有什么？
  一键汉化-Mac.command     ← 双击这个运行
  install_mac.py           ← 安装逻辑（自动调用）
  translation-memory.json  ← 翻译缓存（必须在一起，约 1.9MB）
  使用说明-Mac-M4.txt      ← 本说明

  分享给别人时：把整个文件夹打包发送（zip 即可）。

三、怎么用？（M4 Mini）
  1. 从官网安装 Claude 桌面版到「应用程序」
     https://claude.ai/download
  2. 完全退出 Claude（Dock 右键 → 退出）
  3. 把本文件夹拷到 Mac（桌面即可）
  4. 双击「一键汉化-Mac.command」
     - 若提示「无法打开 / 来自未识别开发者」：
       系统设置 → 隐私与安全性 → 仍要打开
       或：终端执行：
         cd 到本文件夹
         xattr -dr com.apple.quarantine .
         chmod +x 一键汉化-Mac.command
         ./一键汉化-Mac.command
  5. 若提示输入密码：输入本机开机密码（写入 /Applications 需要）
  6. 看到「汉化完成」后，重新打开 Claude

四、如果语言包在 app.asar 里面？
  部分 Claude 版本把语言文件打进 app.asar。
  这时脚本需要 Node.js 才能安全解包/重打包：
    1. 安装 Node.js LTS（ARM64）：https://nodejs.org
    2. 再双击「一键汉化-Mac.command」
  首次 npx 可能短暂联网下载 asar 工具。

五、汉化后仍是英文？
  1. 确认已完全退出再打开 Claude
  2. 系统语言设为简体中文，或在 Claude 设置里选中文
  3. Claude 自动更新后会丢补丁 → 再运行一次本工具即可
  4. 把 install-mac.log 发给制作者

六、安全说明
  - 只新增 zh-CN，不删其他语言
  - 会修改 Claude.app（必要时重签名 ad-hoc）
  - 不强制联网（仅 asar 路径首次 npx 可能联网）
  - 翻译来自内置缓存，不是在线即时翻译

七、与 Windows 版关系
  Windows 用 exe；Mac 用本文件夹。
  翻译缓存相同，两边可分别汉化。
