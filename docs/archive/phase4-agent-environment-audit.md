# Auditoría del entorno del agente

**Fecha:** 2026-09-08  
**Workspace:** `VetManagement`  
**Alcance:** capacidades de contexto, búsqueda, indexación, instrucciones y herramientas observables en esta sesión.

## 1. Resumen ejecutivo

El workspace tiene una base funcional, pero la configuración de contexto está parcialmente fragmentada.

- Las instrucciones de proyecto de [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md) están presentes y son el principal contrato técnico del repositorio. El archivo de instrucciones externo de usuario también se cargó en esta sesión.
- No se detectaron `AGENTS.md`, `GEMINI.md`, `.agent.md`, `.instructions.md`, `SKILL.md`, prompts de workspace ni `.geminiignore`.
- El grafo `codebase-memory` está operativo para el proyecto `VetManagement`: estado `ready`, 2.084 nodos y 6.788 relaciones. Su cobertura es deliberadamente parcial porque excluye `.github`, `docs`, artefactos y archivos locales.
- La herramienta `semantic_search` responde, pero devolvió rutas históricas con la estructura anterior `VetManagement/...`; debe considerarse parcialmente confiable hasta comprobar su alineación con `src/...`.
- Git y .NET están disponibles y fueron comprobados. El repositorio contiene cambios previos no realizados durante esta auditoría; no se modificaron.
- `.gitignore` excluye todo `.github` salvo workflows. Esto puede dejar fuera del control de versiones instrucciones que el agente necesita para trabajar de forma reproducible.
- La documentación separa parcialmente arquitectura e historial, pero `project-knowledge.md` mezcla contexto operativo, ADRs resumidos y credenciales de desarrollo, mientras que `MASTER_STATUS.md` y la revisión de 2025 pueden confundirse con estado vigente.

No se aplicó ninguna recomendación de configuración, instalación o reindexación.

## 2. Contexto persistente detectado

| Mecanismo | Estado | Evidencia | Impacto | Recomendación |
|---|---|---|---|---|
| Instrucciones de usuario | Funcionando | `c:\Users\bubit\.copilot\copilot-instructions.md` fue cargado por la sesión | Aporta reglas generales y específicas de VetManagement fuera del repositorio | Mantenerlas para preferencias personales; evitar duplicar allí reglas que deban viajar con el proyecto |
| Instrucciones del workspace | Funcionando | `.github/copilot-instructions.md` existe y contiene arquitectura, estilo, EF Core, testing y Blazor | Influye directamente en las tareas del repositorio | Versionar el archivo y mantenerlo como contrato breve y vigente |
| `AGENTS.md` | No disponible | No se encontró en el workspace | No existe una convención adicional de agentes por jerarquía | Crear solo si se necesita una política de alcance claro |
| `GEMINI.md` | No disponible | No se encontró | No hay instrucciones específicas para Gemini | No crearla para este flujo de Copilot |
| `*.instructions.md` | No disponible | No se encontraron archivos | No hay reglas por patrón de archivos | Añadirlas solo para reglas realmente específicas por área |
| `*.agent.md` | No disponible | No se encontraron agentes de workspace | No hay agentes personalizados versionados | Crear uno únicamente para un flujo repetible y con herramientas restringidas |
| `SKILL.md` o prompts de workspace | No disponible | No se encontraron archivos | Las skills actuales provienen del entorno del agente, no del repositorio | No copiar skills externas al repositorio sin una necesidad concreta |
| `project-knowledge.md` | Parcialmente disponible | `.github/project-knowledge.md` existe, pero el índice MCP excluye `.github` | Puede aportar contexto si se lee explícitamente, pero no es evidencia de carga automática | Separar conocimiento vigente, ADRs y datos sensibles; declarar su modo de carga |

La presencia de un archivo no prueba que se cargue automáticamente. En esta sesión se comprobó la carga de las instrucciones suministradas por el entorno; no se observó una carga automática de `project-knowledge.md`, `EF-CORE-TRACKING-RULES.md` ni de los documentos de `docs/`.

