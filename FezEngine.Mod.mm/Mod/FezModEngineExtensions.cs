using Ionic.Zip;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FezEngine.Mod {
    public static class FezModEngineExtensions {

        public static MemoryStream ExtractStream(this ZipEntry entry) {
            MemoryStream ms = new MemoryStream();
            entry.Extract(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public static string ToHexadecimalString(this byte[] data)
            => BitConverter.ToString(data).Replace("-", string.Empty);

        public static T InvokePassing<T>(this MulticastDelegate md, T val, params object[] args) {
            if (md == null)
                return val;

            object[] args_ = new object[args.Length + 1];
            args_[0] = val;
            Array.Copy(args, 0, args_, 1, args.Length);

            Delegate[] ds = md.GetInvocationList();
            for (int i = 0; i < ds.Length; i++)
                args_[0] = ds[i].DynamicInvoke(args_);

            return (T)args_[0];
        }

        public static bool InvokeWhileTrue(this MulticastDelegate md, params object[] args) {
            if (md == null)
                return true;

            Delegate[] ds = md.GetInvocationList();
            for (int i = 0; i < ds.Length; i++)
                if (!((bool)ds[i].DynamicInvoke(args)))
                    return false;

            return true;
        }

        public static bool InvokeWhileFalse(this MulticastDelegate md, params object[] args) {
            if (md == null)
                return false;

            Delegate[] ds = md.GetInvocationList();
            for (int i = 0; i < ds.Length; i++)
                if ((bool)ds[i].DynamicInvoke(args))
                    return true;

            return false;
        }

        public static T InvokeWhileNull<T>(this MulticastDelegate md, params object[] args) where T : class {
            if (md == null)
                return null;

            Delegate[] ds = md.GetInvocationList();
            for (int i = 0; i < ds.Length; i++) {
                T result = (T)ds[i].DynamicInvoke(args);
                if (result != null)
                    return result;
            }

            return null;
        }

        public static Vector2 ToVector2(this Point p) {
            return new Vector2(p.X, p.Y);
        }

        public static Vector2? ToVector2(this float[] a) {
            if (a == null)
                return null;
            if (a.Length == 1)
                return new Vector2(a[0]);
            if (a.Length != 2)
                return null;
            return new Vector2(a[0], a[1]);
        }

        public static Vector3? ToVector3(this float[] a) {
            if (a == null)
                return null;
            if (a.Length == 1)
                return new Vector3(a[0]);
            if (a.Length != 3)
                return null;
            return new Vector3(a[0], a[1], a[2]);
        }

        public static Delegate CastDelegate(this Delegate source, Type type) {
            if (source == null)
                return null;
            Delegate[] delegates = source.GetInvocationList();
            if (delegates.Length == 1)
                return Delegate.CreateDelegate(type, delegates[0].Target, delegates[0].Method);
            Delegate[] delegatesDest = new Delegate[delegates.Length];
            for (int i = 0; i < delegates.Length; i++)
                delegatesDest[i] = delegates[i].CastDelegate(type);
            return Delegate.Combine(delegatesDest);
        }

        public static Type[] GetTypesSafe(this Assembly asm) {
            try {
                return asm.GetTypes();
            } catch (ReflectionTypeLoadException e) {
                return e.Types.Where(t => t != null).ToArray();
            }
        }

        public static string Nullify(this string value)
            => string.IsNullOrEmpty(value) ? null : value;

    }
}
