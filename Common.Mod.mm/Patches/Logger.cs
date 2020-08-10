#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common {
    class patch_Logger {

        public static extern void orig_Log(string component, LogSeverity severity, string message);
        public static void Log(string component, LogSeverity severity, string message) {
            Console.WriteLine("(" + DateTime.Now.ToString("HH:mm:ss.fff") + ") [" + component + "] " + message);

            orig_Log(component, severity, message);
        }

    }
}
