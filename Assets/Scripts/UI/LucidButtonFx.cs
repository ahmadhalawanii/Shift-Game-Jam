using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Selection, taken from the dreamcatcher the game is built around: the selected option gets a thread
// drawn in from the left with a bead at its end. Hovering selects, so mouse and pad never disagree.
public class LucidButtonFx : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    Image thread;
    LucidRing bead;
    Text label;
    float k;
    bool selected;

    public void Init(Image thread, LucidRing bead, Text label)
    {
        this.thread = thread; this.bead = bead; this.label = label;
        Apply();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }
    public void OnSelect(BaseEventData e) { selected = true; }
    public void OnDeselect(BaseEventData e) { selected = false; }

    void OnEnable()
    {
        selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
    }

    void Update()
    {
        if (label == null) return;
        k = Mathf.MoveTowards(k, selected ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        Apply();
    }

    void Apply()
    {
        float e = 1f - (1f - k) * (1f - k);
        // The tint follows the world: Dream cyan, Nightmare ember.
        Color accent = Color.Lerp(LucidUI.Dream, LucidUI.Ember, WorldState.MixOrZero);
        thread.color = accent;
        thread.rectTransform.localScale = new Vector3(e, 1f, 1f);
        bead.color = LucidUI.Alpha(accent, e);
        label.color = Color.Lerp(LucidUI.Alpha(LucidUI.Cream, .72f), Color.white, e);
    }
}
