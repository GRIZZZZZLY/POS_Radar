import base64
import ctypes
import logging
import os
import queue
import socket
import subprocess
import sys
import threading
import time
from datetime import datetime
from pathlib import Path

import pystray
from PIL import Image, ImageDraw


# ============================================================
# CONFIG
# ============================================================

APP_NAME = "Posiflora UEM Monitor"

LOG_DIR = Path(r"C:\Posiflora")
LOG_FILE = LOG_DIR / "uem_monitor.log"

CHECK_INTERVAL_SECONDS = 300
RECHECK_DELAY_SECONDS = 10
ALERT_COOLDOWN_SECONDS = 300

UEM_AGENT_PATH = r"C:\Program Files\UEM\Agent\bin\uema.exe"
UEM_UPDATER_PATH = r"C:\Program Files\UEM\Updater\bin\uemu.exe"

UEM_SERVICES = [
    {"title": "UEM Agent", "path": UEM_AGENT_PATH},
    {"title": "UEM Updater", "path": UEM_UPDATER_PATH},
]

LOCAL_PORTS = [5050, 5051]

MUTEX_NAME = "Local\\PosifloraUemPythonMonitor"


# ============================================================
# GLOBAL STATE
# ============================================================

LOG_DIR.mkdir(parents=True, exist_ok=True)

logging.basicConfig(
    filename=str(LOG_FILE),
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    encoding="utf-8",
)

ui_queue = queue.Queue()
force_check_event = threading.Event()
stop_event = threading.Event()

root_window = None
tray_icon = None
mutex_handle = None

current_status_text = "Запуск..."
current_problems = []
current_last_check = "Еще не выполнялась"
current_state = "unknown"


# ============================================================
# LOG / UI
# ============================================================

def send_ui_event(event_type: str, **kwargs) -> None:
    try:
        ui_queue.put({"type": event_type, **kwargs})
    except Exception:
        pass


def log_message(level: str, message: str) -> None:
    if level == "INFO":
        logging.info(message)
    elif level == "WARN":
        logging.warning(message)
    elif level == "ERROR":
        logging.error(message)
    else:
        logging.info(message)

    send_ui_event(
        "log",
        time=datetime.now().strftime("%H:%M:%S"),
        level=level,
        message=message,
    )


def log_info(message: str) -> None:
    log_message("INFO", message)


def log_warn(message: str) -> None:
    log_message("WARN", message)


def log_error(message: str) -> None:
    log_message("ERROR", message)


def set_status(message: str) -> None:
    send_ui_event("status", message=message)


def set_last_check_now() -> None:
    send_ui_event(
        "last_check",
        value=datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
    )


def set_problems(problems: list[str]) -> None:
    send_ui_event("problems", problems=problems)


def set_state(state: str) -> None:
    send_ui_event("state", state=state)


def show_problem_popup(title: str, message: str) -> None:
    send_ui_event("popup", title=title, message=message)


# ============================================================
# WINDOWS HELPERS
# ============================================================

def is_admin() -> bool:
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except Exception:
        return False


def acquire_single_instance_mutex() -> None:
    global mutex_handle

    kernel32 = ctypes.windll.kernel32
    mutex_handle = kernel32.CreateMutexW(None, False, MUTEX_NAME)

    last_error = kernel32.GetLastError()

    if last_error == 183:
        log_warn("Another instance already running. Exit.")
        sys.exit(0)


def run_powershell(script: str, timeout: int = 30) -> subprocess.CompletedProcess:
    full_script = """
$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
""" + script

    encoded = base64.b64encode(full_script.encode("utf-16le")).decode("ascii")

    return subprocess.run(
        [
            "powershell.exe",
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-EncodedCommand",
            encoded,
        ],
        capture_output=True,
        text=True,
        timeout=timeout,
        encoding="utf-8",
        errors="replace",
        creationflags=subprocess.CREATE_NO_WINDOW,
    )


# ============================================================
# SERVICE CHECKS
# ============================================================

