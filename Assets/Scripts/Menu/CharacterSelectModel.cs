using System.Collections.Generic;

namespace GameJam
{
    /// <summary>Why a confirm did, or did not, go through.</summary>
    public enum ConfirmStatus
    {
        /// <summary>The player now owns the character under their cursor.</summary>
        Confirmed,

        /// <summary>The player already confirmed a character (cancel first to pick another one).</summary>
        AlreadyConfirmed,

        /// <summary>Another player owns the character under the cursor.</summary>
        Unavailable,

        /// <summary>The player has no cursor, so there is nothing to confirm.</summary>
        NoCursor,

        /// <summary>The player does not take part in this game.</summary>
        Inactive,
    }

    /// <summary>
    /// The rules of the character selection and nothing else: which player hovers over, and which player owns, which
    /// character. It knows nothing about Unity, input or visuals, so replacing art or controls can never change how it
    /// behaves.
    /// <para>
    /// A character is claimed in exactly one place, <see cref="TryConfirm"/>, and every call runs to completion before the
    /// next one starts. The first caller gets the character, every later caller - even one that arrives in the same frame -
    /// is refused with <see cref="ConfirmStatus.Unavailable"/>. A character is therefore never owned by two players.
    /// </para>
    /// <para>
    /// A claim does not move anybody's cursor by itself. Call <see cref="ResolveCursors"/> once all the inputs of a frame
    /// have been applied: it moves every player who is still hovering over a taken character to the nearest free one. That
    /// way a player whose confirm arrives in the same frame as a rival's is refused instead of silently getting another
    /// character than the one they pressed on.
    /// </para>
    /// Players are numbered from 0. The first <c>activePlayers</c> of them take part; the others are ignored.
    /// </summary>
    public sealed class CharacterSelectModel
    {
        /// <summary>Value of "no character" / "no player".</summary>
        public const int None = -1;

        readonly int characterCount;
        readonly int playerCount;
        readonly int activePlayers;

        readonly int[] cursor;   // cursor[player]   = the character the player hovers over (None = no cursor)
        readonly int[] choice;   // choice[player]   = the character the player confirmed (None = not confirmed yet)
        readonly int[] owner;    // owner[character] = the player who confirmed it (None = free)
        readonly List<int> moved = new List<int>();

        public CharacterSelectModel(int characterCount, int playerCount, int activePlayers, bool wrapAround = true)
        {
            this.characterCount = System.Math.Max(0, characterCount);
            this.playerCount = System.Math.Max(0, playerCount);
            this.activePlayers = System.Math.Max(0, System.Math.Min(activePlayers, this.playerCount));
            WrapAround = wrapAround;

            cursor = new int[this.playerCount];
            choice = new int[this.playerCount];
            owner = new int[this.characterCount];
            Reset();
        }

        public int CharacterCount => characterCount;
        public int PlayerCount => playerCount;

        /// <summary>Number of players that take part. They are players 0 .. ActivePlayerCount - 1.</summary>
        public int ActivePlayerCount => activePlayers;

        /// <summary>True: moving past the last character continues at the first one (and the other way round).</summary>
        public bool WrapAround { get; set; }

        public bool IsActive(int player) => player >= 0 && player < activePlayers;

        /// <summary>The character the player hovers over, or <see cref="None"/>.</summary>
        public int GetCursor(int player) => InRange(player) ? cursor[player] : None;

        /// <summary>The character the player confirmed, or <see cref="None"/>.</summary>
        public int GetChoice(int player) => InRange(player) ? choice[player] : None;

        /// <summary>The player who owns the character, or <see cref="None"/> if it is free.</summary>
        public int GetOwner(int character) => character >= 0 && character < characterCount ? owner[character] : None;

        public bool HasConfirmed(int player) => GetChoice(player) != None;

        public bool IsClaimed(int character) => GetOwner(character) != None;

        /// <summary>True if the player may hover over / confirm the character: it is free, or already theirs.</summary>
        public bool IsAvailableTo(int character, int player)
        {
            if (character < 0 || character >= characterCount) return false;
            int current = owner[character];
            return current == None || current == player;
        }

        /// <summary>How many of the active players have confirmed a character.</summary>
        public int ConfirmedCount
        {
            get
            {
                int count = 0;
                for (int p = 0; p < activePlayers; p++)
                    if (choice[p] != None) count++;
                return count;
            }
        }

