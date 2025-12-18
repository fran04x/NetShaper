import os
import tkinter as tk
from tkinter import ttk, messagebox, scrolledtext
import math

# --- CONFIGURACIÓN ---
ROOT_DIR = '.'
EXTENSIONS = ['.cs']
IGNORE_DIRS = {'bin', 'obj', '.git', '.vs', '.idea', 'Properties', 'node_modules'}
OUTPUT_FILE = 'analisis.proyecto.txt'
BG_COLOR = "#ffffff"

# --- PROMPTS PREDEFINIDOS ---
PROMPTS = {
    "Modo 0: vacio": """""",
    "Modo 1: Contexto Maestro (NetShaper)": """AUDITORÍA NETSHAPER (STRICT COMPLIANCE MODE)

ROL: Principal Engineer & Automated Linter.
OBJETIVO: Analizar y validar cumplimiento estricto de estándares.

CONTEXTO DEL PROYECTO:
- Stack: C# 12, .NET 8, WinForms, WinDivert 2.2.2.
- Capas:
  1. ENGINE (Hot Path): Crítico. Cero latencia.
  2. UI (WinForms): Reactividad y limpieza.

PRINCIPIOS SUPERIORES (OBLIGATORIOS Y NO NEGOCIABLES)

Aplicación estricta, prioridad de mayor a menor:
-Clean Architecture (dependencias solo hacia adentro).
-SOLID completo.
-Separation of Concerns.
-KISS / DRY / YAGNI.
-Complejidad ciclomática ≤10.
-Anidación ≤3 niveles.
-APIs públicas sin null; usar tipos no-nullables.
-Async siempre con CancellationToken.
-Recursos liberados (IDisposable / using / ref struct).
-Nullable enable + warnings as errors.
-Si una regla choca con otra, gana la regla superior.

CONSTITUCIÓN DE CÓDIGO (THE LAW):
PRINCIPIOS GENERALES (OBLIGATORIOS)

- Clean Architecture: dependencias solo hacia adentro.
- SOLID completo.
- Separation of Concerns.
- KISS / DRY / YAGNI.
- Complejidad ciclomática ≤10.
- Anidación ≤3 niveles.
- APIs públicas sin null; usar tipos no-nullables.
- Async siempre con CancellationToken.
- Recursos liberados (IDisposable / using / ref struct cuando aplica).
- Nullable enable + warnings as errors.
- Si una regla choca con otra, gana la regla superior.
LAYER: ENGINE / DATA PLANE
- Evitar allocations en loops (new, LINQ, boxing, ToArray()).
- Usar Span, ArrayPool, buffers, estructuras ref struct donde sea crítico.
- Evitar locks innecesarios; usar concurrencia segura.
LAYER: UI / GENERAL
- Async/await correcto en toda lógica asíncrona.
- Separar lógica de UI de lógica de negocio y red.
- Evitar bloqueo del hilo UI.
- Inyección de dependencias limpia.
GLOBAL: CLEAN CODE
- Métodos ≤50 líneas.
- Nombres claros y descriptivos.
- Evitar comentarios obvios.
- Principios SOLID aplicados en todo el código.
GLOBAL: ARQUITECTURA
- Sin dependencias cíclicas entre proyectos.
- Evitar servicios estáticos salvo constantes.
- Interfaces en capas superiores, implementaciones en inferiores.
- Separación estricta entre dominio, infraestructura y presentación.
GLOBAL: ERRORES
- No capturar Exception genérica sin filtrar.
- No silenciar errores.
- Logs estructurados (ID, categoría, nivel).
GLOBAL: TESTING
- Unit tests por componente.
- Tests independientes de red o disco mediante mocks.
- Integración solo donde sea necesaria.
GLOBAL: VERSIONADO
- Commits atómicos.
- Convenciones tipo Conventional Commits.
- Cambios mediante Pull Request.
PERFORMANCE / MEMORIA
- Evitar GC Gen2 evitable.
- Evitar reasignación repetida de buffers.
- Benchmarks para cambios en Engine donde aplique.
SEGURIDAD
- No hardcodear secrets.
- Validación estricta de input.
- Serialización segura.
RESPONDE: "*SCORE [1/100]*"
Tabla:
| Severidad | Archivo y Línea | Regla Violada | Solución |
""",
    "Modo 2: Diagnóstico (Qué arreglar primero)": """ROL: Lead Software Architect & Open Source Maintainer conocido por ser extremadamente estricto en los Code Reviews (estilo Linus Torvalds)
OBJETIVO: Identificar puntos críticos (Performance, Seguridad, Spaghetti Code) sin ser pedante.
INSTRUCCIONES DE ANÁLISIS:
1. Detecta archivos "Clase Dios" (>400 líneas con múltiples responsabilidades).
2. Detecta "Micro-fragmentación" (exceso de archivos pequeños de <20 líneas que deberían estar juntos).
3. Prioriza SOLO lo que pone en riesgo la estabilidad o el rendimiento AHORA.
SALIDA ESPERADA:
- Lista priorizada de problemas.
- NO generes código aún.
- Para cada problema, sugiere: "¿Refactorizar in-place o extraer a archivo nuevo?".""",
    "Modo 3: Implementación Profesional (Código Real)": """ROL: SENIOR C# DEVELOPER
OBJETIVO: Aplicar la solución al problema seleccionado manteniendo un equilibrio arquitectónico.
REGLAS DE ORO (MODULARIDAD PRAGMÁTICA):
1. **NO MÁS CLASES DIOS:** Si tu solución hace que un archivo supere las 400 líneas, DEBES extraer lógica a un nuevo archivo (ej: `PacketProcessor.cs`).
2. **NO MICRO-ARCHIVOS:** No crees un archivo nuevo para un Enum, un DTO simple o una Interface pequeña. Agrúpalos en el archivo que los usa o en un `DomainTypes.cs`.
3. **COHESIÓN:** Las clases relacionadas deben vivir cerca.
TU TAREA:
1. Resuelve el problema solicitado.
2. Si creas nuevos archivos, entrégalos completos con formato:
   ### NombreArchivo.cs
   ```csharp
   ... código ...
Si modificas existentes, entrega el archivo COMPLETO (no snippets).""",
    "Modo 4: Arquitecto (Reestructuración)": """ROL: SOFTWARE ARCHITECT 
OBJETIVO: Organizar el proyecto para que sea escalable pero mantenible por un humano.
ESTRATEGIA DE REFACTORIZACIÓN:
Consolidación: Identifica qué archivos pequeños se pueden fusionar (ej: Mover todas las interfaces de red a NetworkContract.cs).
Segregación: Identifica qué lógica de negocio está mezclada con UI y propon su extracción.
Estructura: Propón una estructura de carpetas lógica (Services, Models, Core, UI).
SALIDA:
Árbol de carpetas propuesto.
Lista de archivos a CREAR, ELIMINAR o FUSIONAR.
Justificación técnica breve.""",
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

        # Variables para los modos
        self.include_csproj = tk.BooleanVar(value=False)
        self.structure_only = tk.BooleanVar(value=False)
        self.select_all_var = tk.BooleanVar(value=True)  # Para checkbox de selección

        self.scanned_files = []
        self.file_vars = []
        self.folder_vars = {}  # Track folder checkboxes
        self.folder_expanded = {}  # Track which folders are expanded
        
        self.create_ui()
        # Escaneo inicial
        self.refresh_file_list() 
        self.root.after(10, self.adjust_window_size)

    def scan_files(self):
        files_data = []
        # Determinar extensiones según el modo
        current_exts = EXTENSIONS.copy()
        if self.include_csproj.get():
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
        
        # Checkbox de selección todo/nada
        ttk.Checkbutton(
            toolbar, 
            text="Seleccionar todo",
            variable=self.select_all_var,
            command=self.toggle_select_all
        ).pack(side='left', padx=(0, 10))
        
        # Botón expandir/colapsar todo
        ttk.Button(
            toolbar, 
            text="▼ Expandir todo",
            command=self.expand_collapse_all,
            width=15
        ).pack(side='left', padx=(0, 10))
        
        # --- CHECKBOXES DE MODO ---
        ttk.Checkbutton(
            toolbar, 
            text="Incluir .csproj", 
            variable=self.include_csproj, 
            command=self.refresh_file_list
        ).pack(side='left', padx=(10, 5))
        
        ttk.Checkbutton(
            toolbar, 
            text="Solo Estructura (Usings)", 
            variable=self.structure_only, 
            command=None  # No recarga lista, solo afecta output
        ).pack(side='left', padx=(5, 5))
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
        self.folder_vars = {}

        # Escanear
        self.scanned_files = self.scan_and_sort_files()

        total = len(self.scanned_files)
        if total == 0:
            ttk.Label(self.scroll_frame, text="No se encontraron archivos.", background=BG_COLOR).pack(pady=10)
            self.update_stats()
            return


        # Agrupar archivos por carpeta
        folders = {}
        for f in self.scanned_files:
            folder = f['directory'] if f['directory'] else "[ROOT]"
            if folder not in folders:
                folders[folder] = []
            folders[folder].append(f)

        # Responsive columns (3-4 columnas)
        sorted_folders = sorted(folders.keys(), key=lambda x: (x != "[ROOT]", x))
        num_folders = len(sorted_folders)
        num_cols = min(4, max(3, (num_folders + 2) // 3))  # 3-4 columnas
        folders_per_col = (num_folders + num_cols - 1) // num_cols
        
        col = 0
        row = 0
        
        for idx, folder_name in enumerate(sorted_folders):
            files = folders[folder_name]
            
            # Cambiar de columna
            if idx > 0 and idx % folders_per_col == 0:
                col += 1
                row = 0
            
            # Frame para carpeta
            folder_frame = ttk.Frame(self.scroll_frame, style="White.TFrame")
            folder_frame.grid(row=row, column=col, sticky='ew', padx=(2, 12), pady=2)
            
            # Estado colapsado por defecto
            if folder_name not in self.folder_expanded:
                self.folder_expanded[folder_name] = False
            
            # Botón expandir/colapsar
            expand_btn = tk.Button(
                folder_frame,
                text="▶" if not self.folder_expanded[folder_name] else "▼",
                command=lambda fn=folder_name: self.toggle_expand(fn),
                width=2, relief='flat', bg=BG_COLOR, fg="#555",
                font=('Segoe UI', 7), cursor="hand2", borderwidth=0
            )
            expand_btn.pack(side='left')
            
            # Checkbox de carpeta
            folder_var = tk.BooleanVar(value=True)
            folder_chk = ttk.Checkbutton(
                folder_frame,
                text=f"{folder_name} ({len(files)})",
                variable=folder_var,
                command=lambda fn=folder_name: self.toggle_folder(fn),
                style="Bold.TCheckbutton"
            )
            folder_chk.pack(side='left')
            
            self.folder_vars[folder_name] = {"var": folder_var, "files": []}
            row += 1
            
            # Mostrar archivos solo si expandido
            if self.folder_expanded[folder_name]:
                for f in files:
                    file_var = tk.BooleanVar(value=True)
                    file_chk = ttk.Checkbutton(
                        self.scroll_frame,
                        text=f"  {f['name']} ({f['lines']}L)",
                        variable=file_var,
                        command=lambda fn=folder_name: self.on_file_toggle(fn),
                        style="TCheckbutton"
                    )
                    file_chk.grid(row=row, column=col, sticky='w', padx=(24, 12), pady=0)
                    
                    file_data = {**f, "var": file_var}
                    self.file_vars.append(file_data)
                    self.folder_vars[folder_name]["files"].append(file_data)
                    row += 1
            else:
                # Archivos ocultos pero funcionales
                for f in files:
                    file_var = tk.BooleanVar(value=True)
                    file_data = {**f, "var": file_var}
                    self.file_vars.append(file_data)
                    self.folder_vars[folder_name]["files"].append(file_data)
        
        # Estilo bold
        style = ttk.Style()
        style.configure("Bold.TCheckbutton", font=('Segoe UI', 9, 'bold'), background=BG_COLOR)
        
        self.update_stats()
        self.adjust_window_size()

    def toggle_expand(self, folder_name):
        """Expand/colapsa una carpeta"""
        self.folder_expanded[folder_name] = not self.folder_expanded.get(folder_name, False)
        self.refresh_file_list()

    def toggle_folder(self, folder_name):
        """Selecciona/deselecciona todos los archivos de una carpeta"""
        if folder_name not in self.folder_vars:
            return
        
        folder_checked = self.folder_vars[folder_name]["var"].get()
        for file_data in self.folder_vars[folder_name]["files"]:
            file_data["var"].set(folder_checked)
        self.update_stats()

    def on_file_toggle(self, folder_name):
        """Actualiza checkbox de carpeta según estado de archivos"""
        if folder_name not in self.folder_vars:
            return
        
        files = self.folder_vars[folder_name]["files"]
        checked_count = sum(1 for f in files if f["var"].get())
        
        # Si todos están seleccionados, marcar carpeta
        self.folder_vars[folder_name]["var"].set(checked_count == len(files))
        self.update_stats()

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
        req_h = min(self.scroll_frame.winfo_reqheight() + 320, 550)  # Más compacto
        self.root.geometry(f"800x{req_h}")  # 800px ancho
        self.canvas.config(scrollregion=self.canvas.bbox("all"))
        self.toggle_scroll()

    def toggle_select_all(self):
        """Toggle selección de todos los archivos según checkbox"""
        state = self.select_all_var.get()
        for folder_data in self.folder_vars.values():
            folder_data['var'].set(state)
        for file_data in self.file_vars:
            file_data['var'].set(state)
        self.update_stats()
    
    def expand_collapse_all(self):
        """Expande o colapsa todas las carpetas"""
        # Determinar si hay alguna colapsada
        any_collapsed = any(not expanded for expanded in self.folder_expanded.values())
        
        # Si hay alguna colapsada, expandir todas; sino, colapsar todas
        new_state = any_collapsed
        
        for folder_name in self.folder_expanded.keys():
            self.folder_expanded[folder_name] = new_state
        
        # Actualizar texto del botón
        btn_text = "▲ Colapsar todo" if new_state else "▼ Expandir todo"
        # Buscar el botón en el toolbar y actualizar su texto
        for widget in self.root.winfo_children():
            if isinstance(widget, ttk.Frame):
                for child in widget.winfo_children():
                    if isinstance(child, ttk.Frame):
                        for toolbar_child in child.winfo_children():
                            if isinstance(toolbar_child, ttk.Button):
                                if "Expandir" in toolbar_child.cget("text") or "Colapsar" in toolbar_child.cget("text"):
                                    toolbar_child.config(text=btn_text)
        
        self.refresh_file_list()

    def select_all(self):
        for folder_data in self.folder_vars.values():
            folder_data['var'].set(True)
        for i in self.file_vars:
            i['var'].set(True)
        self.update_stats()
    
    def deselect_all(self):
        for folder_data in self.folder_vars.values():
            folder_data['var'].set(False)
        for i in self.file_vars:
            i['var'].set(False)
        self.update_stats()
    def update_stats(self):
        sel = [f for f in self.file_vars if f['var'].get()]
        total_kb = sum(f['kb'] for f in sel)
        total_lines = sum(f['lines'] for f in sel)
        self.lbl_stats.config(text=f"{len(sel)}/{len(self.file_vars)} sel | {total_lines} líneas | {int(total_kb)} KB")

    def generate_file(self):
        sel = [f for f in self.file_vars if f['var'].get()]
        if not sel: return messagebox.showwarning("!", "Selecciona archivos.")
        
        is_structure_only = self.structure_only.get()
        is_include_csproj = self.include_csproj.get()
        
        try:
            with open(OUTPUT_FILE, 'w', encoding='utf-8') as out:
                out.write(self.txt_prompt.get("1.0", tk.END).strip() + "\n\n")
                
                # Header según modo
                if is_structure_only:
                    header = "ESTRUCTURA (Usings + Namespace)"
                else:
                    header = "CÓDIGO FUENTE COMPLETO"
                out.write(f"# CONTEXTO: {len(sel)} ARCHIVOS - {header}\n")
                
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
                        
                        # Filtrado según modo
                        if is_structure_only and f['name'].endswith('.cs'):
                            # Solo usings y namespace para archivos .cs
                            lines = src.readlines()
                            filtered = [line for line in lines 
                                       if line.strip().startswith('using') or 
                                          line.strip().startswith('namespace')]
                            content = "".join(filtered) if filtered else "// (Sin usings o namespace)"
                        else:
                            # Contenido completo (.csproj o modo normal)
                            content = src.read()
                        
                        out.write(f'<file path="{f["path"]}">\n<![CDATA[\n{content.strip()}\n]]>\n</file>\n')
                
                out.write("</codebase>")
            
            # Mensaje según modos activos
            msg = f"Contexto generado: {OUTPUT_FILE}"
            status_parts = []
            if is_include_csproj:
                status_parts.append("con .csproj")
            if is_structure_only:
                status_parts.append("solo usings/namespace")
            if status_parts:
                msg += f"\n({', '.join(status_parts)})"
            
            messagebox.showinfo("Listo", msg)
            self.root.destroy()
        except Exception as e: messagebox.showerror("Error", str(e))

if __name__ == "__main__":
    root = tk.Tk()
    app = ContextApp(root)
    root.mainloop()