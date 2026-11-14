using System;

namespace GameJam
{
    /// <summary>
    /// One screen of the selection flow (choose a power-up, choose a player, choose a ghost, confirm ...). The
    /// <see cref="PowerUpSelection"/> keeps the steps on a stack and hands the chooser's <see cref="NavCommand"/>s to the
    /// top one. A step draws itself in <see cref="Enter"/>, removes what it drew in <see cref="Exit"/> and moves the flow
    /// through the <see cref="PowerUpContext"/>: <c>Push</c> opens a further step, <c>Back</c> returns to the previous one
    /// (which draws itself again and keeps its state) and <c>Complete</c> ends the chooser's turn. Nothing a step shows
    /// may touch real game state; only the final confirmation of a power-up applies its effect.
    /// </summary>
    public abstract class SelectionStep
    {
        protected PowerUpContext Context { get; }

        protected SelectionStep(PowerUpContext context) => Context = context;

        /// <summary>Called when the step becomes the top of the stack (again). Draw everything the step needs.</summary>
        public abstract void Enter();

        /// <summary>Called when the step stops being the top. Remove what <see cref="Enter"/> showed.</summary>
        public virtual void Exit() { }

        public abstract void Handle(NavCommand command);

        /// <summary>The next index after <paramref name="current"/> in direction <paramref name="direction"/> (+1 / -1), wrapping around and skipping entries that are not allowed. Returns <paramref name="current"/> if there is no other.</summary>
        protected static int NextAllowed(int count, int current, int direction, Func<int, bool> allowed)
        {
            for (int step = 1; step <= count; step++)
            {
                int index = ((current + direction * step) % count + count) % count;
                if (allowed(index)) return index;
            }
            return current;
        }
    }
}