def get_service_names_by_exe_path(exe_path: str) -> list[str]:
    safe_path = exe_path.replace("'", "''").lower()

    script = f"""
$target = '{safe_path}'

Get-CimInstance Win32_Service |
    Where-Object {{
        $_.PathName -and $_.PathName.ToLower().Contains($target)
    }} |
    ForEach-Object {{
        $_.Name
    }}
"""

    try:
        result = run_powershell(script, timeout=30)
        return [line.strip() for line in result.stdout.splitlines() if line.strip()]
    except Exception as exc:
        log_error(f"Failed to find service by path {exe_path}: {exc}")
        return []


def get_service_status(service_name: str) -> str:
    safe_name = service_name.replace("'", "''")

    script = f"""
$svc = Get-Service -Name '{safe_name}' -ErrorAction SilentlyContinue

if ($svc) {{
    $svc.Status.ToString()
}} else {{
    "NOT_FOUND"
}}
"""

    try:
        result = run_powershell(script, timeout=15)
        return result.stdout.strip()
    except Exception as exc:
        log_error(f"Failed to get service status {service_name}: {exc}")
        return "ERROR"


def start_service(service_name: str) -> bool:
    safe_name = service_name.replace("'", "''")

    script = f"""
try {{
    Start-Service -Name '{safe_name}' -ErrorAction Stop
    Start-Sleep -Seconds 3

    $svc = Get-Service -Name '{safe_name}' -ErrorAction Stop
    $svc.Status.ToString()
}} catch {{
    "ERROR: $($_.Exception.Message)"
}}
"""

    try:
        result = run_powershell(script, timeout=30)
        output = result.stdout.strip()

        if "Running" in output:
            log_info(f"Service started: {service_name}")
            return True

        log_error(f"Service start failed: {service_name}. Output: {output}")
        return False
    except Exception as exc:
        log_error(f"Service start exception {service_name}: {exc}")
        return False


def restart_service(service_name: str) -> bool:
    safe_name = service_name.replace("'", "''")

    script = f"""
try {{
    Restart-Service -Name '{safe_name}' -Force -ErrorAction Stop
    Start-Sleep -Seconds 5

    $svc = Get-Service -Name '{safe_name}' -ErrorAction Stop
    $svc.Status.ToString()
}} catch {{
    "ERROR: $($_.Exception.Message)"
}}
"""

    try:
        result = run_powershell(script, timeout=45)
        output = result.stdout.strip()

        if "Running" in output:
            log_info(f"Service restarted: {service_name}")
            return True

        log_error(f"Service restart failed: {service_name}. Output: {output}")
        return False
    except Exception as exc:
        log_error(f"Service restart exception {service_name}: {exc}")
        return False


def check_uem_services() -> list[str]:
    problems = []

    set_status("Проверка служб UEM...")

    for item in UEM_SERVICES:
        title = item["title"]
        path = item["path"]

        if not os.path.exists(path):
            problem = f"Файл службы не найден: {title} / {path}"
            problems.append(problem)
            log_error(problem)
            continue

        service_names = get_service_names_by_exe_path(path)

        if not service_names:
            problem = f"Служба не найдена по пути: {title} / {path}"
            problems.append(problem)
            log_error(problem)
            continue

        for service_name in service_names:
            status = get_service_status(service_name)

            if status == "Running":
                log_info(f"Service OK: {title} / {service_name}")
                continue

            log_warn(f"Service not running: {title} / {service_name}. Status: {status}")

            if not start_service(service_name):
                problem = f"Служба не запущена: {title} / {service_name}"
                problems.append(problem)
                log_error(problem)

    return problems


def restart_all_uem_services() -> None:
    set_status("Перезапуск служб UEM...")
    log_warn("Restarting all UEM services")

    for item in UEM_SERVICES:
        title = item["title"]
        path = item["path"]

        service_names = get_service_names_by_exe_path(path)

        if not service_names:
            log_error(f"Cannot restart. Service not found: {title} / {path}")
            continue

        for service_name in service_names:
            restart_service(service_name)


# ============================================================
# PORT CHECKS
# ============================================================

def check_local_port(port: int) -> bool:
    script = f"""
$result = Get-NetTCPConnection -LocalPort {port} -State Listen -ErrorAction SilentlyContinue

if ($result) {{
    "OK"
}} else {{
    "FAIL"
}}
"""

    try:
        result = run_powershell(script, timeout=15)
        return "OK" in result.stdout.strip()
    except Exception as exc:
        log_error(f"Local port check error {port}: {exc}")
        return False


