using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FezEngine.Mod.Services {
    public interface IServiceWrapper {

        void Wrap(object orig);

    }
}
