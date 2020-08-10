#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using FezEngine.Mod;
using FezEngine.Mod.Core;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FezEngine.Structure {
    // ctor is private
    class patch_OggStream {

        [MonoModIgnore] private static readonly ConcurrentQueue<patch_OggStream> ToPrecache;
        [MonoModIgnore] private static readonly AutoResetEvent WakeUpPrecacher;
        [MonoModIgnore] private static Thread ThreadedPrecacher;
        [MonoModIgnore] private static IntPtr bufferPtr;
        [MonoModIgnore] private static byte[] vorbisBuffer;

        [MonoModIgnore] private IntPtr vorbisFile;
        [MonoModIgnore] private DynamicSoundEffectInstance soundEffect;
        [MonoModIgnore] private bool hitEof;
        [MonoModIgnore] public float Volume { get; set; }
        [MonoModIgnore] public bool IsLooped { get; set; }

        [MonoModIgnore]
        private static extern void PrecacheStreams();

        [MonoModIgnore]
        private extern void OnBufferNeeded(object sender, EventArgs e);

        [MonoModReplace]
        private void Initialize() {
            // The original method refers to long vorbis_info.rate, which has been changed to IntPtr in newer FNA releases.

            Vorbisfile.vorbis_info vorbis_info = Vorbisfile.ov_info(vorbisFile, -1);
            soundEffect = new DynamicSoundEffectInstance((int) vorbis_info.rate, (vorbis_info.channels == 1) ? AudioChannels.Mono : AudioChannels.Stereo);
            Volume = 1f;

            ToPrecache.Enqueue(this);
            if (ThreadedPrecacher == null) {
                ThreadedPrecacher = new Thread(PrecacheStreams) {
                    Priority = ThreadPriority.Lowest
                };
                ThreadedPrecacher.Start();
            }
            WakeUpPrecacher.Set();
        }

        private void QueueBuffer(object source, EventArgs ea) {
            // The original method refers to Int64 Vorbisfile.ov_read(IntPtr, IntPtr, Int32, Int32, Int32, Int32, Int32 ByRef),
            // which has been changed in newer FNA releases. The last parameter is now out, not ref.

            int pos = 0;
            int read;
            do {
                read = (int) Vorbisfile.ov_read(vorbisFile, bufferPtr + pos, 4096, 0, 2, 1, out int current_section);
                pos += read;
            }
            while (read > 0 && pos < 187904);

            if (pos != 0) {
                soundEffect.SubmitBuffer(vorbisBuffer, 0, pos);
                return;
            }

            if (IsLooped) {
                Vorbisfile.ov_time_seek(vorbisFile, 0.0);
                QueueBuffer(source, ea);
                return;
            }

            hitEof = true;
            soundEffect.BufferNeeded -= OnBufferNeeded;
        }

    }
}
