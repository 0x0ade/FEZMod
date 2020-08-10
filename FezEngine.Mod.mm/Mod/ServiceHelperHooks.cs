using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FezEngine.Mod {
    public static class ServiceHelperHooks {

        public static Dictionary<string, IGameComponent> ReplacementComponents = new Dictionary<string, IGameComponent>();
        public static Dictionary<string, object> ReplacementServices = new Dictionary<string, object>();

    }
}
