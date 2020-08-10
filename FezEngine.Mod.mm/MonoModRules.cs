using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using MonoMod.InlineRT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoMod {
    [MonoModCustomMethodAttribute("PatchInitMMSharedData")]
    class PatchInitMMSharedDataAttribute : Attribute { }

    static class MonoModRules {

        static MonoModRules() {
        }

        public static void PatchInitMMSharedData(MethodDefinition method, CustomAttribute attrib) {
            MethodDefinition m_Set = method.DeclaringType.FindMethod("System.Void SetMMSharedData(System.String,System.Boolean)");
            if (m_Set == null)
                return;

            if (!method.HasBody)
                return;

            method.Body.Instructions.Clear();
            ILProcessor il = method.Body.GetILProcessor();

            foreach (KeyValuePair<string, object> kvp in MonoModRule.Modder.SharedData) {
                if (!(kvp.Value is bool))
                    return;
                il.Emit(OpCodes.Ldstr, kvp.Key);
                il.Emit((bool)kvp.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Call, m_Set);
            }

            il.Emit(OpCodes.Ret);
        }

    }
}
