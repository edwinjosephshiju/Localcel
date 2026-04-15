import os
import sys
import subprocess
import tempfile
import shutil
import hashlib

# ==========================================
# MODULE: DROPPER ARCHITECTURE
# ==========================================
# The entire GUI application is stored as a string payload.
# This prevents PyInstaller from seeing PyQt6 during the build phase,
# keeping the resulting .exe incredibly small (~5MB).

PAYLOAD = r'''
import os
import sys
import json
import socket
import shutil
import subprocess
import threading
import re
import platform
import webbrowser
from pathlib import Path
from dataclasses import dataclass
from typing import Dict, Optional, List

GLOBAL_APP_DIR = Path.home() / ".localcel"
GLOBAL_APP_DIR.mkdir(parents=True, exist_ok=True)
DEPS_FILE = GLOBAL_APP_DIR / "deps.json"
CONFIG_FILE = GLOBAL_APP_DIR / "config.json"

def manage_dependencies():
    """Checks and installs PyQt6 and System dependencies on the HOST machine."""
    required_python = ["PyQt6", "psutil", "packaging"]
    
    if "--uninstall-deps" in sys.argv:
        if DEPS_FILE.exists():
            with open(DEPS_FILE, 'r') as f:
                installed = json.load(f)
            for pkg in installed:
                subprocess.run([sys.executable, "-m", "pip", "uninstall", "-y", pkg])
            DEPS_FILE.unlink()
            print("Dependencies removed.")
        sys.exit(0)

    missing = []
    for pkg in required_python:
        try:
            __import__(pkg)
        except ImportError:
            missing.append(pkg)

    if missing:
        # Show a prompt to the user so they know why the app is pausing
        if platform.system() == "Windows":
            import ctypes
            msg = f"Localcel needs to install required dependencies to run on this machine: {', '.join(missing)}\n\nThis may take a minute. Please wait after clicking OK."
            ctypes.windll.user32.MessageBoxW(0, msg, "First Time Setup", 0x40)

        print(f"Installing missing components: {missing}...")
        GLOBAL_APP_DIR.mkdir(parents=True, exist_ok=True)
        subprocess.check_call([sys.executable, "-m", "pip", "install"] + missing)
        with open(DEPS_FILE, 'w') as f:
            json.dump(required_python, f)
        print("Python dependencies ready. Restarting...")
        subprocess.call([sys.executable] + sys.argv)
        sys.exit(0)

manage_dependencies()

from PyQt6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, 
                             QHBoxLayout, QLabel, QPushButton, QFrame, QScrollArea,
                             QTabWidget, QPlainTextEdit, QLineEdit, QDialog, QMessageBox,
                             QFileDialog, QSystemTrayIcon, QMenu)
from PyQt6.QtCore import Qt, pyqtSignal, QObject, QTimer
from PyQt6.QtGui import QIcon, QPixmap, QImage
import psutil
import base64

# ==========================================
# MODULE: EMBEDDED ASSETS
# ==========================================
# TODO: Replace these tiny placeholder strings with your actual Base64 data!
ICON_B64 = b"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY3jP4PgfAAWpA6Fh80E1AAAAAElFTkSuQmCC"
LOGO_B64 = b"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY3jP4PgfAAWpA6Fh80E1AAAAAElFTkSuQmCC"

def get_icon():
    img = QImage.fromData(base64.b64decode(ICON_B64))
    return QIcon(QPixmap.fromImage(img))

def get_logo():
    img = QImage.fromData(base64.b64decode(LOGO_B64))
    return QPixmap.fromImage(img)

# ==========================================
# MODULE: UTILS & PATHS
# ==========================================
BASE_DIR = None
APPS_DIR = None
LOGS_DIR = None
PIDS_DIR = None

def initialize_workspace(path: Path):
    global BASE_DIR, APPS_DIR, LOGS_DIR, PIDS_DIR
    BASE_DIR = path
    APPS_DIR = BASE_DIR / "apps"
    LOGS_DIR = BASE_DIR / "logs"
    PIDS_DIR = BASE_DIR / "pids"
    ensure_directories()

def ensure_directories():
    if BASE_DIR:
        for d in [APPS_DIR, LOGS_DIR, PIDS_DIR]:
            d.mkdir(parents=True, exist_ok=True)

def get_executable_path(name: str) -> Optional[str]:
    return shutil.which(name)

def apply_translucent_acrylic(window):
    if platform.system() == "Windows":
        window.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        try:
            import ctypes
            from ctypes import wintypes
            hwnd = int(window.winId())
            set_window_attribute = ctypes.windll.dwmapi.DwmSetWindowAttribute
            set_window_attribute(hwnd, 20, ctypes.byref(ctypes.c_int(1)), 4)
            set_window_attribute(hwnd, 38, ctypes.byref(ctypes.c_int(3)), 4)
            class MARGINS(ctypes.Structure):
                _fields_ = [("cxLeftWidth", ctypes.c_int), ("cxRightWidth", ctypes.c_int),
                            ("cyTopHeight", ctypes.c_int), ("cyBottomHeight", ctypes.c_int)]
            margins = MARGINS(-1, -1, -1, -1)
            ctypes.windll.dwmapi.DwmExtendFrameIntoClientArea(hwnd, ctypes.byref(margins))
        except Exception as e:
            print(f"Native acrylic failed: {e}")

def get_or_choose_workspace() -> Path:
    if CONFIG_FILE.exists():
        try:
            with open(CONFIG_FILE, 'r') as f:
                data = json.load(f)
                ws_str = data.get("workspace")
                if ws_str:
                    ws_path = Path(ws_str)
                    if ws_path.exists() and ws_path.is_dir():
                        return ws_path
        except Exception:
            pass

    msg = QMessageBox()
    msg.setWindowTitle("Workspace Setup")
    msg.setWindowIcon(get_icon())
    msg.setText("Welcome to Localcel!\n\nPlease select a directory to act as your Server Workspace.\nAll your applications, configurations, and logs will be securely stored there.")
    msg.setIcon(QMessageBox.Icon.Information)
    msg.exec()

    dialog = QFileDialog()
    dialog.setWindowIcon(get_icon())
    dialog.setWindowTitle("Select Workspace Directory")
    dialog.setFileMode(QFileDialog.FileMode.Directory)
    dialog.setOption(QFileDialog.Option.ShowDirsOnly, True)

    if dialog.exec():
        selected = dialog.selectedFiles()[0]
        selected_path = Path(selected)
        if selected_path.name != "localcel_workspace":
            ws_path = selected_path / "localcel_workspace"
        else:
            ws_path = selected_path
            
        ws_path.mkdir(parents=True, exist_ok=True)
        with open(CONFIG_FILE, 'w') as f:
            json.dump({"workspace": str(ws_path)}, f)
        return ws_path
    else:
        sys.exit(0)

# ==========================================
# MODULE: CORE LOGIC
# ==========================================
@dataclass
class AppConfig:
    name: str
    port: int
    entry: str
    domain: Optional[str] = None

class AppManager:
    @staticmethod
    def get_apps() -> List[AppConfig]:
        ensure_directories()
        apps = []
        for app_dir in APPS_DIR.iterdir():
            if app_dir.is_dir() and (app_dir / "config.json").exists():
                try:
                    with open(app_dir / "config.json", 'r') as f:
                        data = json.load(f)
                        apps.append(AppConfig(**data))
                except: pass
        return sorted(apps, key=lambda x: x.name)

    @staticmethod
    def create_app(name: str, port: int, domain: str = "", entry: str = "server.js"):
        app_dir = APPS_DIR / name
        app_dir.mkdir(parents=True, exist_ok=True)
        config = {"name": name, "port": port, "entry": entry}
        if domain: config["domain"] = domain
        with open(app_dir / "config.json", 'w') as f:
            json.dump(config, f, indent=4)
        
        server_js = f"""const http = require('http');
const port = process.env.PORT || {port};
const server = http.createServer((req, res) => {{
  console.log(`[${{new Date().toISOString()}}] ${{req.method}} ${{req.url}}`);
  res.end('Localcel: {name} is running!');
}});
server.listen(port, () => {{
  console.log(`Server started on port ${{port}}`);
}});"""
        with open(app_dir / entry, 'w', encoding="utf-8") as f:
            f.write(server_js)

    @staticmethod
    def update_app(name: str, port: int, domain: str = ""):
        config_path = APPS_DIR / name / "config.json"
        if config_path.exists():
            with open(config_path, 'r') as f:
                config = json.load(f)
            config['port'] = port
            if domain:
                config['domain'] = domain
            elif 'domain' in config:
                del config['domain']
            with open(config_path, 'w') as f:
                json.dump(config, f, indent=4)

    @staticmethod
    def delete_app(name: str):
        if (APPS_DIR / name).exists():
            shutil.rmtree(APPS_DIR / name)

class CloudflareHelper:
    @staticmethod
    def install_cloudflared():
        if platform.system() == "Windows":
            if get_executable_path("winget"):
                subprocess.run(["winget", "install", "Cloudflare.cloudflared"], shell=True)
                return True
        return False

    @staticmethod
    def list_tunnels() -> List[dict]:
        cf_bin = get_executable_path("cloudflared")
        if not cf_bin: return []
        res = subprocess.run([cf_bin, "tunnel", "list", "--output", "json"], capture_output=True, text=True)
        if res.returncode == 0:
            try: return json.loads(res.stdout)
            except: return []
        return []

    @staticmethod
    def delete_tunnel(tunnel_id_or_name: str):
        cf_bin = get_executable_path("cloudflared")
        if not cf_bin: return
        subprocess.run([cf_bin, "tunnel", "delete", "-f", tunnel_id_or_name])

    @staticmethod
    def setup_named_tunnel(app_name: str, port: int, domain: str) -> Path:
        cf_bin = get_executable_path("cloudflared")
        tunnel_name = f"localcel_{app_name}"
        subprocess.run([cf_bin, "tunnel", "create", tunnel_name], capture_output=True)
        
        tunnels = CloudflareHelper.list_tunnels()
        tunnel_id = next((t['id'] for t in tunnels if t['name'] == tunnel_name), None)
        
        if not tunnel_id: raise Exception("Tunnel ID not found.")
        
        subprocess.run([cf_bin, "tunnel", "route", "dns", "-f", tunnel_name, domain], capture_output=True)
        
        config_path = APPS_DIR / app_name / "tunnel.yml"
        cred_file = Path.home() / ".cloudflared" / f"{tunnel_id}.json"
        yaml = f"tunnel: {tunnel_id}\ncredentials-file: {cred_file.as_posix()}\ningress:\n  - hostname: {domain}\n    service: http://localhost:{port}\n  - service: http_status:404"
        with open(config_path, 'w') as f: f.write(yaml)
        return config_path

class WorkerSignals(QObject):
    log_appended = pyqtSignal(str, str, str)
    tunnel_ready = pyqtSignal()

class ManagedProcess:
    def __init__(self, name: str, is_tunnel: bool = False):
        self.name = name
        self.is_tunnel = is_tunnel
        self.process = None
        self.url = None
        suffix = "_tunnel" if is_tunnel else ""
        self.log_path = LOGS_DIR / f"{name}{suffix}.log"

    def start(self, cmd: List[str], cwd: str, log_cb):
        if self.is_running(): return
        
        creation_flags = subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0
        self.process = subprocess.Popen(
            cmd, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, encoding="utf-8", errors="replace", bufsize=1, creationflags=creation_flags
        )
        
        def reader(proc):
            url_regex = re.compile(r"https://[a-zA-Z0-9-]+\.trycloudflare\.com")
            try:
                with open(self.log_path, 'a', encoding="utf-8") as f:
                    for line in proc.stdout:
                        f.write(line)
                        f.flush()
                        log_cb(line.strip())
                        if self.is_tunnel and not self.url:
                            match = url_regex.search(line)
                            if match: self.url = match.group(0)
                proc.wait()
            except Exception:
                pass

        threading.Thread(target=reader, args=(self.process,), daemon=True).start()

    def stop(self):
        if self.process:
            try:
                p = psutil.Process(self.process.pid)
                for child in p.children(recursive=True): child.kill()
                p.kill()
            except: pass
            self.process = None
            self.url = None

    def is_running(self) -> bool:
        return self.process is not None and self.process.poll() is None

# ==========================================
# MODULE: GUI COMPONENTS (PYQT6)
# ==========================================
QSS = """
QMainWindow, QDialog { background: transparent; }
QWidget#Central { background-color: rgba(10, 10, 10, 40); }
QFrame#Sidebar { background-color: rgba(0, 0, 0, 50); border-right: 1px solid rgba(255, 255, 255, 20); }
QScrollArea, QScrollArea::viewport { background: transparent; border: none; }
QWidget#ScrollContainer { background: transparent; }
QFrame#HeaderCard { background-color: rgba(30, 30, 30, 90); border: 1px solid rgba(255, 255, 255, 20); border-radius: 8px; }
QPushButton { background-color: rgba(50, 50, 50, 100); color: #FFFFFF; border: 1px solid rgba(255, 255, 255, 30); border-radius: 4px; padding: 8px 16px; font-family: "Segoe UI"; font-size: 13px; }
QPushButton:hover { background-color: rgba(80, 80, 80, 140); }
QPushButton:disabled { color: #777777; border-color: rgba(255, 255, 255, 10); background-color: rgba(30, 30, 30, 60); }
QPushButton#PrimaryBtn { background-color: #60CDFF; color: black; border: none; font-weight: bold; }
QPushButton#PrimaryBtn:hover { background-color: #58BCEB; }
QPushButton#StartBtn { background-color: rgba(108, 203, 95, 40); color: #FFFFFF; border: 1px solid rgba(108, 203, 95, 80); font-weight: bold; }
QPushButton#StartBtn:hover { background-color: rgba(108, 203, 95, 80); border: 1px solid rgba(108, 203, 95, 120); }
QPushButton#StartBtn:disabled { background-color: rgba(50, 50, 50, 80); border: 1px solid rgba(255, 255, 255, 10); color: #777777; font-weight: normal; }
QPushButton#StopBtn { background-color: rgba(196, 43, 28, 40); color: #FFFFFF; border: 1px solid rgba(196, 43, 28, 80); font-weight: bold; }
QPushButton#StopBtn:hover { background-color: rgba(196, 43, 28, 80); border: 1px solid rgba(196, 43, 28, 120); }
QPushButton#StopBtn:disabled { background-color: rgba(50, 50, 50, 80); border: 1px solid rgba(255, 255, 255, 10); color: #777777; font-weight: normal; }
QTabWidget { background: transparent; }
QTabWidget::pane { border: 1px solid rgba(255, 255, 255, 20); border-radius: 4px; background: rgba(15, 15, 15, 80); }
QTabBar::tab { background: rgba(10, 10, 10, 90); border: 1px solid rgba(255, 255, 255, 20); padding: 8px 16px; margin-right: 2px; border-top-left-radius: 4px; border-top-right-radius: 4px; color: #A0A0A0; font-family: "Segoe UI"; }
QTabBar::tab:selected { background: rgba(60, 60, 60, 140); color: white; }
QTabBar::tab:hover:!selected { background: rgba(40, 40, 40, 120); }
QPlainTextEdit { background: transparent; color: #E5E5E5; border: none; font-family: "Consolas"; font-size: 13px; padding: 8px; }
QLineEdit { background-color: rgba(0, 0, 0, 90); border: 1px solid rgba(255, 255, 255, 30); border-radius: 4px; padding: 8px; color: white; font-family: "Segoe UI"; font-size: 13px; }
QLabel { color: white; font-family: "Segoe UI"; background: transparent; }
"""

class AppCard(QFrame):
    def __init__(self, app_conf: AppConfig, on_select, parent=None):
        super().__init__(parent)
        self.app_conf = app_conf
        self.on_select = on_select
        
        self.setCursor(Qt.CursorShape.PointingHandCursor)
        self.setFixedHeight(50)
        
        layout = QHBoxLayout(self)
        layout.setContentsMargins(15, 0, 15, 0)
        
        self.lbl_name = QLabel(app_conf.name)
        self.status_dot = QLabel("●")
        self.status_dot.setStyleSheet("color: #555555; font-size: 16px; background: transparent; border: none;")
        
        layout.addWidget(self.lbl_name)
        layout.addStretch()
        layout.addWidget(self.status_dot)
        
        self.set_selected(False)
        
    def mousePressEvent(self, event):
        if event.button() == Qt.MouseButton.LeftButton:
            self.on_select(self.app_conf)

    def update_status(self, is_running):
        color = "#6CCB5F" if is_running else "#C42B1C"
        self.status_dot.setStyleSheet(f"color: {color}; font-size: 16px; background: transparent; border: none;")

    def set_selected(self, is_selected):
        if is_selected:
            self.setStyleSheet("QFrame { background-color: rgba(255, 255, 255, 20); border: 1px solid rgba(255, 255, 255, 40); border-radius: 8px; } QLabel { color: #FFFFFF; font-weight: bold; font-family: 'Segoe UI'; font-size: 13px; background: transparent; border: none; }")
        else:
            self.setStyleSheet("QFrame { background-color: transparent; border: 1px solid rgba(255, 255, 255, 10); border-radius: 8px; } QLabel { color: #E5E5E5; font-weight: normal; font-family: 'Segoe UI'; font-size: 13px; background: transparent; border: none; }")

class LocalcelGUI(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Localcel - Vercel for Localhost")
        self.setWindowIcon(get_icon())
        self.resize(1150, 750)
        apply_translucent_acrylic(self)
        
        self.engine_servers = {}
        self.engine_tunnels = {}
        self.selected_app = None
        self.cards = {}
        
        self.signals = WorkerSignals()
        self.signals.log_appended.connect(self.append_log)
        self.signals.tunnel_ready.connect(self.tunnel_manager_dialog)
        
        self.setup_ui()
        self.setup_tray_icon()
        self.refresh_app_list()
        
        QApplication.instance().commitDataRequest.connect(self.on_commit_data_request)
        
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.check_loop)
        self.timer.start(1000)

    def setup_tray_icon(self):
        self.tray_icon = QSystemTrayIcon(self)
        self.tray_icon.setIcon(get_icon())
        self.tray_icon.setToolTip("Localcel - No running servers")
        
        self.tray_menu = QMenu(self)
        self.tray_menu.setStyleSheet("""
            QMenu { background-color: #272727; color: #FFFFFF; border: 1px solid #333333; border-radius: 4px; }
            QMenu::item { padding: 6px 25px 6px 20px; font-family: "Segoe UI"; }
            QMenu::item:selected { background-color: #3A3A3A; }
            QMenu::separator { height: 1px; background-color: #333333; margin: 4px 0px 4px 0px; }
        """)
        
        self.tray_menu.aboutToShow.connect(self.update_tray_menu)
        self.tray_icon.setContextMenu(self.tray_menu)
        self.tray_icon.activated.connect(self.tray_activated)
        self.tray_icon.show()

    def update_tray_menu(self):
        self.tray_menu.clear()
        running_apps = [name for name, srv in self.engine_servers.items() if srv.is_running()]
        if running_apps:
            title_action = self.tray_menu.addAction("Running Servers:")
            title_action.setEnabled(False)
            for app in running_apps:
                action = self.tray_menu.addAction(f"  ● {app}")
                action.setEnabled(False)
        else:
            action = self.tray_menu.addAction("No running servers")
            action.setEnabled(False)
        self.tray_menu.addSeparator()
        show_action = self.tray_menu.addAction("Show Localcel")
        show_action.triggered.connect(self.show_normal)
        exit_action = self.tray_menu.addAction("Exit")
        exit_action.triggered.connect(self.quit_app)

    def tray_activated(self, reason):
        if reason == QSystemTrayIcon.ActivationReason.DoubleClick:
            self.show_normal()
            
    def show_normal(self):
        self.showNormal()
        self.activateWindow()

    def setup_ui(self):
        self.central = QWidget()
        self.central.setObjectName("Central")
        self.setCentralWidget(self.central)
        
        self.main_layout = QHBoxLayout(self.central)
        self.main_layout.setContentsMargins(0, 0, 0, 0)
        self.main_layout.setSpacing(0)
        
        self.sidebar = QFrame()
        self.sidebar.setObjectName("Sidebar")
        self.sidebar.setFixedWidth(280)
        self.sidebar_layout = QVBoxLayout(self.sidebar)
        self.sidebar_layout.setContentsMargins(20, 20, 20, 20)
        
        self.lbl_logo = QLabel()
        self.lbl_logo.setStyleSheet("background: transparent;")
        logo_pixmap = get_logo()
        
        if not logo_pixmap.isNull():
            self.lbl_logo.setPixmap(logo_pixmap.scaledToWidth(220, Qt.TransformationMode.SmoothTransformation))
            self.lbl_logo.setAlignment(Qt.AlignmentFlag.AlignCenter)
        else:
            self.lbl_logo.setText("Localcel")
            self.lbl_logo.setStyleSheet("color: #FFFFFF; font-size: 22px; font-weight: bold; font-family: 'Segoe UI Variable Display'; background: transparent;")
            
        self.sidebar_layout.addWidget(self.lbl_logo)
        self.sidebar_layout.addSpacing(15)
        
        self.scroll = QScrollArea()
        self.scroll.setWidgetResizable(True)
        self.scroll_container = QWidget()
        self.scroll_container.setObjectName("ScrollContainer")
        self.scroll_layout = QVBoxLayout(self.scroll_container)
        self.scroll_layout.setAlignment(Qt.AlignmentFlag.AlignTop)
        self.scroll_layout.setContentsMargins(0, 0, 0, 0)
        self.scroll_layout.setSpacing(8)
        self.scroll.setWidget(self.scroll_container)
        self.sidebar_layout.addWidget(self.scroll)
        
        self.btn_new = QPushButton("New App")
        self.btn_new.setObjectName("PrimaryBtn")
        self.btn_new.setCursor(Qt.CursorShape.PointingHandCursor)
        self.btn_new.clicked.connect(self.create_app_dialog)
        self.sidebar_layout.addWidget(self.btn_new)
        
        self.btn_login = QPushButton("CF Login")
        self.btn_login.setCursor(Qt.CursorShape.PointingHandCursor)
        self.btn_login.clicked.connect(self.cf_login)
        self.sidebar_layout.addWidget(self.btn_login)
        
        self.btn_cleanup = QPushButton("Tunnel Manager")
        self.btn_cleanup.setCursor(Qt.CursorShape.PointingHandCursor)
        self.btn_cleanup.clicked.connect(self.tunnel_manager_dialog)
        self.sidebar_layout.addWidget(self.btn_cleanup)
        
        self.main_layout.addWidget(self.sidebar)
        
        self.right_area = QWidget()
        self.right_area.setObjectName("Central")
        self.right_layout = QVBoxLayout(self.right_area)
        self.right_layout.setContentsMargins(25, 25, 25, 25)
        
        self.header = QFrame()
        self.header.setObjectName("HeaderCard")
        self.header_layout = QVBoxLayout(self.header)
        self.header_layout.setContentsMargins(25, 20, 25, 20)
        
        self.lbl_app_title = QLabel("Select an application")
        self.lbl_app_title.setStyleSheet("color: #FFFFFF; font-size: 24px; font-weight: bold; font-family: 'Segoe UI Variable Display'; background: transparent;")
        self.header_layout.addWidget(self.lbl_app_title)
        
        self.lbl_url = QLabel("Not running")
        self.lbl_url.setStyleSheet("color: #A0A0A0; font-size: 14px; background: transparent;")
        self.lbl_url.setOpenExternalLinks(True)
        self.header_layout.addWidget(self.lbl_url)
        
        self.right_layout.addWidget(self.header)
        
        self.ctrl_bar = QWidget()
        self.ctrl_bar.setStyleSheet("background: transparent;")
        self.ctrl_layout = QHBoxLayout(self.ctrl_bar)
        self.ctrl_layout.setContentsMargins(0, 10, 0, 10)
        
        self.btn_start = QPushButton("Start")
        self.btn_start.setObjectName("StartBtn")
        self.btn_start.setCursor(Qt.CursorShape.PointingHandCursor)
        self.btn_start.clicked.connect(self.start_current)
        
        self.btn_stop = QPushButton("Stop")
        self.btn_stop.setObjectName("StopBtn")
        self.btn_stop.setCursor(Qt.CursorShape.PointingHandCursor)
        self.btn_stop.clicked.connect(self.stop_current)
        
        self.btn_edit = QPushButton("Edit App")
        self.btn_edit.setCursor(Qt.CursorShape.PointingHandCursor)
        self.btn_edit.clicked.connect(self.edit_app_dialog)
        
        self.btn_del = QPushButton("Delete App")
        self.btn_del.setCursor(Qt.CursorShape.PointingHandCursor)
        self.btn_del.clicked.connect(self.delete_current)
        
        self.ctrl_layout.addWidget(self.btn_start)
        self.ctrl_layout.addWidget(self.btn_stop)
        self.ctrl_layout.addWidget(self.btn_edit)
        self.ctrl_layout.addStretch()
        self.ctrl_layout.addWidget(self.btn_del)
        
        self.right_layout.addWidget(self.ctrl_bar)
        
        self.log_tabs = QTabWidget()
        self.log_tabs.setObjectName("LogTabs")
        
        self.txt_server = QPlainTextEdit()
        self.txt_server.setReadOnly(True)
        self.log_tabs.addTab(self.txt_server, "Server Logs")
        
        self.txt_tunnel = QPlainTextEdit()
        self.txt_tunnel.setReadOnly(True)
        self.log_tabs.addTab(self.txt_tunnel, "Tunnel Logs")
        
        self.right_layout.addWidget(self.log_tabs)
        self.main_layout.addWidget(self.right_area)

    def refresh_app_list(self):
        while self.scroll_layout.count():
            child = self.scroll_layout.takeAt(0)
            if child.widget(): child.widget().deleteLater()
            
        self.cards = {}
        apps = AppManager.get_apps()
        for app in apps:
            card = AppCard(app, self.select_app)
            self.scroll_layout.addWidget(card)
            self.cards[app.name] = card
            
        if self.selected_app:
            for name, card in self.cards.items():
                card.set_selected(name == self.selected_app.name)

    def select_app(self, app_conf):
        self.selected_app = app_conf
        self.lbl_app_title.setText(f"{app_conf.name}")
        
        for name, card in self.cards.items():
            card.set_selected(name == app_conf.name)
            
        self.update_ui_state()
        self.txt_server.clear()
        self.txt_tunnel.clear()
        
        for suffix, box in [("", self.txt_server), ("_tunnel", self.txt_tunnel)]:
            p = LOGS_DIR / f"{app_conf.name}{suffix}.log"
            if p.exists():
                with open(p, 'r', encoding="utf-8", errors="replace") as f:
                    content = "".join(f.readlines()[-100:])
                    box.appendPlainText(content.strip())

    def update_ui_state(self):
        if not self.selected_app: return
        name = self.selected_app.name
        is_running = name in self.engine_servers and self.engine_servers[name].is_running()
        
        self.btn_start.setEnabled(not is_running)
        self.btn_stop.setEnabled(is_running)
        
        if is_running:
            tun = self.engine_tunnels.get(name)
            url = tun.url if tun and tun.url else "Tunnelling..."
            if url.startswith("http"):
                self.lbl_url.setText(f'<a href="{url}" style="color: #60CDFF; text-decoration: none; background: transparent;">{url}</a>')
            else:
                self.lbl_url.setText(f'<span style="color: #60CDFF; background: transparent;">{url}</span>')
        else:
            self.lbl_url.setText('<span style="color: #A0A0A0; background: transparent;">Stopped</span>')

    def start_current(self):
        if not self.selected_app: return
        app = self.selected_app
        
        if not get_executable_path("cloudflared"):
            reply = QMessageBox.question(self, "Missing Dependency", "cloudflared is not installed. Attempt to install via winget?", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
            if reply == QMessageBox.StandardButton.Yes:
                if not CloudflareHelper.install_cloudflared():
                    QMessageBox.critical(self, "Error", "Auto-install failed. Please install manually.")
                    return
            else: return
        
        if not get_executable_path("node"):
            QMessageBox.critical(self, "Error", "Node.js not found in PATH.")
            return

        srv = ManagedProcess(app.name)
        tun = ManagedProcess(app.name, is_tunnel=True)
        self.engine_servers[app.name] = srv
        self.engine_tunnels[app.name] = tun
        
        srv.start([get_executable_path("node"), app.entry], str(APPS_DIR / app.name), 
                  lambda line: self.signals.log_appended.emit(app.name, "server", line))
        
        if app.domain:
            try:
                cfg = CloudflareHelper.setup_named_tunnel(app.name, app.port, app.domain)
                cmd = [get_executable_path("cloudflared"), "tunnel", "--config", str(cfg), "run"]
                tun.url = f"https://{app.domain}"
            except Exception as e:
                QMessageBox.critical(self, "Tunnel Error", str(e))
                return
        else:
            cmd = [get_executable_path("cloudflared"), "tunnel", "--url", f"http://localhost:{app.port}"]

        tun.start(cmd, str(BASE_DIR), lambda line: self.signals.log_appended.emit(app.name, "tunnel", line))

    def stop_current(self):
        if not self.selected_app: return
        name = self.selected_app.name
        if name in self.engine_servers: self.engine_servers[name].stop()
        if name in self.engine_tunnels: self.engine_tunnels[name].stop()

    def stop_all_servers(self):
        for name, srv in self.engine_servers.items():
            if srv.is_running(): srv.stop()
        for name, tun in self.engine_tunnels.items():
            if tun.is_running(): tun.stop()

    def quit_app(self):
        running_apps = [name for name, srv in self.engine_servers.items() if srv.is_running()]
        if running_apps:
            reply = QMessageBox.warning(
                self, 
                "Servers Running", 
                f"The following servers are still running:\n\n{', '.join(running_apps)}\n\nClosing Localcel will stop these servers. Are you sure you want to exit?",
                QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                QMessageBox.StandardButton.No
            )
            if reply == QMessageBox.StandardButton.Yes:
                self.stop_all_servers()
                QApplication.quit()
        else:
            self.stop_all_servers()
            QApplication.quit()

    def closeEvent(self, event):
        running_apps = [name for name, srv in self.engine_servers.items() if srv.is_running()]
        if running_apps:
            self.hide()
            self.tray_icon.showMessage(
                "Localcel Background Mode",
                "Localcel has been minimized to the system tray to keep your servers active.",
                QSystemTrayIcon.MessageIcon.Information,
                2000
            )
            event.ignore()
        else:
            self.stop_all_servers()
            QApplication.quit()
            event.accept()
            
    def on_commit_data_request(self, manager):
        running_apps = [name for name, srv in self.engine_servers.items() if srv.is_running()]
        if running_apps:
            manager.cancel()
        else:
            self.stop_all_servers()

    def delete_current(self):
        if not self.selected_app: return
        reply = QMessageBox.question(self, "Confirm", "Delete this app and all its files?", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
        if reply == QMessageBox.StandardButton.Yes:
            self.stop_current()
            AppManager.delete_app(self.selected_app.name)
            self.selected_app = None
            self.refresh_app_list()
            self.txt_server.clear()
            self.txt_tunnel.clear()
            self.lbl_app_title.setText("Select an application")
            self.lbl_url.setText("Not running")
            self.update_ui_state()

    def append_log(self, app_name, log_type, line):
        if self.selected_app and self.selected_app.name == app_name:
            box = self.txt_server if log_type == "server" else self.txt_tunnel
            box.appendPlainText(line.strip())

    def cf_login(self):
        proc = subprocess.Popen([get_executable_path("cloudflared"), "tunnel", "login"])
        def wait_and_show():
            proc.wait()
            self.signals.tunnel_ready.emit()
        threading.Thread(target=wait_and_show, daemon=True).start()

    def check_loop(self):
        running_apps = []
        for name, card in self.cards.items():
            is_run = name in self.engine_servers and self.engine_servers.get(name).is_running()
            card.update_status(is_run)
            if is_run:
                running_apps.append(name)
                
        if running_apps:
            self.tray_icon.setToolTip(f"Localcel - Running:\n" + "\n".join(running_apps))
        else:
            self.tray_icon.setToolTip("Localcel - No running servers")
            
        self.update_ui_state()

    def create_app_dialog(self):
        dlg = QDialog(self)
        dlg.setWindowTitle("New Application")
        dlg.setWindowIcon(get_icon())
        dlg.resize(400, 350)
        apply_translucent_acrylic(dlg)
        
        layout = QVBoxLayout(dlg)
        frame = QFrame()
        frame.setObjectName("HeaderCard")
        frame_layout = QVBoxLayout(frame)
        
        lbl = QLabel("Create New App")
        lbl.setStyleSheet("font-size: 20px; font-weight: bold; font-family: 'Segoe UI Variable Display'; background: transparent;")
        frame_layout.addWidget(lbl)
        
        e_name = QLineEdit()
        e_name.setPlaceholderText("App Name")
        frame_layout.addWidget(e_name)
        
        e_port = QLineEdit()
        e_port.setPlaceholderText("Port (e.g. 3000)")
        e_port.setText("3000")
        frame_layout.addWidget(e_port)
        
        e_dom = QLineEdit()
        e_dom.setPlaceholderText("Custom Domain (Optional)")
        frame_layout.addWidget(e_dom)
        
        btn_save = QPushButton("Create App")
        btn_save.setObjectName("PrimaryBtn")
        btn_save.setCursor(Qt.CursorShape.PointingHandCursor)
        
        def save():
            name, port = e_name.text().strip(), e_port.text().strip()
            if name and port.isdigit():
                AppManager.create_app(name, int(port), e_dom.text().strip())
                self.refresh_app_list()
                dlg.accept()
                
        btn_save.clicked.connect(save)
        frame_layout.addWidget(btn_save)
        layout.addWidget(frame)
        dlg.exec()

    def edit_app_dialog(self):
        if not self.selected_app: return
        app = self.selected_app
        dlg = QDialog(self)
        dlg.setWindowTitle(f"Edit {app.name}")
        dlg.setWindowIcon(get_icon())
        dlg.resize(400, 350)
        apply_translucent_acrylic(dlg)
        
        layout = QVBoxLayout(dlg)
        frame = QFrame()
        frame.setObjectName("HeaderCard")
        frame_layout = QVBoxLayout(frame)
        
        lbl = QLabel(f"Edit {app.name}")
        lbl.setStyleSheet("font-size: 20px; font-weight: bold; font-family: 'Segoe UI Variable Display'; background: transparent;")
        frame_layout.addWidget(lbl)
        
        lbl_port = QLabel("Port Number")
        lbl_port.setStyleSheet("background: transparent;")
        frame_layout.addWidget(lbl_port)
        
        e_port = QLineEdit()
        e_port.setText(str(app.port))
        frame_layout.addWidget(e_port)
        
        lbl_dom = QLabel("Custom Domain")
        lbl_dom.setStyleSheet("background: transparent;")
        frame_layout.addWidget(lbl_dom)
        
        e_dom = QLineEdit()
        e_dom.setText(app.domain or "")
        frame_layout.addWidget(e_dom)
        
        btn_save = QPushButton("Save Changes")
        btn_save.setObjectName("PrimaryBtn")
        btn_save.setCursor(Qt.CursorShape.PointingHandCursor)
        
        def save():
            port = e_port.text().strip()
            if port.isdigit():
                was_running = app.name in self.engine_servers and self.engine_servers.get(app.name).is_running()
                if was_running: self.stop_current()
                AppManager.update_app(app.name, int(port), e_dom.text().strip())
                self.refresh_app_list()
                self.selected_app = next((a for a in AppManager.get_apps() if a.name == app.name), None)
                if was_running: self.start_current()
                dlg.accept()
                
        btn_save.clicked.connect(save)
        frame_layout.addWidget(btn_save)
        layout.addWidget(frame)
        dlg.exec()

    def tunnel_manager_dialog(self):
        dlg = QDialog(self)
        dlg.setWindowTitle("Cloudflare Tunnels")
        dlg.setWindowIcon(get_icon())
        dlg.resize(600, 450)
        apply_translucent_acrylic(dlg)
        
        layout = QVBoxLayout(dlg)
        frame = QFrame()
        frame.setObjectName("HeaderCard")
        frame_layout = QVBoxLayout(frame)
        
        lbl = QLabel("Cloudflare Tunnels")
        lbl.setStyleSheet("font-size: 20px; font-weight: bold; font-family: 'Segoe UI Variable Display'; background: transparent;")
        frame_layout.addWidget(lbl)
        
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        container = QWidget()
        container.setObjectName("ScrollContainer")
        scroll_layout = QVBoxLayout(container)
        scroll_layout.setAlignment(Qt.AlignmentFlag.AlignTop)
        scroll.setWidget(container)
        frame_layout.addWidget(scroll)
        
        def refresh_list():
            while scroll_layout.count():
                child = scroll_layout.takeAt(0)
                if child.widget(): child.widget().deleteLater()
                
            tunnels = CloudflareHelper.list_tunnels()
            if not tunnels:
                l = QLabel("No active tunnels found or not logged in.")
                l.setStyleSheet("color: #A0A0A0; background: transparent;")
                scroll_layout.addWidget(l)
                return
                
            for t in tunnels:
                row = QFrame()
                row.setStyleSheet("background-color: rgba(30, 30, 30, 150); border: 1px solid rgba(255, 255, 255, 20); border-radius: 4px;")
                row_layout = QHBoxLayout(row)
                
                t_lbl = QLabel(f"{t['name']}  •  {t['id'][:8]}")
                t_lbl.setStyleSheet("border: none; font-weight: bold; background: transparent;")
                
                btn_del = QPushButton("Delete")
                btn_del.setCursor(Qt.CursorShape.PointingHandCursor)
                btn_del.setStyleSheet("background-color: rgba(196, 43, 28, 40); color: white; border: 1px solid rgba(196, 43, 28, 80); padding: 4px 12px; border-radius: 4px;")
                
                def make_del(checked, tid=t['id']):
                    reply = QMessageBox.question(dlg, "Confirm", f"Delete tunnel {tid}?", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
                    if reply == QMessageBox.StandardButton.Yes:
                        CloudflareHelper.delete_tunnel(tid)
                        refresh_list()
                
                btn_del.clicked.connect(make_del)
                row_layout.addWidget(t_lbl)
                row_layout.addStretch()
                row_layout.addWidget(btn_del)
                scroll_layout.addWidget(row)
                
        refresh_list()
        btn_refresh = QPushButton("Refresh Status")
        btn_refresh.setCursor(Qt.CursorShape.PointingHandCursor)
        btn_refresh.clicked.connect(refresh_list)
        frame_layout.addWidget(btn_refresh)
        
        layout.addWidget(frame)
        dlg.exec()

if __name__ == "__main__":
    app = QApplication(sys.argv)
    app.setQuitOnLastWindowClosed(False)
    
    workspace_path = get_or_choose_workspace()
    initialize_workspace(workspace_path)
    
    if platform.system() == "Windows":
        try:
            import ctypes
            myappid = 'localcel.gui.app.1' 
            ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID(myappid)
        except Exception:
            pass

    app.setWindowIcon(get_icon())
    app.setStyleSheet(QSS) 
    
    gui = LocalcelGUI()
    gui.show()
    sys.exit(app.exec())
'''