## 3. Indexación y búsqueda disponible

### 3.1 Grafo estructural MCP

**Estado: funcionando, con alcance parcial.**

La consulta de estado del proyecto `VetManagement` devolvió:

- Estado: `ready`.
- Raíz: `C:/Users/bubit/Documents/Projects/Web Projects/VetManagement`.
- 2.084 nodos.
- 6.788 relaciones.
- Sin archivos con parseo parcial.
- Sin archivos omitidos por error de lectura o parseo.
- 25 directorios y 21 archivos no indexados deliberadamente por `.gitignore`, `.cbmignore` o listas de exclusión.

El grafo es adecuado para localizar símbolos, relaciones, implementaciones y dependencias del código indexado. No es una fuente completa para instrucciones ni documentación porque excluye `.github` y `docs` por diseño.

### 3.2 Búsqueda semántica

**Estado: parcialmente disponible.**

La llamada a `semantic_search` respondió a una consulta conceptual sobre límites de migración y encontró contenido relacionado. Sin embargo, los resultados usaron rutas antiguas como `VetManagement/VetManagement.Shared/...`, mientras que el workspace actual usa `src/VetManagement.Shared/...`.

Esto confirma que la capacidad de búsqueda por significado existe, pero no confirma que su índice esté fresco o alineado con el checkout actual. Una búsqueda positiva no debe usarse para hacer afirmaciones exhaustivas hasta corregir o validar esa discrepancia.

### 3.3 Búsqueda textual y de archivos

**Estado: funcionando.**

Están disponibles búsqueda por glob, búsqueda textual, lectura directa de archivos y búsqueda semántica. Son apropiadas para:

- nombres de archivos y configuraciones;
- literales, mensajes y reglas de ignore;
- documentación no indexada por el grafo;
- verificación de fuentes exactas tras una búsqueda estructural.

### 3.4 Cómo verificar el mecanismo

La verificación mínima reproducible antes de una tarea de arquitectura debe ser:

1. Consultar el estado del proyecto MCP y confirmar `ready`, raíz y exclusiones.
2. Ejecutar una búsqueda estructural sobre un símbolo conocido bajo `src/`.
3. Confirmar una relación de llamada o implementación.
4. Comprobar la cobertura del archivo citado cuando el resultado se use como evidencia.
5. Usar búsqueda textual directa para `.github`, `docs`, configuraciones y cualquier ruta excluida.

No se debe interpretar `indexed` como “todo el repositorio está indexado”.

## 4. Exclusiones y archivos innecesarios

### 4.1 Exclusiones confirmadas por el índice

El índice excluye deliberadamente:

- `.github/`;
- `docs/`;
- `.vs/`;
- `obj/` y `bin/` de proyectos;
- `tests/VetManagement.Tests/TestResults/`;
- archivos de configuración locales y de usuario;
- mapas, fuentes, imágenes y otros recursos marcados por listas de sufijos ignorados.

Esto evita ruido de documentación y compilación dentro del grafo, pero también significa que las instrucciones y reglas arquitectónicas deben auditarse por lectura directa.

### 4.2 Exclusiones de Git

El `.gitignore` excluye:

- `.github/*`, salvo `.github/workflows/`;
- `.vscode/`, `.vs/` e `.idea/`;
- `bin/`, `obj/`, `Debug/`, `Release/`, `artifacts/` y logs;
- resultados de pruebas, cobertura y bases de datos locales;
- configuraciones de desarrollo como `appsettings.Development.json` y `config.json`;
- varios documentos históricos o personales como `SESSION_HISTORY.md`, `QUICKSTART_COPILOT.md` y `DOC_INDEX.md`.

La exclusión de Git no garantiza por sí sola que una herramienta de contexto ignore el archivo. En este caso, el índice MCP sí reporta muchas de esas exclusiones, pero el agente debe seguir usando reglas explícitas para no leerlas innecesariamente.

### 4.3 Ruido identificado

No deberían entrar automáticamente en cada tarea:

