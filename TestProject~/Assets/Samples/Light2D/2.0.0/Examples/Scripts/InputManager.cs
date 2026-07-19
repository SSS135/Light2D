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

        private IEnumerator Start()
        {
            if (TouchControls != null)
                TouchControls.SetActive(Application.isMobilePlatform && Input.touchSupported);

            ControlledSpacecraft.MainRigidbody.bodyType = RigidbodyType2D.Kinematic;
            yield return new WaitForSeconds(1);
            ControlledSpacecraft.MainRigidbody.bodyType = RigidbodyType2D.Dynamic;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                Time.timeScale = Time.timeScale > 0.5 ? 0 : 1;

            if (Input.GetKeyDown(KeyCode.R))
                Restart();

            if(Input.GetKeyDown(KeyCode.C))
                ControlledSpacecraft.DropFlares();

            ControlledSpacecraft.BottomLeftEngine.ForcePercent = 0;
            ControlledSpacecraft.BottomRightEngine.ForcePercent = 0;
            ControlledSpacecraft.SideRightEngine.ForcePercent = 0;
            ControlledSpacecraft.SideLeftEngine.ForcePercent = 0;

            var moveDir = Vector2.zero;
            if (Input.GetKey(KeyCode.UpArrow) || IsPressed(UpButton)) moveDir += Vector2.up;
            if (Input.GetKey(KeyCode.DownArrow) || IsPressed(DownButton)) moveDir += Vector2.down;
            if (Input.GetKey(KeyCode.RightArrow) || IsPressed(RightButton)) moveDir += Vector2.right;
            if (Input.GetKey(KeyCode.LeftArrow) || IsPressed(LeftButton)) moveDir += Vector2.left;

            ControlledSpacecraft.BottomLeftEngine.ForcePercent = moveDir.y*2f + moveDir.x;
            ControlledSpacecraft.BottomRightEngine.ForcePercent = moveDir.y*2f - moveDir.x;
            ControlledSpacecraft.SideLeftEngine.ForcePercent = moveDir.x;
            ControlledSpacecraft.SideRightEngine.ForcePercent = -moveDir.x;
            ControlledSpacecraft.ReverseLeftEngine.ForcePercent = -moveDir.y - moveDir.x*2f;
            ControlledSpacecraft.ReverseRightEngine.ForcePercent = -moveDir.y + moveDir.x*2f;

            if (Input.GetKeyDown(KeyCode.Space))
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

        private static bool IsPressed(ButtonHelper button)
        {
            return button != null && button.IsPressed;
        }
    }
}
