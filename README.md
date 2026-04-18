# Localcel ⚡

**A portable, Vercel-like environment for localhost.** Localcel provides a beautiful, native Windows 11 GUI to manage local Node.js servers and instantly expose them to the internet using Cloudflare Tunnels—now with built-in **GitHub Pages** deployment!

![Windows 11 Acrylic UI](localcel_full.png)

## ✨ Features (v1.1)
* **GitHub Pages Integration (New):** Deploy any local folder to GitHub Pages with one click. Localcel handles repository creation, Git initialization, and DNS configuration via the GitHub CLI.
* **Smart Tunneling:** Supports both ephemeral Cloudflare TryCloudflare URLs and persistent **Named Tunnels** with custom domains.
* **Tiny Footprint (~5MB):** Uses a advanced "Dropper" architecture. It doesn't bundle massive UI libraries; instead, it leverages the host machine's Python and PyQt6 environment.
* **Auto-Dependency Management:** Missing Python or libraries? Localcel automatically detects, prompts, and installs them via `winget` and `pip` during first-run.
* **Intelligent Port Checker:** No more port collisions! If a port is in use, Localcel prompts you to choose a new free port before starting.
* **Native Windows 11 Aesthetics:** Features native Mica/Acrylic effects and System Tray background mode for persistent hosting.

---

## 🚀 How to Use (For Users)

1. Navigate to the **[Releases](../../releases)** page.
2. Download `Localcel.exe`.
3. Double-click to run. 
   * *Note: If you do not have Python 3 installed, Localcel will prompt you to automatically install it via Windows Package Manager (`winget`).*
4. Select a local directory to act as your "Workspace".
5. Create an app, assign a port, and click **Start**!

---

## 🛠️ How to Build (For Developers)

If you want to modify the source code and recompile the `.exe` yourself, follow these steps:

**Prerequisites:**
Ensure you have Python 3 installed. 

**1. Clone the repository:**
```bash
git clone https://github.com/edwinjosephshiju/Localcel.git
cd localcel
```

**2. Prepare your files:**
Make sure the following files are in the same folder:
- `localcel_optimized.py` (The main source code)
- `localcelBuilder.py` (The build script)
- `localcel_logo.ico` (Icon)
- `localcel_full.png` (Logo)

**3. Run the automated builder:**
```bash
python localcelBuilder.py
```
*The builder will automatically encode your images to Base64, inject them into the code, and invoke PyInstaller with the correct flags.*

**4. Locate your executable:**
Once complete, your portable application will be located in the `dist/` folder as `Localcel.exe`.


---

![Windows 11 Acrylic UI](Screenshot.png)