        /// <summary>True once every active player has confirmed (and there is at least one).</summary>
        public bool AllConfirmed => activePlayers > 0 && ConfirmedCount == activePlayers;

        /// <summary>Forgets everything: nothing is owned, nobody has confirmed, each active player hovers over the character with their own number.</summary>
        public void Reset()
        {
            for (int c = 0; c < characterCount; c++) owner[c] = None;
            for (int p = 0; p < playerCount; p++)
            {
                choice[p] = None;
                cursor[p] = IsActive(p) && characterCount > 0 ? p % characterCount : None;
            }
            moved.Clear();
        }

        /// <summary>
        /// Moves the player's cursor one step (<paramref name="direction"/> &lt; 0 = left, &gt; 0 = right) to the next character
        /// that is available to them; taken characters are skipped. Returns false if the cursor did not move (the player
        /// already confirmed, or there is nowhere to go).
        /// </summary>
        public bool Move(int player, int direction)
        {
            if (!IsActive(player) || direction == 0 || choice[player] != None) return false;

            int from = cursor[player];
            if (from == None) return false;

            int step = direction > 0 ? 1 : -1;
            for (int distance = 1; distance < characterCount; distance++)
            {
                int candidate = from + step * distance;
                if (WrapAround) candidate = ((candidate % characterCount) + characterCount) % characterCount;
                else if (candidate < 0 || candidate >= characterCount) return false;

                if (!IsAvailableTo(candidate, player)) continue;
                cursor[player] = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gives the character under the player's cursor to the player, if it is still free. This is the one and only place a
        /// character is claimed. Other cursors are not touched - see <see cref="ResolveCursors"/>.
        /// </summary>
        public ConfirmStatus TryConfirm(int player)
        {
            if (!IsActive(player)) return ConfirmStatus.Inactive;
            if (choice[player] != None) return ConfirmStatus.AlreadyConfirmed;

            int character = cursor[player];
            if (character == None) return ConfirmStatus.NoCursor;
            if (owner[character] != None) return ConfirmStatus.Unavailable;

            owner[character] = player;
            choice[player] = character;
            return ConfirmStatus.Confirmed;
        }

        /// <summary>Takes the player's confirmed character back, so they (and everyone else) can pick it again. False if they had none.</summary>
        public bool TryCancel(int player)
        {
            if (!IsActive(player) || choice[player] == None) return false;

            int character = choice[player];
            owner[character] = None;
            choice[player] = None;
            cursor[player] = character;
            return true;
        }

        /// <summary>
        /// Moves every player who has not confirmed yet, but hovers over a character somebody else owns, to the nearest
        /// character that is available to them (to the right first on a tie). A character nobody else is hovering over is
        /// preferred, so several players who lost the same race end up on different characters instead of racing again.
        /// Call it once per frame, after all inputs of the frame were applied. Returns the players that were moved (empty
        /// most of the time); the list is only valid until the next call.
        /// </summary>
        public IReadOnlyList<int> ResolveCursors()
        {
            moved.Clear();
            for (int p = 0; p < activePlayers; p++)
            {
                if (choice[p] != None) continue;

                int current = cursor[p];
                if (current == None || IsAvailableTo(current, p)) continue;

                int target = NearestAvailable(current, p, avoidHovered: true);
                if (target == None) target = NearestAvailable(current, p, avoidHovered: false);
                cursor[p] = target;
                moved.Add(p);
            }
            return moved;
        }

        int NearestAvailable(int from, int player, bool avoidHovered)
        {
            for (int distance = 1; distance < characterCount; distance++)
            {
                int right = from + distance;
                int left = from - distance;
                if (WrapAround)
                {
                    right = ((right % characterCount) + characterCount) % characterCount;
                    left = ((left % characterCount) + characterCount) % characterCount;
                }

                if (right >= 0 && right < characterCount && IsCandidate(right, player, avoidHovered)) return right;
                if (left >= 0 && left < characterCount && IsCandidate(left, player, avoidHovered)) return left;
            }
            return None;
        }

        bool IsCandidate(int character, int player, bool avoidHovered)
        {
            if (!IsAvailableTo(character, player)) return false;
            if (!avoidHovered) return true;

            for (int other = 0; other < activePlayers; other++)
                if (other != player && choice[other] == None && cursor[other] == character) return false;
            return true;
        }

        bool InRange(int player) => player >= 0 && player < playerCount;
    }
}
