using System;
using System.IO;

namespace LogrosBB
{
    // Edición del programa. La de desarrollo (build.ps1 -Dev) convive con la instalada sin tocarla:
    // otra instancia, otra carpeta de datos, sin inicio con Windows ni actualizaciones automáticas.
    static class Edicion
    {
#if DEV
        public const bool Dev = true;
        public const string Id = "LogrosBloodborne-Dev";
#else
        public const bool Dev = false;
        public const string Id = "LogrosBloodborne";
#endif
        // %APPDATA%\LogrosBloodborne (o LogrosBloodborne-Dev)
        public static string CarpetaDatos
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Id); }
        }
    }
}
