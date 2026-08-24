namespace Pos.Desktop.Configuration
{
    // BASIC-REL-01, sección 28/33: señal de "es esto un build de Release" atada a la configuración
    // de compilación real de Pos.Desktop (Debug/Release vía #if DEBUG), no a una variable de entorno
    // que un operador pudiera olvidar configurar en la máquina del cliente al publicar. Cuando se
    // publica con "dotnet publish -c Release", MSBuild propaga esa configuración a los proyectos
    // referenciados, así que este valor refleja fielmente el binario que realmente se empaqueta.
    internal static class ReleaseBuildInfo
    {
#if DEBUG
        public const bool IsReleaseBuild = false;
#else
        public const bool IsReleaseBuild = true;
#endif
    }
}