- `bin/`, `obj/`, `.vs/`, `TestResults/` y cobertura;
- logs de migración y archivos `.user`;
- configuraciones locales o con secretos;
- `docs/reports/ARCHITECTURE_REVIEW_2025.md`, salvo auditorías históricas;
- `docs/MASTER_STATUS.md`, salvo comparación de evolución;
- checklists antiguas, salvo validación de pendientes históricos.

No se recomienda eliminar estos archivos ni cambiar su contenido como parte de esta auditoría.

## 5. Herramientas disponibles

| Herramienta | Estado | Valor para el proyecto |
|---|---|---|
| Lectura de archivos | Funcionando | Fuente principal para instrucciones y documentación excluidas del grafo |
| Búsqueda de archivos | Funcionando | Descubrimiento de convenciones, configuraciones y artefactos |
| Búsqueda textual | Funcionando | Literales, ignore rules, documentación y configuración |
| Búsqueda semántica | Parcialmente disponible | Útil para exploración conceptual, pero debe validarse por rutas y frescura |
| Grafo `codebase-memory` | Funcionando, limitado | Muy útil para símbolos, llamadas, implementaciones e impacto del código indexado |
| Cobertura del índice | Funcionando | Permite conocer si una ruta o ámbito quedó fuera del grafo |
| Terminal PowerShell | Funcionando | Permite comprobaciones reproducibles y comandos del proyecto |
| Git | Funcionando | `git.exe` fue localizado y `git status` se ejecutó correctamente |
| .NET SDK | Funcionando | `dotnet.exe` fue localizado; no se ejecutó build en esta auditoría |
| Subagentes | Funcionando | Útiles para exploración de solo lectura con alcance explícito |
| Memoria de sesión | Funcionando | Conserva el plan y decisiones de esta auditoría, fuera del repositorio |
| MCP genérico no relacionado | No verificado | No debe asumirse por documentación externa |

El estado de Git observado fue `main...origin/main [ahead 9, behind 1]`, con cambios locales previos en varios archivos de código y `PROJECT_STATE.md` sin seguimiento. Esos cambios no forman parte de esta auditoría y no fueron revertidos ni modificados.

## 6. Problemas encontrados

### P1. Instrucciones del proyecto potencialmente fuera del control de versiones

`.gitignore` excluye `.github/*` salvo workflows, pero `.github/copilot-instructions.md` contiene reglas esenciales. Esto puede producir workspaces con comportamiento distinto entre colaboradores o clones.

**Estado:** confirmado.  
**Impacto:** alto para reproducibilidad del agente.  
**Recomendación:** decidir explícitamente qué instrucciones deben versionarse y ajustar el ignore solo después de aprobación.

### P2. Índices con señales de frescura inconsistente

El estado del grafo MCP es correcto y apunta a la raíz actual, pero `semantic_search` entregó rutas históricas sin `src/`.

**Estado:** confirmado como discrepancia; causa exacta no determinada.  
**Impacto:** alto para búsquedas conceptuales y análisis de impacto.  
**Recomendación:** validar el proyecto usado por cada mecanismo y reindexar únicamente después de confirmar el alcance y las exclusiones.

### P3. Documentación operativa e histórica mezclada

`PROJECT_STATE.md` está actualizado al 2026-09-07. En cambio, `MASTER_STATUS.md` y `ARCHITECTURE_REVIEW_2025.md` describen estados de 2025. No existe un directorio formal de ADRs; los ADRs aparecen resumidos dentro de `project-knowledge.md`.

**Estado:** confirmado.  
**Impacto:** medio-alto; puede inducir decisiones basadas en estado obsoleto.  
**Recomendación:** declarar una fuente de verdad para estado, arquitectura y decisiones, y etiquetar claramente el material histórico.

### P4. `project-knowledge.md` mezcla contexto técnico y credenciales

El archivo contiene arquitectura, términos, ADRs resumidos y credenciales de desarrollo.

