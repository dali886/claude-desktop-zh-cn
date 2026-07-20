#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Claude Desktop zh-CN patcher for macOS (Apple Silicon M1–M4 / Intel).

Portable: put this file next to translation-memory.json and run.
No pip packages required. System python3 only.
"""
from __future__ import print_function

import json
import os
import re
import shutil
import struct
import subprocess
import sys
import time
from pathlib import Path

ENGINE = "mac-portable-v1"
SCRIPT_DIR = Path(__file__).resolve().parent
MEM_PATH = SCRIPT_DIR / "translation-memory.json"
LOG_PATH = SCRIPT_DIR / "install-mac.log"
STATE_PATH = SCRIPT_DIR / "install-state-mac.json"

OLD_LOCALES = '["en-US","de-DE","fr-FR","ko-KR","ja-JP","es-419","es-ES","it-IT","hi-IN","pt-BR","id-ID"]'
NEW_LOCALES = '["en-US","de-DE","fr-FR","ko-KR","ja-JP","es-419","es-ES","it-IT","hi-IN","pt-BR","id-ID","zh-CN"]'
OLD_MAP = '{"en-US":"en","de-DE":"de","fr-FR":"fr","ko-KR":"ko","ja-JP":"ja","es-419":"es","es-ES":"es","it-IT":"it","hi-IN":"en","pt-BR":"pt_BR","id-ID":"id"}'
NEW_MAP = '{"en-US":"en","de-DE":"de","fr-FR":"fr","ko-KR":"ko","ja-JP":"ja","es-419":"es","es-ES":"es","it-IT":"it","hi-IN":"en","pt-BR":"pt_BR","id-ID":"id","zh-CN":"zh_CN"}'
OLD_PERSONA = 'case"ja-JP":return["language","ja"];case"es-419"'
NEW_PERSONA = 'case"ja-JP":return["language","ja"];case"zh-CN":return["language","zh"];case"es-419"'


def log(msg):
    line = time.strftime("[%H:%M:%S] ") + str(msg)
    print(line)
    try:
        with LOG_PATH.open("a", encoding="utf-8") as f:
            f.write(line + "\n")
    except Exception:
        pass


def die(msg):
    log("ERROR: " + str(msg))
    print("")
    print("失败: " + str(msg))
    print("日志: " + str(LOG_PATH))
    sys.exit(1)


def run(cmd, check=False, capture=True):
    try:
        p = subprocess.run(
            cmd,
            stdout=subprocess.PIPE if capture else None,
            stderr=subprocess.STDOUT if capture else None,
            universal_newlines=True,
        )
        out = (p.stdout or "").strip() if capture else ""
        if check and p.returncode != 0:
            raise RuntimeError("cmd failed %s: %s" % (cmd, out))
        return p.returncode, out
    except FileNotFoundError:
        return 127, ""


def quit_claude():
    log("退出 Claude…")
    run(["osascript", "-e", 'tell application "Claude" to quit'])
    time.sleep(1)
    run(["pkill", "-x", "Claude"])
    time.sleep(1)


def find_claude_app():
    candidates = [
        Path("/Applications/Claude.app"),
        Path.home() / "Applications" / "Claude.app",
    ]
    for c in candidates:
        if c.is_dir():
            return c
    code, out = run(
        ["mdfind", 'kMDItemCFBundleIdentifier == "com.anthropic.claudefordesktop"']
    )
    if code == 0 and out:
        p = Path(out.splitlines()[0].strip())
        if p.is_dir():
            return p
    code, out = run(["mdfind", 'kMDItemDisplayName == "Claude.app"'])
    if code == 0 and out:
        p = Path(out.splitlines()[0].strip())
        if p.is_dir():
            return p
    return None


def find_resources_root(claude_app):
    """Return (app_res Path, mode) where mode is 'loose' or path to asar."""
    resources = claude_app / "Contents" / "Resources"
    if not resources.is_dir():
        die("找不到 Contents/Resources: %s" % resources)

    for try_dir in (
        resources / "app" / "resources",
        resources / "resources",
        resources,
    ):
        if (try_dir / "en-US.json").is_file():
            return try_dir, "loose"

    # search shallow
    for p in resources.rglob("en-US.json"):
        # skip huge trees if any
        parent = p.parent
        # prefer one that also has ion-dist nearby
        if (parent / "ion-dist" / "i18n" / "en-US.json").is_file() or (
            parent.parent / "ion-dist" / "i18n" / "en-US.json"
        ).is_file():
            if (parent / "ion-dist").is_dir():
                return parent, "loose"
            if (parent.parent / "ion-dist").is_dir():
                return parent.parent, "loose"
        # shell-only
        if p.name == "en-US.json" and parent.name in ("resources", "Resources"):
            return parent, "loose"

    asar = resources / "app.asar"
    if asar.is_file():
        return asar, "asar"
    die("找不到 en-US.json / app.asar，Claude 安装可能不完整。")


def load_json(path):
    with Path(path).open("r", encoding="utf-8") as f:
        return json.load(f)


def write_json(path, obj):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    items = list(obj.items())
    lines = ["{"]
    for i, (k, v) in enumerate(items):
        ks = json.dumps(str(k), ensure_ascii=False)
        vs = json.dumps("" if v is None else str(v), ensure_ascii=False)
        comma = "," if i < len(items) - 1 else ""
        lines.append("  %s: %s%s" % (ks, vs, comma))
    lines.append("}")
    text = "\n".join(lines) + "\n"
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(text, encoding="utf-8")
    tmp.replace(path)


def build_zh(en_dict, memory, label):
    out = {}
    hit = miss = 0
    for k, v in en_dict.items():
        en = "" if v is None else str(v)
        if en in memory:
            out[str(k)] = str(memory[en])
            hit += 1
        else:
            out[str(k)] = en
            miss += 1
    log("[%s] keys=%d hit=%d miss=%d" % (label, len(out), hit, miss))
    return out, hit, miss


def patch_js_in_dir(root, bak_dir):
    patched = 0
    root = Path(root)
    if not root.is_dir():
        return 0
    bak_dir = Path(bak_dir)
    bak_dir.mkdir(parents=True, exist_ok=True)

    # Prefer assets/v1; else scan limited
    candidates = []
    assets = root / "ion-dist" / "assets" / "v1"
    if assets.is_dir():
        candidates = list(assets.glob("*.js"))
    else:
        for js in root.rglob("*.js"):
            try:
                if js.stat().st_size > 8 * 1024 * 1024:
                    continue
            except Exception:
                continue
            candidates.append(js)

    for js in candidates:
        try:
            size = js.stat().st_size
            if size < 500 or size > 50 * 1024 * 1024:
                continue
            text = js.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue
        if "en-US" not in text:
            continue
        if not any(x in text for x in ("de-DE", "ja-JP", "pt-BR")):
            continue
        # if none of our patterns, skip
        if OLD_LOCALES not in text and OLD_MAP not in text and OLD_PERSONA not in text:
            # still allow already partial
            if "zh-CN" in text:
                continue
            continue
        orig = text
        if OLD_LOCALES in text and NEW_LOCALES not in text:
            text = text.replace(OLD_LOCALES, NEW_LOCALES)
        if OLD_MAP in text and '"zh-CN":"zh_CN"' not in text:
            text = text.replace(OLD_MAP, NEW_MAP)
        if OLD_PERSONA in text and 'case"zh-CN":return["language","zh"]' not in text:
            text = text.replace(OLD_PERSONA, NEW_PERSONA)
        if text != orig:
            bak = bak_dir / js.name
            if not bak.exists():
                try:
                    shutil.copy2(js, bak)
                except Exception:
                    pass
            js.write_text(text, encoding="utf-8")
            log("Patched JS: %s" % js.name)
            patched += 1
    return patched


# ---- asar helpers (minimal) ----

def asar_read_header(asar_path):
    with open(asar_path, "rb") as f:
        header_size = struct.unpack("<I", f.read(4))[0]
        pickle = f.read(header_size)
        json_size = struct.unpack("<I", pickle[:4])[0]
        header = json.loads(pickle[4 : 4 + json_size].decode("utf-8"))
        base = 4 + header_size
        return header, base


def asar_walk(node, prefix=""):
    files = node.get("files")
    if files is None:
        yield prefix, node
        return
    for name, child in files.items():
        p = "%s/%s" % (prefix, name) if prefix else name
        for x in asar_walk(child, p):
            yield x


def asar_extract_selected(asar_path, out_dir):
    """Extract locale-related and app/resources files from asar."""
    header, base = asar_read_header(asar_path)
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    n = 0
    with open(asar_path, "rb") as f:
        for path, node in asar_walk(header):
            if "offset" not in node or "size" not in node:
                continue
            low = path.lower()
            keep = False
            if low.endswith(".json") and (
                "en-us" in low
                or "zh-cn" in low
                or "/i18n/" in low
                or low.endswith("en-us.json")
            ):
                keep = True
            if "/assets/v1/" in low and low.endswith(".js"):
                keep = True
            if low.startswith("app/resources/") or low.startswith("resources/"):
                if low.endswith(".json") or "/assets/v1/" in low:
                    keep = True
            if not keep:
                continue
            try:
                off = int(node["offset"])
                size = int(node["size"])
            except Exception:
                continue
            f.seek(base + off)
            data = f.read(size)
            dest = out_dir / path
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(data)
            n += 1
    return n


def asar_pack_with_npx(src_dir, out_asar):
    # requires node/npx
    code, out = run(["npx", "--yes", "asar", "pack", str(src_dir), str(out_asar)])
    if code != 0:
        raise RuntimeError("npx asar pack failed: %s" % out)


def asar_extract_full_npx(asar_path, out_dir):
    code, out = run(["npx", "--yes", "asar", "extract", str(asar_path), str(out_dir)])
    if code != 0:
        raise RuntimeError("npx asar extract failed: %s" % out)


def ensure_writable_dir(path):
    path = Path(path)
    if os.access(str(path), os.W_OK):
        return True
    return False


def sudo_copy(src, dst):
    code, out = run(["sudo", "cp", "-f", str(src), str(dst)])
    if code != 0:
        die("sudo 复制失败: %s -> %s\n%s" % (src, dst, out))


def codesign_app(app):
    log("ad-hoc 签名 Claude.app（M 系列 Mac 修改后常需要）…")
    # deep force sign
    cmd = ["codesign", "--force", "--deep", "--sign", "-", str(app)]
    if not ensure_writable_dir(app):
        cmd = ["sudo"] + cmd
    code, out = run(cmd)
    if code != 0:
        log("codesign 警告: %s" % out)
    run(["xattr", "-dr", "com.apple.quarantine", str(app)])


def resolve_app_res_from_extract(work):
    work = Path(work)
    for cand in (
        work / "app" / "resources",
        work / "resources",
        work,
    ):
        if (cand / "en-US.json").is_file():
            return cand
    found = list(work.rglob("en-US.json"))
    if not found:
        return None
    # pick one with ion-dist sibling if possible
    for p in found:
        parent = p.parent
        if (parent / "ion-dist" / "i18n" / "en-US.json").is_file():
            return parent
    return found[0].parent


def main():
    if LOG_PATH.exists():
        LOG_PATH.write_text("", encoding="utf-8")

    print("==========================================")
    print("  Claude 桌面版 · 中文补丁 (macOS)")
    print("  引擎: %s" % ENGINE)
    print("  适合 Apple Silicon (M1/M2/M3/M4) 与 Intel")
    print("==========================================")
    print("")

    log("Engine: %s" % ENGINE)
    log("ScriptDir: %s" % SCRIPT_DIR)
    log("Arch: %s" % os.uname().machine)
    code, ver = run(["sw_vers", "-productVersion"])
    log("macOS: %s" % ver)
    log("python: %s" % sys.version.replace("\n", " "))

    if not MEM_PATH.is_file():
        die("缺少 translation-memory.json（请与本脚本放同一文件夹）")

    memory = load_json(MEM_PATH)
    if not isinstance(memory, dict) or len(memory) < 100:
        die("translation-memory.json 异常（条目过少）")
    log("Memory entries: %d" % len(memory))

    quit_claude()

    claude_app = find_claude_app()
    if not claude_app:
        die("未找到 Claude.app。请先安装到「应用程序」文件夹。")
    log("Claude.app: %s" % claude_app)

    target, mode = find_resources_root(claude_app)
    log("资源定位: mode=%s path=%s" % (mode, target))

    work = None
    asar_path = None
    app_res = None

    if mode == "loose":
        app_res = Path(target)
    else:
        asar_path = Path(target)
        work = SCRIPT_DIR / (".asar-work-%d" % os.getpid())
        if work.exists():
            shutil.rmtree(work, ignore_errors=True)
        work.mkdir(parents=True, exist_ok=True)

        # Prefer full extract via npx for safe repack
        has_npx = run(["bash", "-lc", "command -v npx"])[0] == 0
        if has_npx:
            log("使用 npx asar 完整解包（首次可能需联网下载 asar 工具）…")
            try:
                asar_extract_full_npx(asar_path, work)
            except Exception as e:
                log("npx 解包失败，回退精简解包: %s" % e)
                n = asar_extract_selected(asar_path, work)
                log("精简解包文件数: %d" % n)
                if n < 3:
                    die("从 app.asar 解包失败。请安装 Node.js 后重试。")
                die(
                    "当前仅解出部分文件，无法安全重打包。\n"
                    "请安装 Node.js（https://nodejs.org）后重新运行本工具。"
                )
        else:
            n = asar_extract_selected(asar_path, work)
            log("精简解包文件数: %d" % n)
            die(
                "Claude 语言包在 app.asar 内，需要 Node.js 才能安全写入。\n"
                "请安装 Node.js（https://nodejs.org ，LTS 即可）后重新双击运行。\n"
                "（M4 Mini 用 ARM64 官方安装包）"
            )

        app_res = resolve_app_res_from_extract(work)
        if not app_res:
            die("解包后找不到 en-US.json")
        log("解包资源目录: %s" % app_res)

    shell_en = app_res / "en-US.json"
    ion_en = app_res / "ion-dist" / "i18n" / "en-US.json"
    dyn_en = app_res / "ion-dist" / "i18n" / "dynamic" / "en-US.json"

    if not ion_en.is_file():
        # search
        found = list(Path(app_res).rglob("ion-dist/i18n/en-US.json"))
        if not found and work:
            found = list(Path(work).rglob("ion-dist/i18n/en-US.json"))
        if found:
            ion_en = found[0]
            # if shell is missing at app_res, try parent of ion-dist
            maybe = ion_en.parent.parent.parent  # ion-dist/i18n/en-US -> resources?
            # ion-dist/i18n/file -> parent=i18n, parent=ion-dist, parent=resources
            maybe = ion_en.parents[2]
            if (maybe / "en-US.json").is_file():
                app_res = maybe
                shell_en = app_res / "en-US.json"
                dyn_en = app_res / "ion-dist" / "i18n" / "dynamic" / "en-US.json"
                log("重定位 app_res=%s" % app_res)

    if not shell_en.is_file():
        die("缺少 shell en-US.json: %s" % shell_en)
    if not ion_en.is_file():
        die("缺少 ion en-US.json: %s" % ion_en)

    log("读取英文语言包…")
    shell = load_json(shell_en)
    ion = load_json(ion_en)
    dyn = load_json(dyn_en) if dyn_en.is_file() else {}
    log("shell=%d ion=%d dynamic=%d" % (len(shell), len(ion), len(dyn)))
    if len(shell) < 10 or len(ion) < 100:
        die("英文语言包解析失败")

    shell_zh, h1, m1 = build_zh(shell, memory, "shell")
    dyn_zh, h2, m2 = build_zh(dyn, memory, "dynamic") if dyn else ({}, 0, 0)
    ion_zh, h3, m3 = build_zh(ion, memory, "ion")
    total_hit = h1 + h2 + h3
    total_miss = m1 + m2 + m3
    if total_hit < 100:
        die("命中过少 hit=%d，已中止" % total_hit)

    log("写入 zh-CN…")
    shell_dst = app_res / "zh-CN.json"
    ion_dst = app_res / "ion-dist" / "i18n" / "zh-CN.json"
    ovr_dst = app_res / "ion-dist" / "i18n" / "zh-CN.overrides.json"
    dyn_dst = app_res / "ion-dist" / "i18n" / "dynamic" / "zh-CN.json"

    # if loose mode and not writable, copy to temp then sudo
    writable = ensure_writable_dir(app_res)
    if mode == "loose" and not writable:
        log("无写权限，将使用 sudo 写入（需输入开机密码）…")

    def put_file(dst, writer):
        dst = Path(dst)
        if mode == "loose" and not writable:
            tmp = SCRIPT_DIR / (".write-" + dst.name)
            writer(tmp)
            # ensure parent
            run(["sudo", "mkdir", "-p", str(dst.parent)])
            sudo_copy(tmp, dst)
            try:
                tmp.unlink()
            except Exception:
                pass
        else:
            writer(dst)

    def write_empty_obj(p):
        Path(p).parent.mkdir(parents=True, exist_ok=True)
        Path(p).write_text("{}\n", encoding="utf-8")

    put_file(shell_dst, lambda p: write_json(p, shell_zh))
    put_file(ion_dst, lambda p: write_json(p, ion_zh))
    put_file(ovr_dst, write_empty_obj)
    if dyn:
        put_file(dyn_dst, lambda p: write_json(p, dyn_zh))

    bak_dir = (
        Path.home()
        / "Library"
        / "Application Support"
        / "Claude-zh-translate"
        / "patched-js-backups"
    )
    patched = patch_js_in_dir(app_res, bak_dir)
    log("Patched JS count: %d" % patched)

    if mode == "asar":
        # repack
        log("重新打包 app.asar…")
        new_asar = SCRIPT_DIR / "app.asar.new"
        try:
            asar_pack_with_npx(work, new_asar)
        except Exception as e:
            die("打包 app.asar 失败: %s\n请确认已安装 Node.js" % e)
        bak = asar_path.with_suffix(".asar.bak-zhcn")
        log("替换 app.asar（可能需要密码）…")
        if ensure_writable_dir(asar_path.parent):
            if not bak.exists():
                shutil.copy2(asar_path, bak)
            shutil.copy2(new_asar, asar_path)
        else:
            if not bak.exists():
                sudo_copy(asar_path, bak)
            sudo_copy(new_asar, asar_path)
        try:
            new_asar.unlink()
        except Exception:
            pass
        log("app.asar 已更新，备份: %s" % bak)

    codesign_app(claude_app)

    state = {
        "installedAt": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "mode": "add-zh-CN-pack",
        "locale": "zh-CN",
        "engine": ENGINE,
        "claudeApp": str(claude_app),
        "resourceMode": mode,
        "appRes": str(app_res),
        "arch": os.uname().machine,
        "shell": len(shell_zh),
        "ion": len(ion_zh),
        "dynamic": len(dyn_zh),
        "cacheHits": total_hit,
        "cacheMisses": total_miss,
        "patchedJs": patched,
    }
    STATE_PATH.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")

    if work and work.exists():
        shutil.rmtree(work, ignore_errors=True)

    print("")
    print("==========================================")
    print("  汉化完成！")
    print(
        "  shell %d / ion %d / dynamic %d"
        % (len(shell_zh), len(ion_zh), len(dyn_zh))
    )
    print("  命中 %d，未命中 %d，JS补丁 %d" % (total_hit, total_miss, patched))
    print("  请重新打开 Claude。")
    print("==========================================")
    print("日志: %s" % LOG_PATH)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("\n已取消")
        sys.exit(130)
    except SystemExit:
        raise
    except Exception as e:
        die(str(e))
