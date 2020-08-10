using FezEngine.Mod.Services;
using FezEngine.Services;
using FezEngine.Structure.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace FezGame.Mod.Services {
    public class ModMouseStateManager : IMouseStateManager, IServiceWrapper {

        public bool ForceDisable = false;

        private IMouseStateManager _;

        public void Wrap(object orig) => _ = (IMouseStateManager) orig;

        public MouseButtonState LeftButton {
            get {
                if (ForceDisable)
                    return new MouseButtonState();

                return _.LeftButton;
            }
        }

        public MouseButtonState MiddleButton {
            get {
                if (ForceDisable)
                    return new MouseButtonState();

                return _.MiddleButton;
            }
        }

        public MouseButtonState RightButton {
            get {
                if (ForceDisable)
                    return new MouseButtonState();

                return _.RightButton;
            }
        }

        public int WheelTurns {
            get {
                if (ForceDisable)
                    return 0;

                return _.WheelTurns;
            }
        }

        public FezButtonState WheelTurnedUp {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.WheelTurnedUp;
            }
        }

        public FezButtonState WheelTurnedDown {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.WheelTurnedDown;
            }
        }

        public Point Position {
            get {
                return _.Position;
            }
        }

        public Point Movement {
            get {
                if (ForceDisable)
                    return new Point();

                return _.Movement;
            }
        }

        public IntPtr RenderPanelHandle {
            set {
                _.RenderPanelHandle = value;
            }
        }

        public IntPtr ParentFormHandle {
            set {
                _.ParentFormHandle = value;
            }
        }

        public void Update(GameTime time) {
            if (ForceDisable)
                return;

            _.Update(time);
        }

    }
}
