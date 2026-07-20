# CLAUDE.md — PosPlatform

Reglas permanentes para Claude Code en este repositorio. Estas reglas aplican a
todas las tareas futuras salvo que el usuario autorice explícitamente una
excepción puntual.

## A. Contexto del proyecto

- Este repositorio contiene el POS Desktop y su servicio local, local-first
  para Windows.
- Tecnología: C#, .NET 8, WPF, XAML, SQLite (se incorporará más adelante).
- El sistema operará con hardware de punto de venta (impresoras, cajones,
  lectores, básculas, etc.).
- La aplicación debe seguir funcionando sin conexión a internet.
- Los datos comerciales permanecen en SQLite dentro de la tienda. No existe
  base central cloud de ventas en V1.
- La aplicación móvil consulta reportes de solo lectura mediante el servicio
  local expuesto por Cloudflare Tunnel.
- El servidor privado del fabricante solo administra licencias, salud
  técnica, soporte y actualizaciones, y pertenece a otro proyecto.
- El POS debe continuar operando aunque Cloudflare, la aplicación móvil o el
  servidor privado no estén disponibles.

## B. Arquitectura y dependencias

- `Pos.Domain` no depende de otros proyectos.
- `Pos.Application` depende únicamente de `Pos.Domain`.
- `Pos.Infrastructure` depende de `Pos.Application` y `Pos.Domain`.
- `Pos.Hardware` depende de `Pos.Application` y `Pos.Domain`.
- `Pos.Desktop` depende de `Pos.Application`, `Pos.Infrastructure` y
  `Pos.Hardware`.
- No introducir referencias circulares entre proyectos.
- No agregar dependencia directa de `Pos.Desktop` a `Pos.Domain` sin
  autorización explícita del usuario.
- No acceder directamente desde la UI a persistencia o hardware; debe pasar
  por `Pos.Application`.
- No colocar reglas de negocio en code-behind de WPF.

## C. Reglas de desarrollo

- Mantener .NET 8 en toda la solución.
- `Nullable` debe permanecer habilitado.
- Usar `async`/`await` para operaciones de entrada/salida (I/O).
- No usar `async void`, salvo manejadores de eventos WPF donde sea
  inevitable.
- No usar `float` ni `double` para representar dinero.
- Usar `decimal` para importes monetarios.
- Las fechas persistidas deben almacenarse en UTC.
- No usar `DateTime.Now` dentro de lógica de dominio.
- No crear abstracciones sin una necesidad concreta y justificada.
- Evitar sobreingeniería.
- No crear microservicios.
- No agregar paquetes NuGet sin autorización explícita del usuario.
- No crear ni ejecutar migraciones sin autorización explícita del usuario.
- No implementar funcionalidades fuera de la tarea solicitada.
- No modificar archivos que no estén relacionados con la tarea solicitada.
- No solucionar advertencias mediante supresiones indiscriminadas
  (`#pragma warning disable`, `NoWarn`, etc. usados como atajo).
- No almacenar secretos (cadenas de conexión, claves, tokens) en el
  repositorio.

## D. Reglas de Git

Claude **no puede ejecutar comandos Git de escritura**.

Prohibidos:

- `git add`
- `git commit`
- `git push`
- `git pull`
- `git fetch`
- `git merge`
- `git rebase`
- `git checkout`
- `git switch`
- `git branch` (creación/eliminación)
- `git reset`
- `git restore`
- `git stash`
- `git clean`
- Creación de pull requests.

Permitidos únicamente para consulta:

- `git status`
- `git diff`
- `git diff --stat`
- `git branch --show-current`
- `git log`

El usuario es la única persona autorizada para realizar commits, crear
ramas, hacer merges, pull, push y despliegues.

## E. Reglas de base de datos

- SQLite será la base de datos local del POS.
- PostgreSQL pertenece al proyecto privado cloud y no forma parte de esta
  solución Desktop.
- No compartir archivos SQLite por red.
- No actualizar inventario sin registrar el movimiento correspondiente.
- No eliminar físicamente ventas, pagos, cortes de caja ni movimientos
  históricos.
- No crear ni ejecutar migraciones de base de datos sin autorización
  explícita del usuario.

## F. Reglas de validación

Después de cualquier tarea que modifique código, Claude debe:

- Ejecutar `dotnet build PosPlatform.sln`.
- Ejecutar pruebas automatizadas cuando existan.
- Reportar errores y advertencias obtenidos de la compilación.
- Reportar los archivos modificados, creados y eliminados.
- Mostrar la salida de `git status --short`.
- No afirmar que algo funciona si no fue compilado o probado.

## G. Formato de reporte final

Todo reporte final de una tarea debe incluir:

1. Rama detectada.
2. Estado inicial.
3. Archivos modificados.
4. Archivos creados.
5. Archivos eliminados.
6. Decisiones tomadas.
7. Comandos ejecutados.
8. Resultado de compilación.
9. Resultado de pruebas, cuando existan.
10. Errores.
11. Advertencias.
12. Salida final de `git status --short`.
13. Desviaciones o bloqueos.
14. Confirmación de que no se ejecutaron comandos Git de escritura.
