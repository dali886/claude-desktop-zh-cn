#!/bin/bash
# Double-click to run Claude zh-CN patcher on macOS (M1/M2/M3/M4 + Intel)
cd "$(dirname "$0")" || exit 1
export LANG=zh_CN.UTF-8
export LC_ALL=zh_CN.UTF-8

echo "=========================================="
echo "  Claude 桌面版 · 中文补丁 (macOS)"
echo "  适合 Apple Silicon M4 Mini"
echo "=========================================="
echo ""

if ! command -v python3 >/dev/null 2>&1; then
  echo "未找到 python3。"
  echo "请打开「终端」执行："
  echo "  xcode-select --install"
  echo ""
  read -r -p "按回车键关闭…" _
  exit 1
fi

if [[ ! -f "./translation-memory.json" ]]; then
  echo "缺少 translation-memory.json"
  echo "请保证本文件夹完整（脚本 + translation-memory.json）"
  echo ""
  read -r -p "按回车键关闭…" _
  exit 1
fi

if [[ ! -f "./install_mac.py" ]]; then
  echo "缺少 install_mac.py"
  read -r -p "按回车键关闭…" _
  exit 1
fi

# remove quarantine so double-click works
xattr -dr com.apple.quarantine . >/dev/null 2>&1 || true

python3 ./install_mac.py
code=$?
echo ""
if [[ $code -eq 0 ]]; then
  echo "完成。可以关闭本窗口，然后打开 Claude。"
else
  echo "未成功。请把 install-mac.log 发给制作者。"
fi
echo ""
read -r -p "按回车键关闭…" _
exit $code
