import os
import sys
import base64
import re
import subprocess

# Configuration
ICON_FILE = "localcel_logo.ico"
LOGO_FILE = "localcel_full.png"
SOURCE_FILE = "localcel_optimized.py"
STAGING_FILE = "localcel_build_ready.py"
EXE_NAME = "Localcel"

def check_dependencies():
    """Ensure all required files exist and PyInstaller is installed."""
    missing_files = [f for f in [ICON_FILE, LOGO_FILE, SOURCE_FILE] if not os.path.exists(f)]
    if missing_files:
        print(f"❌ Error: Missing required files: {', '.join(missing_files)}")
        print("Please ensure the images and the python script are in the same directory.")
        sys.exit(1)

    try:
        import PyInstaller
    except ImportError:
        print("❌ Error: PyInstaller is not installed.")
        print("Installing PyInstaller now...")
        subprocess.check_call([sys.executable, "-m", "pip", "install", "pyinstaller"])

def encode_images():
    """Reads the image files and converts them to Base64 strings."""
    print(f"📦 Encoding '{ICON_FILE}' and '{LOGO_FILE}' to Base64...")
    with open(ICON_FILE, "rb") as f:
        icon_b64 = base64.b64encode(f.read()).decode('utf-8')
    with open(LOGO_FILE, "rb") as f:
        logo_b64 = base64.b64encode(f.read()).decode('utf-8')
    return icon_b64, logo_b64

def inject_base64(icon_b64, logo_b64):
    """Reads the source file, injects the Base64 data, and writes to a staging file."""
    print(f"💉 Injecting Base64 strings into source code...")
    
    with open(SOURCE_FILE, "r", encoding="utf-8") as f:
        content = f.read()

    # Regex to find the placeholder variables and replace their contents
    # Matches: ICON_B64 = b"anything_inside"
    content = re.sub(r'ICON_B64\s*=\s*b"[^"]*"', f'ICON_B64 = b"{icon_b64}"', content)
    content = re.sub(r'LOGO_B64\s*=\s*b"[^"]*"', f'LOGO_B64 = b"{logo_b64}"', content)

    # Save to a temporary staging file so we don't permanently modify the user's original file
    with open(STAGING_FILE, "w", encoding="utf-8") as f:
        f.write(content)
        
    print(f"✅ Staging file '{STAGING_FILE}' created successfully.")

def compile_exe():
    """Runs PyInstaller to build the single-file executable."""
    print(f"🚀 Compiling {EXE_NAME}.exe using PyInstaller...")
    
    command = [
        sys.executable, "-m", "PyInstaller",
        "--onefile",               # Single executable
        "--noconsole",             # No command prompt window
        f"--icon={ICON_FILE}",     # Taskbar/File explorer icon
        f"--name={EXE_NAME}",      # Name of the output .exe
        STAGING_FILE               # Target file to compile
    ]
    
    try:
        subprocess.check_call(command)
        print(f"\n🎉 Build Complete! Your portable '{EXE_NAME}.exe' is located in the 'dist' folder.")
    except subprocess.CalledProcessError as e:
        print(f"\n❌ Build failed with error code {e.returncode}.")
    finally:
        # Clean up the staging script to keep the workspace clean
        if os.path.exists(STAGING_FILE):
            os.remove(STAGING_FILE)
            print("🧹 Cleaned up temporary staging files.")

if __name__ == "__main__":
    print("=== Localcel Automated Build Script ===\n")
    check_dependencies()
    
    icon_b64, logo_b64 = encode_images()
    inject_base64(icon_b64, logo_b64)
    compile_exe()