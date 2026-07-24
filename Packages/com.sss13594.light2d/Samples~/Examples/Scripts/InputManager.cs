using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Light2D.Examples
{
    public class InputManager : MonoBehaviour
    {
        public Spacecraft ControlledSpacecraft;
        public GameObject TouchControls;
        public ButtonHelper UpButton, DownButton, LeftButton, RightButton;

        // Optional UI Toolkit controller. When assigned, its held-state and click signals are ORed with
        // the legacy ButtonHelper path so both input surfaces work while scenes are migrated. It manages
        // its own touch-pad visibility, so the legacy TouchControls toggle below is only for the uGUI
        // scenes that have not been converted yet.
        public SampleUiController Ui;

        private IEnumerator Start()
        {
            if (Ui != null)
            {
                Ui.FlareClicked += FlareClick;
                Ui.LegsClicked += LegsClick;
                Ui.RestartClicked += Restart;
            }

            if (TouchControls != null)
                TouchControls.SetActive(Application.isMobilePlatform && SampleInput.TouchSupported);

            ControlledSpacecraft.MainRigidbody.bodyType = RigidbodyType2D.Kinematic;
            yield return new WaitForSeconds(1);
            ControlledSpacecraft.MainRigidbody.bodyType = RigidbodyType2D.Dynamic;
        }

        private void Update()
        {
            if (SampleInput.GetKeyDown(KeyCode.F1))
                Time.timeScale = Time.timeScale > 0.5 ? 0 : 1;

            if (SampleInput.GetKeyDown(KeyCode.R))
                Restart();

            if(SampleInput.GetKeyDown(KeyCode.C))
                ControlledSpacecraft.DropFlares();

            ControlledSpacecraft.BottomLeftEngine.ForcePercent = 0;
            ControlledSpacecraft.BottomRightEngine.ForcePercent = 0;
            ControlledSpacecraft.SideRightEngine.ForcePercent = 0;
            ControlledSpacecraft.SideLeftEngine.ForcePercent = 0;

            var moveDir = Vector2.zero;
            if (SampleInput.GetKey(KeyCode.UpArrow) || SampleInput.GetKey(KeyCode.W) || IsPressed(UpButton) || (Ui != null && Ui.UpHeld)) moveDir += Vector2.up;
            if (SampleInput.GetKey(KeyCode.DownArrow) || SampleInput.GetKey(KeyCode.S) || IsPressed(DownButton) || (Ui != null && Ui.DownHeld)) moveDir += Vector2.down;
            if (SampleInput.GetKey(KeyCode.RightArrow) || SampleInput.GetKey(KeyCode.D) || IsPressed(RightButton) || (Ui != null && Ui.RightHeld)) moveDir += Vector2.right;
            if (SampleInput.GetKey(KeyCode.LeftArrow) || SampleInput.GetKey(KeyCode.A) || IsPressed(LeftButton) || (Ui != null && Ui.LeftHeld)) moveDir += Vector2.left;

            ControlledSpacecraft.BottomLeftEngine.ForcePercent = moveDir.y*2f + moveDir.x;
            ControlledSpacecraft.BottomRightEngine.ForcePercent = moveDir.y*2f - moveDir.x;
            ControlledSpacecraft.SideLeftEngine.ForcePercent = moveDir.x;
            ControlledSpacecraft.SideRightEngine.ForcePercent = -moveDir.x;
            ControlledSpacecraft.ReverseLeftEngine.ForcePercent = -moveDir.y - moveDir.x*2f;
            ControlledSpacecraft.ReverseRightEngine.ForcePercent = -moveDir.y + moveDir.x*2f;

            if (SampleInput.GetKeyDown(KeyCode.Space))
            {
                ControlledSpacecraft.ReleaseLandingGear ^= true;
            }
        }

        public void LegsClick()
        {
            ControlledSpacecraft.ReleaseLandingGear ^= true;
        }

        public void FlareClick()
        {
            ControlledSpacecraft.DropFlares();
        }

        public void Restart()
        {
            SceneManager.LoadScene(gameObject.scene.name);
        }

        private void OnDestroy()
        {
            if (Ui != null)
            {
                Ui.FlareClicked -= FlareClick;
                Ui.LegsClicked -= LegsClick;
                Ui.RestartClicked -= Restart;
            }
        }

        private static bool IsPressed(ButtonHelper button)
        {
            return button != null && button.IsPressed;
        }
    }
}
