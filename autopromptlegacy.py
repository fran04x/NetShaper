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
Justificación técnica breve."""
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

        self.scanned_files = self.scan_and_sort_files()
        self.file_vars = [] 
        
        self.create_ui()
        self.root.after(10, self.adjust_window_size)

    def scan_files(self):
        files_data = []
        for root_path, dirs, files in os.walk(ROOT_DIR):
            dirs[:] = [d for d in dirs if d not in IGNORE_DIRS]
            for file in files:
                if any(file.endswith(ext) for ext in EXTENSIONS):
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
        """Escanea los archivos y los ordena según los criterios especificados"""
        files_data = self.scan_files()
        
        # Clasificar archivos: root files primero, luego por carpeta
        root_files = []
        dir_files = []
        
        for f in files_data:
            if not f['directory']:  # Archivo en raíz
                root_files.append(f)
            else:
                dir_files.append(f)
        
        # Ordenar archivos raíz alfabéticamente
        root_files.sort(key=lambda x: x['name'])
        
        # Ordenar archivos en carpetas: primero por nombre de carpeta, luego por nombre de archivo
        dir_files.sort(key=lambda x: (x['directory'], x['name']))
        
        # Combinar: root files primero, luego archivos en carpetas
        return root_files + dir_files

    def create_ui(self):
        main_frame = ttk.Frame(self.root, padding="15")
        main_frame.pack(fill='both', expand=True)

        # 1. SELECTOR DE ESTRATEGIA
        lbl_mode = ttk.Label(main_frame, text="Estrategia de Prompt:", style="Bold.TLabel")
        lbl_mode.pack(anchor='w', pady=(0, 2))
        
        self.combo_mode = ttk.Combobox(main_frame, values=list(PROMPTS.keys()), state="readonly")
        self.combo_mode.current(0)
        self.combo_mode.pack(fill='x', pady=(0, 10))
        self.combo_mode.bind("<<ComboboxSelected>>", self.on_mode_change)

        # 2. EDITOR DE PROMPT
        self.txt_prompt = scrolledtext.ScrolledText(main_frame, height=5, font=('Consolas', 9), borderwidth=1, relief="solid")
        self.txt_prompt.pack(fill='x', pady=(0, 10))
        self.txt_prompt.insert(tk.END, PROMPTS[self.combo_mode.get()])

        # 3. TOOLBAR
        toolbar = ttk.Frame(main_frame)
        toolbar.pack(fill='x', pady=(0, 5))
        ttk.Button(toolbar, text="☑ Todo", command=self.select_all).pack(side='left', padx=(0, 5), ipadx=10)
        ttk.Button(toolbar, text="☐ Nada", command=self.deselect_all).pack(side='left', ipadx=10)
        self.lbl_stats = ttk.Label(toolbar, text="...", foreground="#0055cc")
        self.lbl_stats.pack(side='right')

        # 4. BOTÓN (Fijo al fondo)
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
        
        # Grid lógico
        total = len(self.scanned_files)
        cols = 3 if total > 12 else 2
        rows = math.ceil(total / cols)

        for i, f in enumerate(self.scanned_files):
            var = tk.BooleanVar(value=True)
            txt = f"{f['name']} ({f['lines']}L)"
            chk = ttk.Checkbutton(self.scroll_frame, text=txt, variable=var, command=self.update_stats, style="TCheckbutton")
            chk.grid(row=i%rows, column=i//rows, sticky='w', padx=(10,20), pady=2)
            self.file_vars.append({**f, "var": var})
        
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
        req_h = self.scroll_frame.winfo_reqheight() + 320 # + espacio extra por el combo
        max_h = self.root.winfo_screenheight() - 150
        
        self.root.geometry(f"600x{min(req_h, max_h)}")
        self.canvas.config(scrollregion=self.canvas.bbox("all"))
        self.toggle_scroll()

    def select_all(self):
        for i in self.file_vars: i['var'].set(True)
        self.update_stats()
    def deselect_all(self):
        for i in self.file_vars: i['var'].set(False)
        self.update_stats()
    def update_stats(self):
        # Filtramos los archivos seleccionados
        sel = [f for f in self.file_vars if f['var'].get()]
        
        # Calculamos totales
        total_kb = sum(f['kb'] for f in sel)
        total_lines = sum(f['lines'] for f in sel) # <--- Nuevo cálculo
        
        # Actualizamos la etiqueta con el nuevo formato
        self.lbl_stats.config(text=f"{len(sel)}/{len(self.file_vars)} sel | {total_lines} líneas | {int(total_kb)} KB")

    def generate_file(self):
        sel = [f for f in self.file_vars if f['var'].get()]
        if not sel: return messagebox.showwarning("!", "Selecciona archivos.")
        try:
            with open(OUTPUT_FILE, 'w', encoding='utf-8') as out:
                out.write(self.txt_prompt.get("1.0", tk.END).strip() + "\n\n")
                out.write(f"# CONTEXTO: {len(sel)} ARCHIVOS\n")
                
                # Ordenar la lista de archivos seleccionados
                sorted_sel = sorted(sel, key=lambda x: (
                    0 if not x['directory'] else 1,  # Root files primero
                    x['directory'] if x['directory'] else "",  # Luego por carpeta
                    x['name']  # Finalmente por nombre de archivo
                ))
                
                for f in sorted_sel:
                    out.write(f"- {f['path']}\n")
                out.write("\n# CÓDIGO FUENTE\n<codebase>\n")
                for f in sorted_sel:
                    with open(f['path'], 'r', encoding='utf-8') as src:
                        out.write(f'<file path="{f["path"]}">\n<![CDATA[\n{src.read()}\n]]>\n</file>\n')
                out.write("</codebase>")
            messagebox.showinfo("Listo", f"Contexto generado: {OUTPUT_FILE}")
            self.root.destroy()
        except Exception as e: messagebox.showerror("Error", str(e))

if __name__ == "__main__":
    root = tk.Tk()
    app = ContextApp(root)
    root.mainloop()