**Estado:** confirmado.  
**Impacto:** alto por riesgo de exposición y por contaminación del contexto.  
**Recomendación:** retirar credenciales del contexto documental y usar mecanismos de secretos fuera del repositorio; no aplicar ese cambio dentro de esta auditoría.

### P5. No existe `.vscode/` versionado

No hay configuración del workspace para búsquedas, exclusiones visuales, tareas o integración específica del agente.

**Estado:** confirmado.  
**Impacto:** medio; el comportamiento queda sujeto a la configuración local del editor.  
**Recomendación:** considerar una configuración mínima y deliberada solo si resuelve una necesidad concreta, sin convertirla en una nueva fuente duplicada de reglas.

## 7. Recomendaciones

1. Establecer una sola fuente breve para instrucciones activas del repositorio y decidir si `.github/copilot-instructions.md` debe estar versionado.
2. Mantener fuera del contexto automático `bin/`, `obj/`, `.vs/`, resultados de pruebas, cobertura, logs, archivos `.user` y configuraciones locales.
3. Mantener `docs/architecture/` para arquitectura vigente y crear un espacio separado para ADRs si se van a gestionar como decisiones formales.
4. Marcar `MASTER_STATUS.md` y `ARCHITECTURE_REVIEW_2025.md` como históricos o reemplazados, sin cargarlos automáticamente en tareas normales.
5. Separar `project-knowledge.md` en contexto técnico seguro, ADRs y referencias de configuración, eliminando credenciales del material de contexto.
6. Validar que todas las herramientas de búsqueda usan el mismo root y la misma generación de índice.
7. Ejecutar la comprobación de cobertura del índice antes de hacer afirmaciones negativas o exhaustivas sobre código, documentación o exclusiones.
8. No instalar servidores MCP, extensiones ni paquetes hasta definir el problema que deben resolver y aprobar el cambio.

## 8. Configuración propuesta (sin aplicar)

La siguiente configuración es una propuesta de trabajo, no un cambio aplicado:

```text
Instrucciones activas:
  .github/copilot-instructions.md

Arquitectura vigente:
  docs/architecture/

Decisiones:
  docs/architecture/adr/       # solo si se adopta formalmente

Estado actual:
  PROJECT_STATE.md

Histórico:
  docs/archive/ o docs/reports/archive/

No cargar automáticamente:
  bin/, obj/, .vs/, TestResults/, coverage/, logs,
  archivos *.user, configuraciones locales, reportes históricos

Búsqueda de código:
  grafo MCP para símbolos y relaciones;
  búsqueda textual para docs, instrucciones y configuración;
  comprobación de cobertura antes de conclusiones exhaustivas.
```

Antes de aplicar esta propuesta habría que decidir:

- si el repositorio versionará sus instrucciones de Copilot;
- si se adopta una convención formal de ADRs;
- si se moverán documentos históricos o solo se etiquetarán;
- qué mecanismo será la autoridad para refrescar y consultar el índice semántico;
- si se necesita una configuración `.vscode/` compartida.

## 9. Cambios de configuración previos a la migración de arquitectura

Antes de continuar con la migración F1/F2/F3, se recomienda completar, en este orden:

1. Resolver la discrepancia entre el índice semántico y la estructura actual `src/...`.
2. Confirmar qué instrucciones son normativas y versionarlas de forma reproducible.
3. Designar `PROJECT_STATE.md` como estado vigente y etiquetar los documentos de 2025 como históricos.
4. Separar ADRs de resúmenes generales y retirar credenciales del contexto documental.
5. Confirmar que las exclusiones de Git y del índice cubren artefactos sin ocultar código fuente relevante.
6. Documentar un procedimiento corto para verificar raíz, generación, cobertura y frescura del índice antes de cada tarea de impacto arquitectónico.
7. Revisar los cambios locales ya existentes antes de ejecutar una migración, porque el workspace no está limpio.

No se recomienda modificar todavía el código, instalar herramientas, reindexar a ciegas, añadir `.vscode/`, crear agentes personalizados ni cambiar la arquitectura como parte de esta auditoría.
