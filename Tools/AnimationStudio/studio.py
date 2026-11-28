"""Local CLI. Run with Python 3.10+; Blender is invoked only for explicit commands."""
import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from datetime import datetime, timezone
import uuid

ROOT = Path(__file__).resolve().parent

def blender_executable(explicit=None):
    candidates = [explicit, os.environ.get("BLENDER_EXE"), shutil.which("blender")]
    for candidate in candidates:
        if candidate:
            p = Path(candidate).expanduser()
            if p.is_file():
                return str(p.resolve())
            found = shutil.which(candidate)
            if found:
                return found
    for base in (os.environ.get("ProgramFiles"), os.environ.get("ProgramFiles(x86)")):
        if base:
            installs = sorted((Path(base) / "Blender Foundation").glob("Blender*/blender.exe"), reverse=True)
            if installs:
                return str(installs[0])
    raise SystemExit("Blender não encontrado. Use --blender ou defina BLENDER_EXE.")

def blend_path(value):
    p = Path(value).expanduser().resolve()
    if p.suffix.lower() != ".blend" or not p.is_file():
        raise argparse.ArgumentTypeError(f"Arquivo .blend não encontrado: {p}")
    return p

def script_path(value):
    p = Path(value).expanduser().resolve()
    if p.suffix.lower() != ".py" or not p.is_file():
        raise argparse.ArgumentTypeError(f"Script .py não encontrado: {p}")
    return p

def name_slug(value):
    s = re.sub(r"[^a-zA-Z0-9_-]+", "-", value).strip("-_")[:55]
    return s or "preview"

def main():
    parser = argparse.ArgumentParser(description="ScaryParty Animation Studio")
    parser.add_argument("--blender", help="Caminho do executável Blender")
    sub = parser.add_subparsers(dest="command", required=True)
    ins = sub.add_parser("inspect", help="Inventário de objetos, ossos, ações e pesos")
    ins.add_argument("source", type=blend_path)
    ins.add_argument("--output", type=Path, help="Salvar relatório JSON (opcional)")
    pub = sub.add_parser("publish", help="Publicar uma versão GLB no visualizador")
    pub.add_argument("source", type=blend_path)
    pub.add_argument("--name", required=True)
    pub.add_argument("--mode", choices=["pose", "animation"], default="pose")
    pub.add_argument("--frame", type=int, help="Frame da pose; padrão: frame salvo no arquivo")
    pub.add_argument("--append", action="append", type=blend_path, default=[], help="Adicionar objetos de outro .blend")
    pub.add_argument("--script", type=script_path, help="Script bpy confiável de pose, executado após append")
    pub.add_argument("--save-copy", action="store_true", help="Salvar cópia editável em WorkScenes/")
    args = parser.parse_args()
    cmd = [blender_executable(args.blender), "--background", "--disable-autoexec", str(args.source), "--python-exit-code", "1", "--python", str(ROOT / "blender_worker.py"), "--", args.command]
    if args.command == "inspect":
        if args.output:
            cmd += ["--output", str(args.output.resolve())]
        return subprocess.run(cmd, check=False).returncode
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    version = f"{name_slug(args.name)}-{stamp}-{uuid.uuid4().hex[:8]}"
    directory = ROOT / "Previews" / version
    cmd += ["--name", args.name, "--mode", args.mode, "--output", str(directory), "--source", args.source.name]
    if args.frame is not None:
        cmd += ["--frame", str(args.frame)]
    for other in args.append:
        cmd += ["--append", str(other)]
    if args.script:
        cmd += ["--script", str(args.script)]
    if args.save_copy:
        cmd += ["--save-copy", str(ROOT / "WorkScenes" / (version + ".blend"))]
    result = subprocess.run(cmd, check=False)
    if result.returncode == 0 and (directory / "manifest.json").is_file():
        port = os.environ.get("ANIMATION_STUDIO_PORT", "8765")
        print(f"Preview publicado: {version}\nVisualizador: http://127.0.0.1:{port}/")
        return 0
    print("Falha: preview não publicado. Consulte o log do Blender.", file=sys.stderr)
    return result.returncode or 1

if __name__ == "__main__":
    raise SystemExit(main())
