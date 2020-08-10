using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FezEngine.Mod {
    public static class Dummy<T> {

        public static readonly T[] EmptyArray = new T[0];
        public static readonly List<T> EmptyList = new List<T>();
        public static readonly T Default = default;

    }
}
