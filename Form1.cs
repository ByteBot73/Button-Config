using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Button_Config
{
    public partial class Form1 : Form
    {
        private AppSettings _settings = new AppSettings();
        private string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CopilotRemapperSettings.json");

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_F23 = 0x86;
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        // UI Elements
        private TextBox txtTarget;
        private Button btnSave;
        private Button btnUpdate;
        private NotifyIcon trayIcon;

        public Form1()
        {
            LoadSettings();
            SetupUI();
            SetupTray();

            _proc = HookCallback;
            _hookID = SetHook(_proc);

            // Check for updates silently in the background when app starts
            _ = CheckForUpdates(silent: true);
        }

        private void SetupUI()
        {
            this.Text = "Copilot Key Configurator v1.0.0";
            this.Size = new System.Drawing.Size(600, 400); // BIGGER UI
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;

            Label lblHeader = new Label() { 
                Text = "Configuration Settings", 
                Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
                Left = 30, Top = 20, Width = 500 
            };

            Label lblDesc = new Label() { 
                Text = "Enter the full path to an application (.exe) or a website URL (https://...)", 
                Left = 30, Top = 70, Width = 500, Height = 40 
            };

            txtTarget = new TextBox() { 
                Left = 30, Top = 120, Width = 520, 
                Font = new System.Drawing.Font("Segoe UI", 12),
                Text = _settings.Target 
            };

            btnSave = new Button() { 
                Text = "SAVE SETTINGS", 
                Left = 30, Top = 180, Width = 520, Height = 50,
                BackColor = System.Drawing.Color.LightBlue,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.Click += (s, e) => {
                _settings.Target = txtTarget.Text;
                SaveSettings();
                MessageBox.Show("Settings saved! The app is now running in your system tray.");
                this.Hide(); 
            };

            btnUpdate = new Button() { 
                Text = "Check for Updates", 
                Left = 30, Top = 250, Width = 200, Height = 30 
            };
            btnUpdate.Click += async (s, e) => await CheckForUpdates(silent: false);

            this.Controls.Add(lblHeader);
            this.Controls.Add(lblDesc);
            this.Controls.Add(txtTarget);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnUpdate);
        }

        private async Task CheckForUpdates(bool silent)
        {
            try
            {
                // REPLACE THIS URL WITH YOUR GITHUB REPO URL
                var mgr = new UpdateManager(new GithubSource("https://github.com/ByteBot73/Button-Config", null, false));
                
                var newVersion = await mgr.CheckForUpdatesAsync();
                if (newVersion == null)
                {
                    if (!silent) MessageBox.Show("You are on the latest version!");
                    return;
                }

                // THE POPUP YOU ASKED FOR
                DialogResult result = MessageBox.Show(
                    "A new update is available! Would you like to download and install it now?", 
                    "Update Found", 
                    MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Information
                );

                if (result == DialogResult.Yes)
                {
                    if (!silent) btnUpdate.Text = "Downloading...";
                    await mgr.DownloadUpdatesAsync(newVersion);
                    mgr.ApplyUpdatesAndRestart(newVersion);
                }
            }
            catch (Exception ex)
            {
                if (!silent) MessageBox.Show("Update check failed: " + ex.Message);
            }
        }

        private void SetupTray()
        {
            trayIcon = new NotifyIcon()
            {
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true,
                Text = "Copilot Remapper Running"
            };
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };
            
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Settings", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
            menu.Items.Add("Exit", null, (s, e) => { Application.Exit(); });
            trayIcon.ContextMenuStrip = menu;
        }

        // --- HOOK LOGIC ---
        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_F23)
                {
                    bool winDown = (GetKeyState(0x5B) & 0x8000) != 0;
                    bool shiftDown = (GetKeyState(0xA0) & 0x8000) != 0;
                    if (winDown && shiftDown)
                    {
                        try {
                            Process.Start(new ProcessStartInfo(_settings.Target) { UseShellExecute = true });
                        } catch { }
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void LoadSettings() {
            try { if (File.Exists(_settingsPath)) _settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath)); } catch { }
        }

        private void SaveSettings() {
            try { File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_settings)); } catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnhookWindowsHookEx(_hookID);
            trayIcon.Dispose();
            base.OnFormClosing(e);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
    }

    public class AppSettings {
        public string Target { get; set; } = "notepad.exe";
    }
}