# ==========================================
# MODULE: DROPPER BOOTSTRAPPER
# ==========================================

def get_host_python():
    """Aggressively searches the host system for a valid Python installation."""
    exe = shutil.which("python") or shutil.which("pythonw") or shutil.which("python3")
    if exe: return exe
    
    # Check default Windows installation paths via LocalAppData
    local_app_data = os.environ.get("LocalAppData", "")
    if local_app_data:
        base_dir = os.path.join(local_app_data, "Programs", "Python")
        if os.path.exists(base_dir):
            for folder in sorted(os.listdir(base_dir), reverse=True):
                p = os.path.join(base_dir, folder, "python.exe")
                if os.path.exists(p): return p
                
    # Fallback checking root C:\
    if os.path.exists("C:\\"):
        try:
            for folder in sorted(os.listdir("C:\\"), reverse=True):
                if folder.lower().startswith("python"):
                    p = os.path.join("C:\\", folder, "python.exe")
                    if os.path.exists(p): return p
        except Exception: pass
        
    return None

if __name__ == "__main__":
    # If the app was compiled using PyInstaller, it runs this Dropper sequence
    if getattr(sys, 'frozen', False):
        python_exe = get_host_python()
        if not python_exe:
            import ctypes
            # 0x24 = MB_YESNO (0x04) + MB_ICONQUESTION (0x20)
            res = ctypes.windll.user32.MessageBoxW(0, "Python 3 is required to run Localcel but was not found.\n\nWould you like to automatically install it now?", "Python Missing", 0x24)
            
            if res == 6: # IDYES
                # Open a visible console for the user to see the winget installation progress
                creation_flags = subprocess.CREATE_NEW_CONSOLE if sys.platform == "win32" else 0
                subprocess.run(["winget", "install", "--id", "Python.Python.3.12", "-e", "--accept-package-agreements", "--accept-source-agreements"], creationflags=creation_flags)
                
                # Re-check for Python in the standard install paths
                python_exe = get_host_python()
                
            if not python_exe:
                ctypes.windll.user32.MessageBoxW(0, "Automatic installation failed or was cancelled.\n\nPlease install Python 3 manually from python.org to run Localcel.", "Dependency Error", 0x10)
                sys.exit(1)
        
        # Dump the payload string to the user's temp directory
        h = hashlib.md5(PAYLOAD.encode()).hexdigest()[:8]
        script_path = os.path.join(tempfile.gettempdir(), f"localcel_runner_{h}.py")
        
        with open(script_path, "w", encoding="utf-8") as f:
            f.write(PAYLOAD)
            
        # Suppress the background terminal on Windows when passing control to the host python
        flags = 0x08000000 if sys.platform == "win32" else 0 
        
        # Execute the dumped GUI app using the machine's python.
        sys.exit(subprocess.call([python_exe, script_path] + sys.argv[1:], creationflags=flags))
        
    # If it's just being run natively via "python localcel.py", execute the string directly
    else:
        exec(PAYLOAD, globals())