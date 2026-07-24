using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Light2D.Examples
{
    // Drives the sample overlay UI built with UI Toolkit. It sits on the same GameObject as a
    // UIDocument whose Source Asset is SampleUI.uxml (styled by SampleUI.uss through a Style ref) and
    // whose Panel Settings carry the shared reference resolution. On enable it queries the cloned
    // visual tree and binds behaviour to the named elements: a top-left keyboard-hint block, an
    // optional top-center feature label, and a touch-controls cluster (Up/Down bottom-left,
    // Flare/Legs over Left/Right bottom-right, Restart top-center).
    //
    // This is the UI Toolkit replacement for the uGUI ButtonHelper + Canvas path. It is transitional:
    // it exposes the same held-state and click signals the old path did so InputManager can OR the two
    // together while scenes are migrated one at a time.
    //
    //  - Directional buttons (Up/Down/Left/Right) report a held bool. Each press captures its own
    //    pointer id, so a finger that leaves the button still delivers its release to the button and
    //    two fingers on two buttons stay independent (multi-touch safe).
    //  - Action buttons (Flare/Legs/Restart) raise a plain C# event on click, which survives visual
    //    tree rebuilds because subscribers listen to this controller, not to the button element.
    //  - Touch visibility mirrors the legacy InputManager.Start gating (mobile + touch only); the
    //    capture/verification tooling can force it on via SetTouchControlsVisible.
    public class SampleUiController : MonoBehaviour
    {
        [Tooltip("Keyboard-hint text shown top-left. Rich text is honored, so <b>key</b> names render bold.")]
        [TextArea]
        public string HintText =
            "<b>Arrows</b> - thrusters\n\n<b>C</b> - drop flares\n\n<b>Space</b> - raise / lower legs\n\n<b>R</b> - restart";

        [Tooltip("Optional feature label shown top-center. Empty hides it. Also set at runtime via SetFeatureLabel.")]
        public string FeatureLabel = string.Empty;

        // Directional held state, polled by InputManager each frame.
        private bool _upHeld;
        private bool _downHeld;
        private bool _leftHeld;
        private bool _rightHeld;

        public bool UpHeld => _upHeld;
        public bool DownHeld => _downHeld;
        public bool LeftHeld => _leftHeld;
        public bool RightHeld => _rightHeld;

        // Click signals raised on pointer release. Subscribers persist across visual tree rebuilds.
        public event Action FlareClicked;
        public event Action LegsClicked;
        public event Action RestartClicked;

        // True when the current runtime should show the touch pad, matching the legacy gating in
        // InputManager.Start: only real mobile devices with a touchscreen get the on-screen controls.
        public bool TouchControlsRequested => Application.isMobilePlatform && SampleInput.TouchSupported;

        private UIDocument _document;
        private bool _bound;
        private bool _touchControlsVisible;

        private Label _hintLabel;
        private Label _featureLabelElement;
        private VisualElement _touchControls;

        private void OnEnable()
        {
            // Reset the platform default each time the object is enabled; capture tooling can override
            // it afterwards through SetTouchControlsVisible.
            _touchControlsVisible = TouchControlsRequested;
            TryBind();
        }

        private void OnDisable()
        {
            // The UIDocument tears its visual tree down on disable, so drop our element cache and
            // held state; the next enable rebuilds and rebinds against the fresh tree.
            _bound = false;
            _hintLabel = null;
            _featureLabelElement = null;
            _touchControls = null;
            _upHeld = _downHeld = _leftHeld = _rightHeld = false;
        }

        private void Update()
        {
            // The UIDocument may finish cloning its tree after this component's OnEnable, so keep
            // retrying until the elements exist. This is a no-op once bound.
            if (!_bound)
                TryBind();
        }

        // Sets the top-center feature label and shows or hides it. Safe to call before the tree binds;
        // the value is stored and applied on the next bind.
        public void SetFeatureLabel(string label)
        {
            FeatureLabel = label;
            ApplyFeatureLabel();
        }

        // Forces the touch-controls cluster on or off regardless of platform gating. Used by the
        // capture/verification tooling to render the mobile layout on desktop.
        public void SetTouchControlsVisible(bool visible)
        {
            _touchControlsVisible = visible;
            if (_touchControls != null)
                _touchControls.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool TryBind()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
            if (_document == null)
                return false;

            VisualElement root = _document.rootVisualElement;
            if (root == null || root.childCount == 0)
                return false;

            _hintLabel = root.Q<Label>("hint-block");
            _featureLabelElement = root.Q<Label>("feature-label");
            _touchControls = root.Q<VisualElement>("touch-controls");

            BindHeld(root.Q<VisualElement>("up-button"), held => _upHeld = held);
            BindHeld(root.Q<VisualElement>("down-button"), held => _downHeld = held);
            BindHeld(root.Q<VisualElement>("left-button"), held => _leftHeld = held);
            BindHeld(root.Q<VisualElement>("right-button"), held => _rightHeld = held);

            BindClick(root.Q<Button>("flare-button"), () => FlareClicked?.Invoke());
            BindClick(root.Q<Button>("legs-button"), () => LegsClicked?.Invoke());
            BindClick(root.Q<Button>("restart-button"), () => RestartClicked?.Invoke());

            if (_hintLabel != null)
            {
                // An empty hint must hide the whole block, or its translucent panel renders as a
                // bare box (e.g. the Light Volume scene ships no keyboard hint).
                _hintLabel.text = HintText;
                _hintLabel.style.display =
                    string.IsNullOrEmpty(HintText) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            ApplyFeatureLabel();
            SetTouchControlsVisible(_touchControlsVisible);

            _bound = true;
            return true;
        }

        private void ApplyFeatureLabel()
        {
            if (_featureLabelElement == null)
                return;

            bool hasLabel = !string.IsNullOrEmpty(FeatureLabel);
            _featureLabelElement.text = hasLabel ? FeatureLabel : string.Empty;
            _featureLabelElement.style.display = hasLabel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Wires press-and-hold tracking with per-pointer capture so the release always returns to the
        // button that received the press, keeping multiple simultaneous touches independent.
        private static void BindHeld(VisualElement button, Action<bool> setHeld)
        {
            if (button == null)
                return;

            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                setHeld(true);
                button.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            button.RegisterCallback<PointerUpEvent>(evt =>
            {
                setHeld(false);
                if (button.HasPointerCapture(evt.pointerId))
                    button.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });
            // If the capture is lost for any reason (stolen, element removed), the button can never
            // see its PointerUp, so clear the held state here to avoid a stuck thruster.
            button.RegisterCallback<PointerCaptureOutEvent>(_ => setHeld(false));
        }

        private static void BindClick(Button button, Action onClick)
        {
            if (button == null)
                return;

            button.clicked += onClick;
        }
    }
}
