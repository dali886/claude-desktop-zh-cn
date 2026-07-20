// Claude Desktop Chinese Patch - portable single-file tool (no install)
// Compile with build.bat next to this file.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ClaudeZhPatch
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsAdmin())
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        UseShellExecute = true,
                        Verb = "runas",
                        WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                    };
                    Process.Start(psi);
                }
                catch
                {
                    MessageBox.Show(
                        "需要管理员权限才能写入 Claude 安装目录。\n\n" +
                        "请：右键本程序 →「以管理员身份运行」\n" +
                        "并在弹出的 UAC 窗口点「是」。",
                        "Claude 中文补丁",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return;
            }

            Application.Run(new MainForm());
        }

        static bool IsAdmin()
        {
            try
            {
                var id = WindowsIdentity.GetCurrent();
                var p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }

    sealed class MainForm : Form
    {
        readonly Label _lblStatus;
        readonly Label _lblClaude;
        readonly TextBox _log;
        readonly Button _btnPatch;
        readonly ProgressBar _bar;
        volatile bool _busy;

        public MainForm()
        {
            Text = "Claude 中文补丁 v3（管理员）";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(540, 450);
            Font = new Font("Microsoft YaHei UI", 9f);
            BackColor = Color.FromArgb(248, 249, 251);

            var title = new Label
            {
                Text = "Claude 桌面版 · 中文补丁",
                Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold),
                AutoSize = false,
                Location = new Point(20, 16),
                Size = new Size(500, 32),
                ForeColor = Color.FromArgb(30, 30, 30)
            };
            Controls.Add(title);

            var hint = new Label
            {
                Text = "绿色软件，无需安装。点下方按钮自动写入汉化。\n会自动结束 Claude 进程并修复写入权限（需管理员）。",
                AutoSize = false,
                Location = new Point(20, 52),
                Size = new Size(500, 40),
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            Controls.Add(hint);

            _lblClaude = new Label
            {
                Text = "正在检测 Claude…",
                AutoSize = false,
                Location = new Point(20, 100),
                Size = new Size(500, 40),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            Controls.Add(_lblClaude);

            _btnPatch = new Button
            {
                Text = "一键打补丁（汉化）",
                Location = new Point(20, 150),
                Size = new Size(200, 40),
                FlatStyle = FlatStyle.System,
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold)
            };
            _btnPatch.Click += OnPatchClick;
            Controls.Add(_btnPatch);

            _bar = new ProgressBar
            {
                Location = new Point(230, 160),
                Size = new Size(290, 20),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 0,
                Visible = true
            };
            Controls.Add(_bar);

            _lblStatus = new Label
            {
                Text = "就绪（已是管理员）",
                AutoSize = false,
                Location = new Point(20, 200),
                Size = new Size(500, 22),
                ForeColor = Color.FromArgb(0, 120, 60)
            };
            Controls.Add(_lblStatus);

            _log = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(20, 230),
                Size = new Size(500, 190),
                Font = new Font("Consolas", 8.5f),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_log);

            Shown += (s, e) => DetectClaude();
        }

        void Log(string msg)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), msg);
                return;
            }
            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
        }

        void SetStatus(string msg, Color? color = null)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetStatus(msg, color)));
                return;
            }
            _lblStatus.Text = msg;
            _lblStatus.ForeColor = color ?? Color.FromArgb(0, 120, 60);
        }

        void DetectClaude()
        {
            try
            {
                string root = Patcher.FindClaudeRoot();
                string ver = Patcher.ExtractVersion(root);
                _lblClaude.Text = "已找到 Claude\n版本 " + ver + "\n" + root;
                _lblClaude.ForeColor = Color.FromArgb(30, 30, 30);
                Log("管理员: 是");
                Log("检测到: " + root);
            }
            catch (Exception ex)
            {
                _lblClaude.Text = "未找到 Claude 桌面版\n请先安装 Claude，再运行本补丁。";
                _lblClaude.ForeColor = Color.Firebrick;
                _btnPatch.Enabled = false;
                SetStatus("未找到 Claude", Color.Firebrick);
                Log("错误: " + ex.Message);
            }
        }

        void OnPatchClick(object sender, EventArgs e)
        {
            if (_busy) return;
            _busy = true;
            _btnPatch.Enabled = false;
            _bar.MarqueeAnimationSpeed = 30;
            SetStatus("正在打补丁…", Color.FromArgb(0, 90, 160));
            Log("开始汉化…");

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var result = Patcher.Run(Log);
                    BeginInvoke(new Action(() =>
                    {
                        _bar.MarqueeAnimationSpeed = 0;
                        _busy = false;
                        _btnPatch.Enabled = true;
                        SetStatus("汉化成功", Color.FromArgb(0, 120, 60));
                        Log(result.Replace("\n", " | "));
                        MessageBox.Show(
                            result + "\n\n请重新打开 Claude。",
                            "补丁完成",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }));
                }
                catch (Exception ex)
                {
                    string full = ex.Message;
                    if (ex.InnerException != null)
                        full += "\n" + ex.InnerException.Message;
                    BeginInvoke(new Action(() =>
                    {
                        _bar.MarqueeAnimationSpeed = 0;
                        _busy = false;
                        _btnPatch.Enabled = true;
                        SetStatus("失败: 见下方日志", Color.Firebrick);
                        Log("ERROR: " + full);
                        MessageBox.Show(
                            full + "\n\n处理建议：\n" +
                            "1. 右键本程序 → 以管理员身份运行\n" +
                            "2. 完全退出 Claude（托盘也退出）后再试\n" +
                            "3. 暂时关闭杀毒/防护软件后再试\n" +
                            "4. 确认已安装 Claude 桌面版",
                            "补丁失败",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }));
                }
            });
        }
    }

    static class Patcher
    {
        const string WindowsApps = @"C:\Program Files\WindowsApps";
        const string OldLocales = "[\"en-US\",\"de-DE\",\"fr-FR\",\"ko-KR\",\"ja-JP\",\"es-419\",\"es-ES\",\"it-IT\",\"hi-IN\",\"pt-BR\",\"id-ID\"]";
        const string NewLocales = "[\"en-US\",\"de-DE\",\"fr-FR\",\"ko-KR\",\"ja-JP\",\"es-419\",\"es-ES\",\"it-IT\",\"hi-IN\",\"pt-BR\",\"id-ID\",\"zh-CN\"]";
        const string OldMap = "{\"en-US\":\"en\",\"de-DE\":\"de\",\"fr-FR\":\"fr\",\"ko-KR\":\"ko\",\"ja-JP\":\"ja\",\"es-419\":\"es\",\"es-ES\":\"es\",\"it-IT\":\"it\",\"hi-IN\":\"en\",\"pt-BR\":\"pt_BR\",\"id-ID\":\"id\"}";
        const string NewMap = "{\"en-US\":\"en\",\"de-DE\":\"de\",\"fr-FR\":\"fr\",\"ko-KR\":\"ko\",\"ja-JP\":\"ja\",\"es-419\":\"es\",\"es-ES\":\"es\",\"it-IT\":\"it\",\"hi-IN\":\"en\",\"pt-BR\":\"pt_BR\",\"id-ID\":\"id\",\"zh-CN\":\"zh_CN\"}";
        const string OldPersona = "case\"ja-JP\":return[\"language\",\"ja\"];case\"es-419\"";
        const string NewPersona = "case\"ja-JP\":return[\"language\",\"ja\"];case\"zh-CN\":return[\"language\",\"zh\"];case\"es-419\"";

        public static string FindClaudeRoot()
        {
            var cands = new List<Tuple<string, DateTime, string>>();

            // 1) Microsoft Store / WindowsApps
            if (Directory.Exists(WindowsApps))
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(WindowsApps, "Claude_*"))
                    {
                        string en = Path.Combine(dir, "app", "resources", "en-US.json");
                        if (File.Exists(en))
                            cands.Add(Tuple.Create(dir, File.GetLastWriteTimeUtc(en), Path.GetFileName(dir)));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // continue other locations
                }
            }

            // 2) Non-store common locations
            string[] extraRoots = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnthropicClaude"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "claude"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Claude"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Claude")
            };
            foreach (var root in extraRoots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    // direct en-US under app/resources
                    string en1 = Path.Combine(root, "app", "resources", "en-US.json");
                    if (File.Exists(en1))
                        cands.Add(Tuple.Create(root, File.GetLastWriteTimeUtc(en1), Path.GetFileName(root)));

                    // one level down
                    foreach (var dir in Directory.GetDirectories(root))
                    {
                        string en = Path.Combine(dir, "app", "resources", "en-US.json");
                        if (File.Exists(en))
                            cands.Add(Tuple.Create(dir, File.GetLastWriteTimeUtc(en), Path.GetFileName(dir)));
                        string en2 = Path.Combine(dir, "resources", "en-US.json");
                        if (File.Exists(en2))
                            cands.Add(Tuple.Create(dir, File.GetLastWriteTimeUtc(en2), Path.GetFileName(dir)));
                    }
                }
                catch { /* ignore */ }
            }

            if (cands.Count == 0)
                throw new Exception("未安装 Claude 桌面版（找不到 en-US.json）。\n请先从官网或 Microsoft Store 安装 Claude。");

            cands.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return cands[0].Item1;
        }

        public static string ExtractVersion(string appRoot)
        {
            string name = Path.GetFileName(appRoot);
            var parts = name.Split('_');
            if (parts.Length >= 2 && parts[0].Equals("Claude", StringComparison.OrdinalIgnoreCase))
                return parts[1];
            return name;
        }

        public static string Run(Action<string> log)
        {
            log("引擎: gui-portable-v3");
            string appRoot = FindClaudeRoot();
            // resources may be app\resources or resources
            string resources = Path.Combine(appRoot, "app", "resources");
            if (!Directory.Exists(resources))
            {
                string alt = Path.Combine(appRoot, "resources");
                if (Directory.Exists(alt)) resources = alt;
            }
            string version = ExtractVersion(appRoot);
            log("目标: " + appRoot);
            log("版本: " + version);
            log("资源: " + resources);

            // Stop Claude so files are not locked
            KillClaude(log);

            // Fix ACLs only on needed folders (not whole package — recursive takeown hangs)
            EnsureWriteAccess(resources, appRoot, log);

            string shellEn = Path.Combine(resources, "en-US.json");
            string ionEn = Path.Combine(resources, "ion-dist", "i18n", "en-US.json");
            string dynEn = Path.Combine(resources, "ion-dist", "i18n", "dynamic", "en-US.json");
            foreach (var p in new[] { shellEn, ionEn, dynEn })
            {
                if (!File.Exists(p)) throw new Exception("缺少源文件: " + p);
            }

            log("加载内置翻译缓存…");
            var memory = LoadMemory();
            log("缓存条目: " + memory.Count);
            if (memory.Count < 100)
                throw new Exception("翻译缓存异常（条目过少），已中止。");

            log("读取英文语言包…");
            var shellEnDict = ReadDict(shellEn);
            var ionEnDict = ReadDict(ionEn);
            var dynEnDict = ReadDict(dynEn);
            log(string.Format("shell={0} ion={1} dynamic={2}", shellEnDict.Count, ionEnDict.Count, dynEnDict.Count));
            if (shellEnDict.Count < 10 || ionEnDict.Count < 100)
                throw new Exception("英文语言包解析失败，请确认 Claude 安装完整。");

            int hit1, miss1, hit2, miss2, hit3, miss3;
            var shellZh = BuildZh(shellEnDict, memory, out hit1, out miss1);
            var dynZh = BuildZh(dynEnDict, memory, out hit2, out miss2);
            var ionZh = BuildZh(ionEnDict, memory, out hit3, out miss3);
            log(string.Format("[shell] hit={0} miss={1}", hit1, miss1));
            log(string.Format("[dynamic] hit={0} miss={1}", hit2, miss2));
            log(string.Format("[ion] hit={0} miss={1}", hit3, miss3));

            int totalHit = hit1 + hit2 + hit3;
            int totalMiss = miss1 + miss2 + miss3;
            if (totalHit < 100)
                throw new Exception("命中过少，已中止以免把中文覆盖成英文。");

            log("写入 zh-CN 语言包…");
            string shellDst = Path.Combine(resources, "zh-CN.json");
            string ionDst = Path.Combine(resources, "ion-dist", "i18n", "zh-CN.json");
            string ovrDst = Path.Combine(resources, "ion-dist", "i18n", "zh-CN.overrides.json");
            string dynDst = Path.Combine(resources, "ion-dist", "i18n", "dynamic", "zh-CN.json");

            // ensure subdirs exist + writable
            EnsureDirWritable(Path.GetDirectoryName(ionDst), log);
            EnsureDirWritable(Path.GetDirectoryName(dynDst), log);

            SafeWriteDict(shellDst, shellZh, log);
            SafeWriteDict(dynDst, dynZh, log);
            SafeWriteDict(ionDst, ionZh, log);
            SafeWriteText(ovrDst, "{}" + Environment.NewLine, log);

            string assets = Path.Combine(resources, "ion-dist", "assets", "v1");
            string bakDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Claude-zh-translate", "patched-js-backups");
            int patched = PatchJs(assets, bakDir, log);
            log("已补丁 JS: " + patched);

            try
            {
                string stateDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Claude-zh-translate");
                Directory.CreateDirectory(stateDir);
                string state = string.Format(
                    "{{\"installedAt\":\"{0}\",\"mode\":\"add-zh-CN-pack\",\"locale\":\"zh-CN\",\"appVersion\":\"{1}\",\"engine\":\"gui-portable-v3\",\"cacheHits\":{2},\"cacheMisses\":{3},\"claudeRoot\":\"{4}\"}}",
                    DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    EscapeJson(version),
                    totalHit,
                    totalMiss,
                    EscapeJson(appRoot.Replace("\\", "\\\\")));
                File.WriteAllText(Path.Combine(stateDir, "install-state.json"), state, new UTF8Encoding(false));
            }
            catch { /* ignore */ }

            return string.Format(
                "汉化完成！\nshell {0} / ion {1} / dynamic {2}\n命中 {3}，未命中 {4}",
                shellZh.Count, ionZh.Count, dynZh.Count, totalHit, totalMiss);
        }

        static void KillClaude(Action<string> log)
        {
            string[] names = { "Claude", "claude" };
            int killed = 0;
            foreach (var n in names)
            {
                Process[] list;
                try { list = Process.GetProcessesByName(n); }
                catch { continue; }
                foreach (var p in list)
                {
                    try
                    {
                        log("结束进程: " + p.ProcessName + " (PID " + p.Id + ")");
                        p.Kill();
                        p.WaitForExit(5000);
                        killed++;
                    }
                    catch (Exception ex)
                    {
                        log("结束进程失败 PID " + p.Id + ": " + ex.Message);
                    }
                    finally
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }
            if (killed > 0)
            {
                log("已结束 " + killed + " 个 Claude 进程，等待文件释放…");
                Thread.Sleep(1500);
            }
            else
            {
                log("未发现正在运行的 Claude 进程");
            }
        }

        static void FixDirAcl(string dir, Action<string> log, bool recursive)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            string domainUser = Environment.UserDomainName + "\\" + Environment.UserName;
            string rFlag = recursive ? " /R /D Y" : "";
            string tFlag = recursive ? " /T" : "";
            log((recursive ? "修复目录(递归): " : "修复目录: ") + dir);
            // takeown to Administrators group
            RunCmd("takeown.exe", "/F \"" + dir + "\" /A" + rFlag, log, recursive ? 45000 : 15000);
            // grant full control — use SID to avoid locale name issues
            RunCmd("icacls.exe", "\"" + dir + "\" /grant *S-1-5-32-544:(OI)(CI)F" + tFlag + " /C /Q", log, recursive ? 45000 : 15000);
            RunCmd("icacls.exe", "\"" + dir + "\" /grant *S-1-5-18:(OI)(CI)F" + tFlag + " /C /Q", log, recursive ? 45000 : 15000);
            RunCmd("icacls.exe", "\"" + dir + "\" /grant \"" + domainUser + "\":(OI)(CI)F" + tFlag + " /C /Q", log, recursive ? 45000 : 15000);
        }

        static void EnsureWriteAccess(string resources, string appRoot, Action<string> log)
        {
            string test = Path.Combine(resources, ".zh-cn-write-test");
            if (CanWrite(test))
            {
                log("写入权限正常");
                return;
            }

            log("无写权限，开始快速修复 ACL（只改必要目录，不会整包扫描）…");

            // Only touch the few folders we actually write into.
            // Full-package takeown /R on WindowsApps often hangs for many minutes.
            string ionI18n = Path.Combine(resources, "ion-dist", "i18n");
            string ionDyn = Path.Combine(ionI18n, "dynamic");
            string assets = Path.Combine(resources, "ion-dist", "assets", "v1");
            string ionDist = Path.Combine(resources, "ion-dist");
            string appDir = Path.Combine(appRoot, "app");

            // 1) Non-recursive on package root + app (just the folder object)
            FixDirAcl(appRoot, log, false);
            FixDirAcl(appDir, log, false);

            // 2) Recursive only on small write targets
            FixDirAcl(resources, log, true);
            if (Directory.Exists(ionDist)) FixDirAcl(ionDist, log, false);
            if (Directory.Exists(ionI18n)) FixDirAcl(ionI18n, log, true);
            if (Directory.Exists(ionDyn)) FixDirAcl(ionDyn, log, true);
            if (Directory.Exists(assets)) FixDirAcl(assets, log, true);

            // 3) Clear read-only on resources only (not whole package)
            log("清除 resources 只读属性…");
            RunCmd("attrib.exe", "-R \"" + resources + "\\*.*\" /S /D", log, 30000);

            // 4) Unlock known target files
            string[] unlockFiles = new string[]
            {
                Path.Combine(resources, "zh-CN.json"),
                Path.Combine(ionI18n, "zh-CN.json"),
                Path.Combine(ionI18n, "zh-CN.overrides.json"),
                Path.Combine(ionDyn, "zh-CN.json")
            };
            foreach (var f in unlockFiles)
            {
                try
                {
                    string dir = Path.GetDirectoryName(f);
                    if (!Directory.Exists(dir))
                    {
                        try { Directory.CreateDirectory(dir); } catch { }
                        FixDirAcl(dir, log, false);
                    }
                    if (File.Exists(f))
                    {
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                        RunCmd("takeown.exe", "/F \"" + f + "\" /A", log, 10000);
                        RunCmd("icacls.exe", "\"" + f + "\" /grant *S-1-5-32-544:F /C /Q", log, 10000);
                    }
                }
                catch { }
            }

            if (CanWrite(test))
            {
                log("ACL 修复后可写");
                return;
            }

            // 5) Last resort: cmd copy test
            log("仍不可写，尝试 cmd 强制写测试…");
            string tmp = Path.Combine(Path.GetTempPath(), "zh-cn-write-test-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                File.WriteAllText(tmp, "ok", Encoding.ASCII);
                RunCmd("cmd.exe", "/c copy /Y \"" + tmp + "\" \"" + test + "\"", log, 15000);
                RunCmd("cmd.exe", "/c del /F /Q \"" + test + "\"", log, 15000);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }

            if (CanWrite(test))
            {
                log("强制写测试成功");
                return;
            }

            // Diagnostic info for user to send back
            log("诊断: 当前用户=" + Environment.UserDomainName + "\\" + Environment.UserName);
            RunCmd("whoami.exe", "/groups", log, 10000);
            RunCmd("icacls.exe", "\"" + resources + "\"", log, 10000);

            throw new Exception(
                "仍无法写入 Claude 安装目录。\n" +
                "目录: " + resources + "\n\n" +
                "请确认：\n" +
                "1. 右键本程序 → 以管理员身份运行，UAC 点「是」\n" +
                "2. 完全退出 Claude 后再试\n" +
                "3. 暂时关闭杀毒/Defender 实时保护\n" +
                "4. 把窗口下方完整日志发给制作者\n" +
                "5. 若公司电脑有管控，需 IT 放开 WindowsApps 写权限");
        }

        static bool CanWrite(string testPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(testPath);
                if (!Directory.Exists(dir)) return false;
                File.WriteAllText(testPath, "ok");
                File.Delete(testPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void EnsureDirWritable(string dir, Action<string> log)
        {
            if (string.IsNullOrEmpty(dir)) return;
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); }
                catch
                {
                    RunCmd("cmd.exe", "/c mkdir \"" + dir + "\"", log, 15000);
                }
            }
            try
            {
                FixDirAcl(dir, log, false);
            }
            catch { }
        }

        static void SafeWriteDict(string path, Dictionary<string, string> dict, Action<string> log)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            int i = 0;
            int n = dict.Count;
            foreach (var kv in dict)
            {
                i++;
                string line = "  \"" + EscapeJson(kv.Key) + "\": \"" + EscapeJson(kv.Value) + "\"";
                if (i < n) line += ",";
                sb.AppendLine(line);
            }
            sb.AppendLine("}");
            SafeWriteText(path, sb.ToString(), log);
        }

        static void SafeWriteText(string path, string content, Action<string> log)
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); }
                catch
                {
                    RunCmd("cmd.exe", "/c mkdir \"" + dir + "\"", log, 15000);
                }
            }

            // Unlock existing file
            if (File.Exists(path))
            {
                try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
                try
                {
                    RunCmd("takeown.exe", "/F \"" + path + "\" /A", log, 15000);
                    RunCmd("icacls.exe", "\"" + path + "\" /grant *S-1-5-32-544:F /C /Q", log, 15000);
                }
                catch { }
            }

            // Write to temp then copy (helps some locked/ACL edge cases)
            string tmp = Path.Combine(Path.GetTempPath(), "claude-zh-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(tmp, content, new UTF8Encoding(false));
                try
                {
                    File.Copy(tmp, path, true);
                    log("写入: " + Path.GetFileName(path));
                    return;
                }
                catch (Exception ex1)
                {
                    log("直接复制失败 (" + Path.GetFileName(path) + "): " + ex1.Message);
                }

                // cmd copy
                int code = RunCmd("cmd.exe", "/c copy /Y \"" + tmp + "\" \"" + path + "\"", log, 30000);
                if (code == 0 && File.Exists(path))
                {
                    log("cmd 复制成功: " + Path.GetFileName(path));
                    return;
                }

                // last: File.WriteAllText after re-acl
                try
                {
                    File.WriteAllText(path, content, new UTF8Encoding(false));
                    log("直接写入成功: " + Path.GetFileName(path));
                    return;
                }
                catch (Exception ex2)
                {
                    throw new Exception("写入失败: " + path + "\n" + ex2.Message);
                }
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        static int RunCmd(string file, string args, Action<string> log, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Environment.SystemDirectory
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null) return -1;
                    string stdout = "";
                    string stderr = "";
                    var tOut = new Thread(() => { try { stdout = p.StandardOutput.ReadToEnd(); } catch { } });
                    var tErr = new Thread(() => { try { stderr = p.StandardError.ReadToEnd(); } catch { } });
                    tOut.IsBackground = true;
                    tErr.IsBackground = true;
                    tOut.Start();
                    tErr.Start();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        log(Path.GetFileName(file) + " 超时");
                        return -2;
                    }
                    tOut.Join(3000);
                    tErr.Join(3000);
                    int code = p.ExitCode;
                    string combined = ((stdout ?? "") + " " + (stderr ?? "")).Trim();
                    if (combined.Length > 200) combined = combined.Substring(0, 200) + "...";
                    if (code != 0 && !string.IsNullOrEmpty(combined))
                        log(Path.GetFileName(file) + " exit=" + code + " " + combined);
                    return code;
                }
            }
            catch (Exception ex)
            {
                log(Path.GetFileName(file) + " 启动失败: " + ex.Message);
                return -3;
            }
        }

        static Dictionary<string, string> LoadMemory()
        {
            string json = null;
            var asm = Assembly.GetExecutingAssembly();
            foreach (var n in asm.GetManifestResourceNames())
            {
                if (n.EndsWith("translation-memory.json", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("translation-memory.json", StringComparison.OrdinalIgnoreCase))
                {
                    using (var s = asm.GetManifestResourceStream(n))
                    using (var r = new StreamReader(s, Encoding.UTF8))
                    {
                        json = r.ReadToEnd();
                    }
                    break;
                }
            }
            if (json == null)
            {
                string side = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translation-memory.json");
                if (File.Exists(side))
                    json = File.ReadAllText(side, Encoding.UTF8);
            }
            if (string.IsNullOrEmpty(json))
                throw new Exception("找不到 translation-memory.json（程序内置或同目录）。");
            return DeserializeStringDict(json);
        }

        static Dictionary<string, string> ReadDict(string path)
        {
            string raw = File.ReadAllText(path, Encoding.UTF8);
            return DeserializeStringDict(raw);
        }

        static Dictionary<string, string> DeserializeStringDict(string json)
        {
            var ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            ser.RecursionLimit = 100;
            object obj = ser.DeserializeObject(json);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (obj is IDictionary)
            {
                foreach (DictionaryEntry de in (IDictionary)obj)
                {
                    if (de.Key == null) continue;
                    result[de.Key.ToString()] = de.Value == null ? "" : de.Value.ToString();
                }
            }
            return result;
        }

        static Dictionary<string, string> BuildZh(
            Dictionary<string, string> en,
            Dictionary<string, string> memory,
            out int hit,
            out int miss)
        {
            var outDict = new Dictionary<string, string>(StringComparer.Ordinal);
            hit = 0;
            miss = 0;
            foreach (var kv in en)
            {
                string zh;
                if (memory.TryGetValue(kv.Value, out zh))
                {
                    outDict[kv.Key] = zh;
                    hit++;
                }
                else
                {
                    outDict[kv.Key] = kv.Value;
                    miss++;
                }
            }
            return outDict;
        }

        static string EscapeJson(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20)
                            sb.AppendFormat("\\u{0:x4}", (int)ch);
                        else
                            sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }

        static int PatchJs(string assetsDir, string backupDir, Action<string> log)
        {
            if (!Directory.Exists(assetsDir))
            {
                log("无 assets/v1，跳过 JS 补丁");
                return 0;
            }
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            int count = 0;
            string[] files;
            try { files = Directory.GetFiles(assetsDir, "*.js"); }
            catch (Exception ex)
            {
                log("扫描 JS 失败: " + ex.Message);
                return 0;
            }

            foreach (var path in files)
            {
                FileInfo fi;
                try { fi = new FileInfo(path); }
                catch { continue; }
                if (fi.Length < 500 || fi.Length > 50L * 1024 * 1024) continue;

                string text;
                try { text = File.ReadAllText(path, Encoding.UTF8); }
                catch { continue; }

                if (text.IndexOf("en-US", StringComparison.Ordinal) < 0) continue;
                if (text.IndexOf("de-DE", StringComparison.Ordinal) < 0
                    && text.IndexOf("ja-JP", StringComparison.Ordinal) < 0
                    && text.IndexOf("pt-BR", StringComparison.Ordinal) < 0)
                    continue;

                string orig = text;
                bool changed = false;

                if (text.Contains(OldLocales) && !text.Contains(NewLocales))
                {
                    text = text.Replace(OldLocales, NewLocales);
                    changed = true;
                }
                if (text.Contains(OldMap) && text.IndexOf("\"zh-CN\":\"zh_CN\"", StringComparison.Ordinal) < 0)
                {
                    text = text.Replace(OldMap, NewMap);
                    changed = true;
                }
                if (text.Contains(OldPersona) && text.IndexOf("case\"zh-CN\":return[\"language\",\"zh\"]", StringComparison.Ordinal) < 0)
                {
                    text = text.Replace(OldPersona, NewPersona);
                    changed = true;
                }

                if (changed && text != orig)
                {
                    string bak = Path.Combine(backupDir, fi.Name);
                    try
                    {
                        if (!File.Exists(bak))
                            File.Copy(path, bak, false);
                    }
                    catch { }

                    try
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                    }
                    catch { }
                    try
                    {
                        RunCmd("takeown.exe", "/F \"" + path + "\" /A", log, 15000);
                        RunCmd("icacls.exe", "\"" + path + "\" /grant *S-1-5-32-544:F /C /Q", log, 15000);
                    }
                    catch { }

                    try
                    {
                        File.WriteAllText(path, text, new UTF8Encoding(false));
                        log("补丁 JS: " + fi.Name);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        // try cmd write via temp
                        string tmp = Path.Combine(Path.GetTempPath(), "js-" + Guid.NewGuid().ToString("N") + ".js");
                        try
                        {
                            File.WriteAllText(tmp, text, new UTF8Encoding(false));
                            int code = RunCmd("cmd.exe", "/c copy /Y \"" + tmp + "\" \"" + path + "\"", log, 30000);
                            if (code == 0)
                            {
                                log("补丁 JS(cmd): " + fi.Name);
                                count++;
                            }
                            else
                            {
                                log("补丁 JS 失败 " + fi.Name + ": " + ex.Message);
                            }
                        }
                        finally
                        {
                            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                        }
                    }
                }
            }
            return count;
        }
    }
}
