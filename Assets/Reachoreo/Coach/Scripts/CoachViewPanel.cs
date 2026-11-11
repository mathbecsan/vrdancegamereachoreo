using UnityEngine;
using UnityEngine.UI;

namespace Reachoreo
{
    /// <summary>
    /// The "Coach view" panel: two cards that pick how the dance coach stands.
    /// Poke a card with a finger or controller, or point at it and pull the trigger.
    /// The highlighted card always shows the coach's current view, also when it is
    /// switched another way (the A button).
    /// </summary>
    public class CoachViewPanel : MonoBehaviour
    {
        public DanceCoach coach;

        public Button backToYouButton;
        public Button facingYouButton;

        [Tooltip("Card backgrounds, tinted to show which one is selected.")]
        public Image backToYouCard;
        public Image facingYouCard;

        [Tooltip("Small 'selected' tags shown on the chosen card.")]
        public GameObject backToYouTag;
        public GameObject facingYouTag;

        public Color selectedColor = new Color(0.90f, 0.47f, 0.36f, 1f);
        public Color idleColor = new Color(0.20f, 0.20f, 0.22f, 1f);

        DanceCoach.Facing shown;
        bool initialized;

        void OnEnable()
        {
            if (backToYouButton != null) backToYouButton.onClick.AddListener(PickBackToYou);
            if (facingYouButton != null) facingYouButton.onClick.AddListener(PickFacingYou);
            Refresh(true);
        }

        void OnDisable()
        {
            if (backToYouButton != null) backToYouButton.onClick.RemoveListener(PickBackToYou);
            if (facingYouButton != null) facingYouButton.onClick.RemoveListener(PickFacingYou);
        }

        public void PickBackToYou() { if (coach != null) coach.SetFacing(DanceCoach.Facing.BackToYou); Refresh(true); }
        public void PickFacingYou() { if (coach != null) coach.SetFacing(DanceCoach.Facing.FacingYou); Refresh(true); }

        void Update() { Refresh(false); }

        void Refresh(bool force)
        {
            if (coach == null) return;
            if (!force && initialized && shown == coach.facing) return;
            shown = coach.facing;
            initialized = true;
            bool back = shown == DanceCoach.Facing.BackToYou;
            if (backToYouCard != null) backToYouCard.color = back ? selectedColor : idleColor;
            if (facingYouCard != null) facingYouCard.color = back ? idleColor : selectedColor;
            if (backToYouTag != null) backToYouTag.SetActive(back);
            if (facingYouTag != null) facingYouTag.SetActive(!back);
        }
    }
}