def check_local_ports() -> list[str]:
    problems = []

    set_status("Проверка локальных портов UEM...")

    for port in LOCAL_PORTS:
        if check_local_port(port):
            log_info(f"Local port OK: {port}")
        else:
            problem = f"Локальный порт UEM не слушается: {port}"
            problems.append(problem)
            log_error(problem)

    return problems


# ============================================================
# CLOUD CHECK VIA SERVICE PID
# ============================================================

def check_uem_cloud_via_service() -> list[str]:
    problems = []

    set_status("Проверка связи UEM с облаком через службу...")

    script = r"""
$svc = Get-CimInstance Win32_Service |
    Where-Object {
        $_.PathName -and $_.PathName.ToLower().Contains("uema.exe")
    } |
    Select-Object -First 1

if (-not $svc) {
    "NO_SERVICE"
    return
}

if (-not $svc.ProcessId -or $svc.ProcessId -eq 0) {
    "NO_PROCESS"
    return
}

$pidValue = [int]$svc.ProcessId

$conns = Get-NetTCPConnection -ErrorAction SilentlyContinue |
    Where-Object {
        $_.OwningProcess -eq $pidValue -and
        $_.State -eq "Established" -and
        $_.RemotePort -in 443,1883
    }

if ($conns) {
    "OK"
    $conns | ForEach-Object {
        "CONNECTION $($_.RemoteAddress):$($_.RemotePort)"
    }
} else {
    "NO_CONNECTION"
}
"""

    try:
        result = run_powershell(script, timeout=25)
        output = result.stdout.strip()

        if "NO_SERVICE" in output:
            problem = "Служба UEM Agent не найдена"
            problems.append(problem)
            log_error(problem)

        elif "NO_PROCESS" in output:
            problem = "Служба UEM Agent найдена, но процесс не запущен"
            problems.append(problem)
            log_error(problem)

        elif "NO_CONNECTION" in output:
            problem = "UEM Agent не имеет активного соединения с облаком АТОЛ"
            problems.append(problem)
            log_error(problem)

        elif "OK" in output:
            log_info("UEM Agent имеет активное соединение с облаком")
            for line in output.splitlines():
                if line.startswith("CONNECTION"):
                    log_info(f"UEM cloud {line}")
        else:
            problem = f"Не удалось определить состояние связи UEM с облаком. Output: {output}"
            problems.append(problem)
            log_error(problem)

    except Exception as exc:
        problem = f"Ошибка проверки связи UEM с облаком: {exc}"
        problems.append(problem)
        log_error(problem)

    return problems


# ============================================================
# MAIN MONITOR LOGIC
# ============================================================

def run_full_check() -> list[str]:
    problems = []

    problems.extend(check_uem_services())
    problems.extend(check_local_ports())
    problems.extend(check_uem_cloud_via_service())

    return problems


def format_problems_message(problems: list[str]) -> str:
    lines = [
        "Проблема с UEM / связью с облаком АТОЛ.",
        "",
        "После перезапуска служб проблема не устранена:",
        "",
    ]

    for problem in problems:
        lines.append(f"- {problem}")

    lines.extend(["", f"Лог: {LOG_FILE}"])

    return "\n".join(lines)


def wait_interval_or_manual_check(seconds: int) -> None:
    force_check_event.wait(timeout=seconds)
    force_check_event.clear()


