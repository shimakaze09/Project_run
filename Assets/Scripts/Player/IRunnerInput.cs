using Run.Common;

namespace Run.Player
{
    /// <summary>
    /// Where the runner's jump commands come from. <see cref="PlayerController"/> only ever asks
    /// this interface, so a person on the keyboard and the computer runner drive it identically.
    /// </summary>
    public interface IRunnerInput
    {
        /// <summary>True on the frame a jump is first requested.</summary>
        bool JumpPressed();

        /// <summary>True while the jump is held; releasing early gives a shorter hop.</summary>
        bool JumpHeld();
    }

    /// <summary>The default source: the human's keyboard, mouse, or touch via <see cref="RunInput"/>.</summary>
    public sealed class HumanRunnerInput : IRunnerInput
    {
        public static readonly HumanRunnerInput Instance = new HumanRunnerInput();

        private HumanRunnerInput() { }

        public bool JumpPressed() => RunInput.ConfirmPressed();

        public bool JumpHeld() => RunInput.JumpHeld();
    }
}
