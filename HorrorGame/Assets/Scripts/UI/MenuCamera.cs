using UnityEngine;

// Black-screen camera so Unity never shows "No cameras rendering": on whenever the player camera (MainCamera) is inactive,
// i.e. behind the start menu, pause, reward and game over screens. Its AudioListener follows the camera (UI clicks stay audible).
[RequireComponent(typeof(Camera), typeof(AudioListener))]
public class MenuCamera : MonoBehaviour
{
    Camera cam;
    AudioListener listener;

    void Awake()
    {
        cam = GetComponent<Camera>();
        listener = GetComponent<AudioListener>();
    }

    void LateUpdate()
    {
        bool menu = Camera.main == null;
        cam.enabled = menu;
        listener.enabled = menu;
    }
}