def monitor_loop() -> None:
    log_info("========== UEM MONITOR START ==========")
    set_status("Монитор запущен")
    set_state("unknown")

    if not is_admin():
        set_state("error")
        set_status("Нет прав администратора")
        log_error("Administrator rights required")

        show_problem_popup(
            APP_NAME,
            "Монитор запущен без прав администратора.\n"
            "Перезапуск служб невозможен.\n\n"
            f"Лог: {LOG_FILE}",
        )

        return

    last_alert_time = 0

    while not stop_event.is_set():
        try:
            set_state("unknown")
            set_status("Запуск проверки...")
            set_last_check_now()

            log_info("----- CHECK START -----")

            problems = run_full_check()
            set_problems(problems)

            if not problems:
                set_state("ok")
                set_status("Все проверки успешно пройдены")
                log_info("All checks OK")
                log_info("----- CHECK END -----")

                wait_interval_or_manual_check(CHECK_INTERVAL_SECONDS)
                continue

            set_state("warning")
            set_status("Обнаружены проблемы, выполняется перезапуск служб...")

            for problem in problems:
                log_warn(problem)

            restart_all_uem_services()

            set_status(f"Ожидание {RECHECK_DELAY_SECONDS} сек. перед повторной проверкой...")
            time.sleep(RECHECK_DELAY_SECONDS)

            set_status("Повторная проверка после перезапуска...")

            recheck_problems = run_full_check()
            set_problems(recheck_problems)

            if not recheck_problems:
                set_state("ok")
                set_status("Проблемы устранены после перезапуска")
                log_info("Problems fixed after restart")
                log_info("----- CHECK END -----")

                wait_interval_or_manual_check(CHECK_INTERVAL_SECONDS)
                continue

            set_state("error")
            set_status("Проблема не устранена после перезапуска")

            for problem in recheck_problems:
                log_error(problem)

            now = time.time()

            if now - last_alert_time >= ALERT_COOLDOWN_SECONDS:
                show_problem_popup(
                    "Проблема UEM / АТОЛ",
                    format_problems_message(recheck_problems),
                )
                last_alert_time = now
            else:
                log_warn("Popup skipped because cooldown is active")

            log_info("----- CHECK END -----")

            wait_interval_or_manual_check(CHECK_INTERVAL_SECONDS)

        except Exception as exc:
            set_state("error")
            set_status("Ошибка в основном цикле монитора")
            log_error(f"Main loop error: {exc}")

            show_problem_popup(
                "Ошибка монитора UEM",
                f"Ошибка в основном цикле монитора:\n{exc}\n\nЛог: {LOG_FILE}",
            )

            wait_interval_or_manual_check(CHECK_INTERVAL_SECONDS)

    log_info("========== UEM MONITOR STOP ==========")


# ============================================================
# TRAY
# ============================================================

def create_tray_icon_image(state: str) -> Image.Image:
    image = Image.new("RGB", (64, 64), "white")
    draw = ImageDraw.Draw(image)

    if state == "ok":
        color = "#1fa64a"
    elif state == "warning":
        color = "#f0a000"
    elif state == "error":
        color = "#d93025"
    else:
        color = "#808080"

    draw.ellipse((6, 6, 58, 58), fill=color)
    draw.ellipse((20, 20, 44, 44), fill="white")

    return image


def build_tray_title() -> str:
    if current_state == "ok":
        state_text = "OK"
    elif current_state == "warning":
        state_text = "Проверка / восстановление"
    elif current_state == "error":
        state_text = f"Проблем: {len(current_problems)}"
    else:
        state_text = "Запуск"

    return (
        f"{APP_NAME}\n"
        f"Состояние: {state_text}\n"
        f"Этап: {current_status_text}\n"
        f"Проверка: {current_last_check}"
    )


def update_tray() -> None:
    if tray_icon is None:
        return

    try:
        tray_icon.icon = create_tray_icon_image(current_state)
        tray_icon.title = build_tray_title()
    except Exception as exc:
        log_error(f"Tray update error: {exc}")


def show_status_window(icon=None, item=None) -> None:
    if root_window is not None:
        root_window.after(0, show_status_window_safe)


def show_status_window_safe() -> None:
    if root_window is None:
        return

    root_window.deiconify()
    root_window.lift()
    root_window.focus_force()


def hide_status_window() -> None:
    if root_window is not None:
        root_window.withdraw()


def request_manual_check(icon=None, item=None) -> None:
    log_info("Manual check requested")
    set_status("Запрошена ручная проверка...")
    force_check_event.set()


def open_log_file(icon=None, item=None) -> None:
    try:
        subprocess.Popen(
            ["notepad.exe", str(LOG_FILE)],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NO_WINDOW,
        )
    except Exception as exc:
        log_error(f"Cannot open log: {exc}")


def exit_application(icon=None, item=None) -> None:
    stop_event.set()

    try:
        if root_window is not None:
            root_window.after(0, root_window.destroy)
    except Exception:
        pass

    try:
        if tray_icon is not None:
            tray_icon.stop()
    except Exception:
        pass


