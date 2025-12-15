import os
import tkinter as tk
from tkinter import ttk, messagebox, scrolledtext
import math

# --- CONFIGURACIÓN ---
ROOT_DIR = '.'
EXTENSIONS = ['.cs']
IGNORE_DIRS = {'bin', 'obj', '.git', '.vs', '.idea', 'Properties', 'node_modules'}
OUTPUT_FILE = 'analisis.proyecto.d.txt'
BG_COLOR = "#ffffff"

# --- PROMPTS PREDEFINIDOS ---
PROMPTS = {
    "Modo 0: vacio": """""",
    "Modo 1: Contexto Maestro (NetShaper)": """AUDITORÍA NETSHAPER (STRICT COMPLIANCE MODE)
ROL: Principal Engineer & Automated Linter.
OBJETIVO: Analizar y validar cumplimiento estricto de estándares.
CONTEXTO DEL PROYECTO:
- Stack: C# 12, .NET 8, WinForms, WinDivert 2.2.2.
CONSTITUCIÓN DE CÓDIGO (THE LAW):
- Clean Architecture (dependencias solo hacia adentro).
- SOLID completo.
- Separation of Concerns.
- KISS / DRY / YAGNI.
- Complejidad ciclomática ≤10.
- Anidación ≤3 niveles.
- APIs públicas sin null; usar tipos no-nullables.
- Async siempre con CancellationToken.
RESPONDE: "*SCORE [1/100]*"
Tabla:
| Severidad | Archivo y Línea | Regla Violada | Solución |
""",
    "Modo 2: Diagnóstico": """ROL: Lead Software Architect
OBJETIVO: Identificar puntos críticos (Performance, Seguridad, Spaghetti Code).
1. Detecta archivos "Clase Dios".
2. Detecta "Micro-fragmentación".
3. Prioriza SOLO lo que pone en riesgo la estabilidad.""",
    "Modo 3: Implementación": """ROL: SENIOR C# DEVELOPER
OBJETIVO: Aplicar la solución al problema seleccionado.
REGLAS:
1. NO MÁS CLASES DIOS.
2. NO MICRO-ARCHIVOS.
3. COHESIÓN.""",
    "Modo 4: Arquitecto": """ROL: SOFTWARE ARCHITECT 
OBJETIVO: Organizar el proyecto para que sea escalable.
ESTRATEGIA: Consolidación, Segregación, Estructura.""",
    "Modo 5: Auditor de Dependencias (DIP Check)": """ROL: Software Architect especializado en Clean Architecture y Static Analysis.
OBJETIVO: Detectar violaciones del Principio de Inversión de Dependencias (DIP) y fugas de capas (Leaky Abstractions) usando solo la estructura de archivos y referencias.

INPUT:
1. Archivos .csproj (definen referencias entre proyectos).
2. Cabeceras de archivos .cs (usings y namespace).

TU TAREA - BUSCA ESTOS PATRONES ILEGALES:
1. DOMAIN POLLUTION: ¿El dominio importa librerías externas de infraestructura (EF Core, Newtonsoft, Sockets)?
2. LAYER VIOLATION: ¿Capas inferiores (Core) hacen 'using' de capas superiores (UI/Web)?
3. TIGHT COUPLING: ¿Hay 'usings' concretos donde debería haber abstracciones?

SALIDA (Formato Tabla Markdown):
| Gravedad | Archivo | Violación Detectada | Por qué rompe DIP/Clean Arch |
|----------|---------|---------------------|------------------------------|
| ALTA     | User.cs | using System.Data   | Entidad de dominio dependiendo de ADO.NET |
| MEDIA    | Svc.cs  | using WinForms      | Lógica de negocio acoplada a UI concreta |

Si todo está perfecto, responde: "✅ ARQUITECTURA LIMPIA: No se detectaron violaciones de dependencia."""
}

class ContextApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Generador de Contexto IA - Estratégico")
        
        style = ttk.Style()
        style.theme_use('clam')
        style.configure("White.TFrame", background=BG_COLOR)
        style.configure("Bold.TLabel", font=('Segoe UI', 9, 'bold'))
        style.configure("TCheckbutton", background=BG_COLOR)
        style.configure("Accent.TButton", font=('Segoe UI', 10, 'bold'), foreground="black")

        # Variable para el nuevo modo
        self.structure_mode = tk.BooleanVar(value=False)

        self.scanned_files = []
        self.file_vars = [] 
        
        self.create_ui()
        # Escaneo inicial
        self.refresh_file_list() 
        self.root.after(10, self.adjust_window_size)

    def scan_files(self):
        files_data = []
        # Determinar extensiones según el modo
        current_exts = EXTENSIONS.copy()
        if self.structure_mode.get():
            current_exts.append('.csproj')

        for root_path, dirs, files in os.walk(ROOT_DIR):
            dirs[:] = [d for d in dirs if d not in IGNORE_DIRS]
            for file in files:
                if any(file.endswith(ext) for ext in current_exts):
                    full_path = os.path.join(root_path, file)
                    try:
                        size_kb = os.path.getsize(full_path) / 1024
                        with open(full_path, 'r', encoding='utf-8') as f:
                            lines = sum(1 for _ in f)
                        files_data.append({
                            "path": os.path.relpath(full_path, ROOT_DIR),
                            "name": file,
                            "lines": lines,
                            "kb": round(size_kb, 1),
                            "directory": os.path.dirname(os.path.relpath(full_path, ROOT_DIR))
                        })
                    except: pass
        return files_data

    def scan_and_sort_files(self):
        files_data = self.scan_files()
        root_files = []
        dir_files = []
        for f in files_data:
            if not f['directory']: root_files.append(f)
            else: dir_files.append(f)
        
        root_files.sort(key=lambda x: x['name'])
        dir_files.sort(key=lambda x: (x['directory'], x['name']))
        return root_files + dir_files

    def create_ui(self):
        main_frame = ttk.Frame(self.root, padding="15")
        main_frame.pack(fill='both', expand=True)

        # 1. SELECTOR
        lbl_mode = ttk.Label(main_frame, text="Estrategia de Prompt:", style="Bold.TLabel")
        lbl_mode.pack(anchor='w', pady=(0, 2))
        
        self.combo_mode = ttk.Combobox(main_frame, values=list(PROMPTS.keys()), state="readonly")
        self.combo_mode.current(0)
        self.combo_mode.pack(fill='x', pady=(0, 10))
        self.combo_mode.bind("<<ComboboxSelected>>", self.on_mode_change)

        # 2. EDITOR
        self.txt_prompt = scrolledtext.ScrolledText(main_frame, height=5, font=('Consolas', 9), borderwidth=1, relief="solid")
        self.txt_prompt.pack(fill='x', pady=(0, 10))
        self.txt_prompt.insert(tk.END, PROMPTS[self.combo_mode.get()])

        # 3. TOOLBAR
        toolbar = ttk.Frame(main_frame)
        toolbar.pack(fill='x', pady=(0, 5))
        ttk.Button(toolbar, text="☑ Todo", command=self.select_all).pack(side='left', padx=(0, 5), ipadx=10)
        ttk.Button(toolbar, text="☐ Nada", command=self.deselect_all).pack(side='left', ipadx=10)
        
        # --- NUEVO CHECKBOX ---
        ttk.Checkbutton(
            toolbar, 
            text="Solo Estructura (Usings + .csproj)", 
            variable=self.structure_mode, 
            command=self.refresh_file_list # Recarga la lista al cambiar
        ).pack(side='left', padx=(10, 5))
        # ----------------------

        self.lbl_stats = ttk.Label(toolbar, text="...", foreground="#0055cc")
        self.lbl_stats.pack(side='right')

        # 4. BOTÓN
        btn_gen = ttk.Button(main_frame, text="GENERAR CONTEXTO", command=self.generate_file, style="Accent.TButton")
        btn_gen.pack(side='bottom', fill='x', pady=(10, 0), ipady=5)

        # 5. LISTA
        list_frame = tk.LabelFrame(main_frame, text=" Archivos ", bg=BG_COLOR, padx=5, pady=5)
        list_frame.pack(side='top', fill='both', expand=True, pady=5)

        self.canvas = tk.Canvas(list_frame, bg=BG_COLOR, highlightthickness=0)
        self.v_scroll = ttk.Scrollbar(list_frame, orient="vertical", command=self.canvas.yview)
        self.scroll_frame = ttk.Frame(self.canvas, style="White.TFrame")
        
        self.canvas.create_window((0, 0), window=self.scroll_frame, anchor="nw", tags="inner")
        self.canvas.bind('<Configure>', lambda e: self.canvas.itemconfig("inner", width=e.width))
        self.canvas.pack(side="left", fill="both", expand=True)

    def refresh_file_list(self):
        """Limpia y repuebla la lista de archivos basándose en la configuración actual"""
        # Limpiar UI existente
        for widget in self.scroll_frame.winfo_children():
            widget.destroy()
        self.file_vars = []

        # Escanear
        self.scanned_files = self.scan_and_sort_files()

        # Grid lógico
        total = len(self.scanned_files)
        if total == 0:
            ttk.Label(self.scroll_frame, text="No se encontraron archivos.", background=BG_COLOR).pack(pady=10)
            self.update_stats()
            return

        cols = 3 if total > 12 else 2
        rows = math.ceil(total / cols)

        for i, f in enumerate(self.scanned_files):
            var = tk.BooleanVar(value=True)
            txt = f"{f['name']} ({f['lines']}L)"
            chk = ttk.Checkbutton(self.scroll_frame, text=txt, variable=var, command=self.update_stats, style="TCheckbutton")
            chk.grid(row=i%rows, column=i//rows, sticky='w', padx=(10,20), pady=2)
            self.file_vars.append({**f, "var": var})
        
        self.update_stats()
        self.adjust_window_size()

    def on_mode_change(self, event):
        key = self.combo_mode.get()
        self.txt_prompt.delete("1.0", tk.END)
        self.txt_prompt.insert(tk.END, PROMPTS[key])

    def toggle_scroll(self):
        bbox = self.canvas.bbox("all")
        if bbox and bbox[3] > self.canvas.winfo_height():
            self.v_scroll.pack(side="right", fill="y")
            self.canvas.config(yscrollcommand=self.v_scroll.set)
        else:
            self.v_scroll.pack_forget()
            self.canvas.config(yscrollcommand=None)

    def adjust_window_size(self):
        self.root.update_idletasks()
        req_h = self.scroll_frame.winfo_reqheight() + 320
        max_h = self.root.winfo_screenheight() - 150
        self.root.geometry(f"650x{min(req_h, max_h)}") # Un poco más ancho para el checkbox extra
        self.canvas.config(scrollregion=self.canvas.bbox("all"))
        self.toggle_scroll()

    def select_all(self):
        for i in self.file_vars: i['var'].set(True)
        self.update_stats()
    def deselect_all(self):
        for i in self.file_vars: i['var'].set(False)
        self.update_stats()
    def update_stats(self):
        sel = [f for f in self.file_vars if f['var'].get()]
        total_kb = sum(f['kb'] for f in sel)
        total_lines = sum(f['lines'] for f in sel)
        self.lbl_stats.config(text=f"{len(sel)}/{len(self.file_vars)} sel | {total_lines} líneas | {int(total_kb)} KB")

    def generate_file(self):
        sel = [f for f in self.file_vars if f['var'].get()]
        if not sel: return messagebox.showwarning("!", "Selecciona archivos.")
        
        is_structure_mode = self.structure_mode.get()
        
        try:
            with open(OUTPUT_FILE, 'w', encoding='utf-8') as out:
                out.write(self.txt_prompt.get("1.0", tk.END).strip() + "\n\n")
                
                header_title = "ESTRUCTURA (Usings + Proyectos)" if is_structure_mode else "CÓDIGO FUENTE COMPLETO"
                out.write(f"# CONTEXTO: {len(sel)} ARCHIVOS - {header_title}\n")
                
                sorted_sel = sorted(sel, key=lambda x: (
                    0 if not x['directory'] else 1, 
                    x['directory'] if x['directory'] else "", 
                    x['name']
                ))
                
                for f in sorted_sel:
                    out.write(f"- {f['path']}\n")
                
                out.write("\n# CONTENIDO\n<codebase>\n")
                
                for f in sorted_sel:
                    with open(f['path'], 'r', encoding='utf-8') as src:
                        content = ""
                        
                        # --- LÓGICA DE FILTRADO ---
                        if is_structure_mode:
                            if f['name'].endswith('.csproj'):
                                # .csproj se incluye completo (contexto de dependencias)
                                content = src.read()
                            else:
                                # Archivos de código: solo using y namespace
                                lines = src.readlines()
                                filtered_lines = [
                                    line for line in lines 
                                    if line.strip().startswith('using') or line.strip().startswith('namespace')
                                ]
                                content = "".join(filtered_lines)
                                if not content.strip(): content = "// (Sin usings o namespace detectados)"
                        else:
                            # Modo normal: todo el contenido
                            content = src.read()
                        # --------------------------

                        out.write(f'<file path="{f["path"]}">\n<![CDATA[\n{content.strip()}\n]]>\n</file>\n')
                
                out.write("</codebase>")
            
            msg = f"Contexto generado: {OUTPUT_FILE}"
            if is_structure_mode:
                msg += "\n(Modo Estructura: Solo Usings y .csproj)"
            messagebox.showinfo("Listo", msg)
            self.root.destroy()
        except Exception as e: messagebox.showerror("Error", str(e))

if __name__ == "__main__":
    root = tk.Tk()
    app = ContextApp(root)
    root.mainloop()