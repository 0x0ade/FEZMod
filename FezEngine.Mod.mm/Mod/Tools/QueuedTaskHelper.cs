﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace FezEngine.Mod {
    public static class QueuedTaskHelper {

        private static readonly Dictionary<object, object> Map = new Dictionary<object, object>();
        private static readonly Dictionary<object, Stopwatch> Timers = new Dictionary<object, Stopwatch>();

        public static readonly double DefaultDelay = 0.5D;

        public static void Cancel(object key) {
            lock (Map) {
                if (Timers.TryGetValue(key, out Stopwatch timer)) {
                    timer.Stop();
                    Map.Remove(key);
                    Timers.Remove(key);
                }
            }
        }

        public static Task Do(object key, Action a)
            => Do(key, DefaultDelay, a);
        public static Task Do(object key, double delay, Action a) {
            lock (Map) {
                if (Map.TryGetValue(key, out object queued)) {
                    Timers[key].Restart();
                    return (Task) queued;
                }

                Stopwatch timer = Stopwatch.StartNew();
                Timers[key] = timer;
                Task t = new Func<Task>(async () => {
                    do {
                        await Task.Delay(TimeSpan.FromSeconds(delay - timer.Elapsed.TotalSeconds));
                    } while (timer.Elapsed.TotalSeconds < delay);

                    if (!timer.IsRunning)
                        return;

                    lock (Map) {
                        Map.Remove(key);
                        Timers.Remove(key);
                    }
                    timer.Stop();

                    a?.Invoke();
                })();

                Map[key] = t;
                return t;
            }
        }

        public static Task<T> Get<T>(object key, Func<T> f)
            => Get(key, DefaultDelay, f);
        public static Task<T> Get<T>(object key, double delay, Func<T> f) {
            lock (Map) {
                if (Map.TryGetValue(key, out object queued)) {
                    Timers[key].Restart();
                    return (Task<T>) queued;
                }

                Stopwatch timer = Stopwatch.StartNew();
                Timers[key] = timer;
                Task<T> t = new Func<Task<T>>(async () => {
                    do {
                        await Task.Delay(TimeSpan.FromSeconds(delay - timer.Elapsed.TotalSeconds));
                    } while (timer.Elapsed.TotalSeconds < delay);

                    lock (Map) {
                        Map.Remove(key);
                        Timers.Remove(key);
                    }
                    timer.Stop();

                    return f != null ? f.Invoke() : default;
                })();

                Map[key] = t;
                return t;
            }
        }

    }
}