def run_tray_icon() -> None:
    global tray_icon

    menu = pystray.Menu(
        pystray.MenuItem("Открыть окно статуса", show_status_window, default=True),
        pystray.MenuItem("Проверить сейчас", request_manual_check),
        pystray.MenuItem("Открыть лог", open_log_file),
        pystray.Menu.SEPARATOR,
        pystray.MenuItem("Выход", exit_application),
    )

    tray_icon = pystray.Icon(
        "PosifloraUemMonitor",
        create_tray_icon_image("unknown"),
        build_tray_title(),
        menu,
    )

    log_info("Tray icon starting")
    tray_icon.run()
    log_info("Tray icon stopped")


# ============================================================
# POPUP
# ============================================================

def show_problem_window(title: str, message: str) -> None:
    if root_window is None:
        return

    root_window.after(0, lambda: show_problem_window_safe(title, message))


def show_problem_window_safe(title: str, message: str) -> None:
    import tkinter as tk

    popup = tk.Toplevel(root_window)
    popup.title(title)
    popup.attributes("-topmost", True)
    popup.resizable(False, False)

    width = 470
    height = 280

    screen_width = popup.winfo_screenwidth()
    screen_height = popup.winfo_screenheight()

    x = screen_width - width - 20
    y = screen_height - height - 70

    popup.geometry(f"{width}x{height}+{x}+{y}")

    frame = tk.Frame(
        popup,
        bg="#fff4d6",
        padx=12,
        pady=12,
        relief="solid",
        borderwidth=1,
    )
    frame.pack(fill="both", expand=True)

    tk.Label(
        frame,
        text=title,
        bg="#fff4d6",
        fg="#8a4b00",
        font=("Segoe UI", 11, "bold"),
        anchor="w",
        justify="left",
    ).pack(fill="x")

    tk.Label(
        frame,
        text=message,
        bg="#fff4d6",
        fg="#222222",
        font=("Segoe UI", 9),
        anchor="nw",
        justify="left",
        wraplength=435,
    ).pack(fill="both", expand=True, pady=(8, 8))

    bottom = tk.Frame(frame, bg="#fff4d6")
    bottom.pack(fill="x")

    def recheck_now() -> None:
        popup.destroy()
        request_manual_check()

    tk.Button(
        bottom,
        text="Проверить сейчас",
        command=recheck_now,
        width=18,
    ).pack(side="left")

    tk.Button(
        bottom,
        text="Закрыть",
        command=popup.destroy,
        width=12,
    ).pack(side="right")


# ============================================================
# STATUS WINDOW
# ============================================================

