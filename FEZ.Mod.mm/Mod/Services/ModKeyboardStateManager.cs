using FezEngine.Mod.Services;
using FezEngine.Services;
using FezEngine.Structure.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FezGame.Mod.Services {
    public class ModKeyboardStateManager : IKeyboardStateManager, IServiceWrapper {

        public bool ForceDisable = false;

        private IKeyboardStateManager _;

        public void Wrap(object orig) => _ = (IKeyboardStateManager) orig;

        public FezButtonState CancelTalk {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.CancelTalk;
            }
        }

        public FezButtonState ClampLook {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.ClampLook;
            }
        }

        public FezButtonState Down {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.Down;
            }
        }

        public FezButtonState FpViewToggle {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.FpViewToggle;
            }
        }

        public FezButtonState GrabThrow {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.GrabThrow;
            }
        }

        public bool IgnoreMapping {
            get {
                return _.IgnoreMapping;
            }

            set {
                _.IgnoreMapping = value;
            }
        }

        public FezButtonState Jump {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.Jump;
            }
        }

        public FezButtonState Left {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.Left;
            }
        }

        public FezButtonState LookDown {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.LookDown;
            }
        }

        public FezButtonState LookLeft {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.LookLeft;
            }
        }

        public FezButtonState LookRight {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.LookRight;
            }
        }

        public FezButtonState LookUp {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.LookUp;
            }
        }

        public FezButtonState MapZoomIn {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.MapZoomIn;
            }
        }

        public FezButtonState MapZoomOut {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.MapZoomOut;
            }
        }

        public FezButtonState OpenInventory {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.OpenInventory;
            }
        }

        public FezButtonState OpenMap {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.OpenMap;
            }
        }

        public FezButtonState Pause {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.Pause;
            }
        }

        public FezButtonState Right {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.Right;
            }
        }

        public FezButtonState RotateLeft {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.RotateLeft;
            }
        }

        public FezButtonState RotateRight {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.RotateRight;
            }
        }

        public FezButtonState Up {
            get {
                if (ForceDisable)
                    return FezButtonState.Up;

                return _.Up;
            }
        }

        public FezButtonState GetKeyState(Keys key) {
            if (ForceDisable)
                return FezButtonState.Up;

            return _.GetKeyState(key);
        }

        public void RegisterKey(Keys key) {
            _.RegisterKey(key);
        }

        public void Update(KeyboardState state, GameTime time) {
            _.Update(state, time);
        }

        public void UpdateMapping() {
            _.UpdateMapping();
        }

    }
}
