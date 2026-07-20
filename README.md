# PosPlatform

Solución de punto de venta (POS) de escritorio, local-first, para Windows.

## Descripción

PosPlatform es la solución de escritorio para un sistema de punto de venta
pensado para operar principalmente sin conexión a internet, manteniendo los
datos comerciales de forma local en la tienda durante esta primera versión
(V1).

Este repositorio corresponde únicamente al POS Desktop. La plataforma general
contempla, en repositorios separados y aún no implementados:

- aplicación móvil React Native;
- servidor privado NestJS;
- acceso remoto mediante Cloudflare Tunnel.

## Estado

Desarrollo inicial. Actualmente solo se han establecido la estructura de la
solución y los estándares base del repositorio (compilación, estilo y
documentación). Todavía no existe funcionalidad de punto de venta
implementada.

## Arquitectura

La solución sigue una separación por capas, con dependencias unidireccionales
entre proyectos:

- `Pos.Domain` es el núcleo del dominio y no depende de ningún otro proyecto.
- `Pos.Application` contiene la lógica de aplicación y depende únicamente de
  `Pos.Domain`.
- `Pos.Infrastructure` implementa detalles de persistencia y servicios
  externos, dependiendo de `Pos.Application` y `Pos.Domain`.
- `Pos.Hardware` implementa la integración con hardware de punto de venta,
  dependiendo de `Pos.Application` y `Pos.Domain`.
- `Pos.Desktop` es la aplicación WPF de escritorio, dependiendo de
  `Pos.Application`, `Pos.Infrastructure` y `Pos.Hardware`.

### Proyectos y responsabilidades

| Proyecto             | Responsabilidad                                            |
|-----------------------|-------------------------------------------------------------|
| `Pos.Domain`          | Entidades y reglas de negocio centrales del dominio.        |
| `Pos.Application`     | Casos de uso y lógica de aplicación, sin detalles técnicos. |
| `Pos.Infrastructure`  | Persistencia y servicios de infraestructura.                |
| `Pos.Hardware`        | Integración con hardware de punto de venta.                 |
| `Pos.Desktop`         | Interfaz de usuario de escritorio (WPF).                    |

## Requisitos locales

- Windows 11 (recomendado).
- Visual Studio Community 2022.
- .NET 8 SDK.
- Git.
- Claude Code CLI.

## Comandos

```
dotnet restore PosPlatform.sln
dotnet build PosPlatform.sln
dotnet run --project Pos.Desktop/Pos.Desktop.csproj
```

## Flujo de ramas

- `main`: rama estable.
- `develop`: rama de integración.

Los comandos Git de escritura (commits, creación de ramas, merges, push,
pull, despliegues, etc.) son ejecutados exclusivamente por el usuario.