def run_tk_window() -> None:
    global root_window
    global current_status_text
    global current_problems
    global current_last_check
    global current_state

    import tkinter as tk
    from tkinter import scrolledtext

    root = tk.Tk()
    root_window = root

    root.title(f"{APP_NAME} — статус")
    root.geometry("820x580")
    root.minsize(760, 520)

    status_var = tk.StringVar(value=current_status_text)
    last_check_var = tk.StringVar(value=current_last_check)
    state_var = tk.StringVar(value="Запуск")
    problems_var = tk.StringVar(value="Проблем нет")

    main_frame = tk.Frame(root, padx=12, pady=12)
    main_frame.pack(fill="both", expand=True)

    tk.Label(
        main_frame,
        text=APP_NAME,
        font=("Segoe UI", 14, "bold"),
        anchor="w",
    ).pack(fill="x")

    status_frame = tk.LabelFrame(main_frame, text="Статус", padx=10, pady=8)
    status_frame.pack(fill="x", pady=(10, 8))

    tk.Label(status_frame, text="Состояние:", anchor="w").grid(row=0, column=0, sticky="w")
    tk.Label(status_frame, textvariable=state_var, anchor="w").grid(row=0, column=1, sticky="w", padx=(10, 0))

    tk.Label(status_frame, text="Текущий этап:", anchor="w").grid(row=1, column=0, sticky="w", pady=(6, 0))
    tk.Label(status_frame, textvariable=status_var, anchor="w").grid(row=1, column=1, sticky="w", padx=(10, 0), pady=(6, 0))

    tk.Label(status_frame, text="Последняя проверка:", anchor="w").grid(row=2, column=0, sticky="w", pady=(6, 0))
    tk.Label(status_frame, textvariable=last_check_var, anchor="w").grid(row=2, column=1, sticky="w", padx=(10, 0), pady=(6, 0))

    status_frame.columnconfigure(1, weight=1)

    problems_frame = tk.LabelFrame(main_frame, text="Проблемы", padx=10, pady=8)
    problems_frame.pack(fill="x", pady=(0, 8))

    tk.Label(
        problems_frame,
        textvariable=problems_var,
        anchor="w",
        justify="left",
        fg="#9b1c1c",
    ).pack(fill="x")

    log_frame = tk.LabelFrame(main_frame, text="Лог", padx=10, pady=8)
    log_frame.pack(fill="both", expand=True)

    log_text = scrolledtext.ScrolledText(
        log_frame,
        height=16,
        state="disabled",
        font=("Consolas", 9),
    )
    log_text.pack(fill="both", expand=True)

    buttons_frame = tk.Frame(main_frame)
    buttons_frame.pack(fill="x", pady=(10, 0))

    def append_log(line: str) -> None:
        log_text.configure(state="normal")
        log_text.insert("end", line + "\n")
        log_text.see("end")
        log_text.configure(state="disabled")

    def refresh_state_text() -> None:
        if current_state == "ok":
            state_var.set("OK")
        elif current_state == "warning":
            state_var.set("Проверка / восстановление")
        elif current_state == "error":
            state_var.set("Ошибка")
        else:
            state_var.set("Запуск")

    tk.Button(
        buttons_frame,
        text="Проверить сейчас",
        command=request_manual_check,
        width=20,
    ).pack(side="left")

    tk.Button(
        buttons_frame,
        text="Открыть лог",
        command=open_log_file,
        width=16,
    ).pack(side="left", padx=(8, 0))

    tk.Button(
        buttons_frame,
        text="Скрыть в трей",
        command=hide_status_window,
        width=16,
    ).pack(side="left", padx=(8, 0))

    tk.Button(
        buttons_frame,
        text="Выход",
        command=exit_application,
        width=14,
    ).pack(side="right")

    def process_ui_queue() -> None:
        global current_status_text
        global current_problems
        global current_last_check
        global current_state

        try:
            while True:
                event = ui_queue.get_nowait()
                event_type = event.get("type")

                if event_type == "status":
                    current_status_text = event.get("message", "")
                    status_var.set(current_status_text)
                    update_tray()

                elif event_type == "last_check":
                    current_last_check = event.get("value", "")
                    last_check_var.set(current_last_check)
                    update_tray()

                elif event_type == "problems":
                    current_problems = event.get("problems", [])

                    if not current_problems:
                        problems_var.set("Проблем нет")
                    else:
                        problems_var.set("\n".join(f"- {p}" for p in current_problems))

                    update_tray()

                elif event_type == "state":
                    current_state = event.get("state", "unknown")
                    refresh_state_text()
                    update_tray()

                elif event_type == "log":
                    line = f"{event.get('time')} [{event.get('level')}] {event.get('message')}"
                    append_log(line)

                elif event_type == "popup":
                    show_problem_window_safe(
                        event.get("title", APP_NAME),
                        event.get("message", ""),
                    )

        except queue.Empty:
            pass

        if not stop_event.is_set():
            root.after(300, process_ui_queue)

    root.protocol("WM_DELETE_WINDOW", hide_status_window)
    root.after(300, process_ui_queue)

    root.withdraw()

    log_info("Tk window loop starting")
    root.mainloop()
    log_info("Tk window loop stopped")


# ============================================================
# ENTRY POINT
# ============================================================

def main() -> None:
    try:
        acquire_single_instance_mutex()
        log_info("Application main started")

        monitor_thread = threading.Thread(target=monitor_loop, daemon=True)
        monitor_thread.start()

        tk_thread = threading.Thread(target=run_tk_window, daemon=True)
        tk_thread.start()

        run_tray_icon()

    except Exception as exc:
        log_error(f"Fatal application error: {exc}")
        raise


if __name__ == "__main__":
    main()