using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>What a player's indicator shows.</summary>
    public enum CursorState
    {
        /// <summary>The player does not take part (e.g. players 3 and 4 in keyboard test mode): the indicator is not shown.</summary>
        Hidden,

        /// <summary>The player is picking a character: the arrow bobs above the character they are on.</summary>
        Choosing,

        /// <summary>The player confirmed a character: a color chip and the READY marker appear.</summary>
        Ready,

        /// <summary>The player's controller is not connected yet: the indicator is dimmed.</summary>
        WaitingForController,
    }

    /// <summary>
    /// One player's indicator on the character selection screen - the P1 / P2 / P3 / P4 badge with the arrow that points down
    /// at the character that player is on. It glides from character to character, bobs while the player is choosing and shows
    /// the assigned color once the player is ready. Pure view: the screen tells it where to be and what to show. Edit it as a
    /// prefab (Assets/Prefabs/Menu/PlayerCursor.prefab); each of the four on the screen has its own badge sprite and text.
    /// Every part is optional. The object's pivot is the arrow tip, so the tip lands exactly on the slot's cursor anchor.
    /// </summary>
    public class PlayerCursorView : MonoBehaviour
    {
        [Header("Parts (every part is optional)")]
        [Tooltip("The P1 / P2 / P3 / P4 badge. Give each of the four indicators its own sprite. It is never tinted, except to dim it while waiting for a controller.")]
        [SerializeField] Image badge;

        [Tooltip("The text on the badge (P1, P2 ...). Clear it or switch the object off if your badge sprite already contains the text.")]
        [SerializeField] TMP_Text playerLabel;

        [Tooltip("The arrow that points at the character this player is on.")]
        [SerializeField] Image arrow;

        [Tooltip("A chip tinted with the player's assigned color, shown once the player is ready. It is the clearest sign of which color the player got.")]
        [SerializeField] Image colorChip;

        [Tooltip("Switched on once the player is ready (the READY text, a check mark ...).")]
        [SerializeField] GameObject readyMarker;

        [Header("Look")]
        [Tooltip("Tint of the badge and arrow while the player's controller is missing.")]
        [SerializeField] Color waitingTint = new Color(1f, 1f, 1f, 0.35f);

        [Header("Motion")]
        [Tooltip("How quickly the indicator follows its target. Higher is snappier.")]
        [SerializeField, Min(0f)] float followSpeed = 16f;

        [Tooltip("How far the arrow bobs up and down while the player is choosing (canvas units).")]
        [SerializeField, Min(0f)] float bobHeight = 8f;

        [SerializeField, Min(0f)] float bobSpeed = 6f;

        CursorState state = CursorState.Hidden;
        Color shownColor;
        bool stateApplied;
        Vector3 target;
        bool hasTarget;
        Vector2 arrowBase;
        bool arrowKnown;

        public CursorState State => state;

        /// <summary>Forgets what was shown, so the next <see cref="SetState"/> and <see cref="MoveTo"/> apply from scratch. Called when the screen opens.</summary>
        public void ResetView()
        {
            stateApplied = false;
            hasTarget = false;
        }

        /// <summary>Shows the indicator in the given state. <paramref name="assignedColor"/> is the player's color once ready.</summary>
        public void SetState(CursorState newState, Color assignedColor)
        {
            // Called every frame by the screen: do nothing unless something changed.
            if (stateApplied && newState == state && (newState != CursorState.Ready || assignedColor == shownColor)) return;

            stateApplied = true;
            state = newState;
            shownColor = assignedColor;
            gameObject.SetActive(newState != CursorState.Hidden);
            if (newState == CursorState.Hidden) return;

            RememberArrow();
            bool waiting = newState == CursorState.WaitingForController;
            bool ready = newState == CursorState.Ready;

            if (badge != null) badge.color = waiting ? waitingTint : Color.white;
            if (arrow != null)
            {
                arrow.color = waiting ? waitingTint : Color.white;
                arrow.gameObject.SetActive(!waiting);
                if (newState != CursorState.Choosing) arrow.rectTransform.anchoredPosition = arrowBase;
            }

            if (colorChip != null)
            {
                colorChip.gameObject.SetActive(ready);
                if (ready) colorChip.color = assignedColor;
            }
            if (readyMarker != null) readyMarker.SetActive(ready);
        }

        /// <summary>Where the arrow tip should be (world position). <paramref name="snap"/> jumps there at once instead of gliding.</summary>
        public void MoveTo(Vector3 worldPosition, bool snap)
        {
            target = worldPosition;
            if (snap || !hasTarget) transform.position = worldPosition;
            hasTarget = true;
        }

        void RememberArrow()
        {
            if (arrowKnown || arrow == null) return;
            arrowBase = arrow.rectTransform.anchoredPosition;
            arrowKnown = true;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            float dt = Time.unscaledDeltaTime;

            if (hasTarget)
            {
                Vector3 next = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followSpeed * dt));
                transform.position = (next - target).sqrMagnitude < 0.0001f ? target : next;
            }

            if (arrow != null && arrowKnown && state == CursorState.Choosing)
                arrow.rectTransform.anchoredPosition = arrowBase + new Vector2(0f, Mathf.Sin(Time.unscaledTime * bobSpeed) * bobHeight);
        }
    }
}
