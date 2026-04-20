using UnityEngine;
using UnityEngine.UI;

public class Joycon2UIPointer : MonoBehaviour
{
    public JoyconDeviceID deviceID;
    public RectTransform canvasRect;
    public float mouseScale = 0.05f; // Adjust based on how fast you want the pointer to move
    
    private RectTransform myRect;

    private void Start()
    {
        myRect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (Joycon2Manager.Instance == null || canvasRect == null) return;

        Vector2 mouseVal = (deviceID == JoyconDeviceID.Left) ? 
            Joycon2Manager.Instance.leftMouseValue : 
            Joycon2Manager.Instance.rightMouseValue;

        uint buttons = (deviceID == JoyconDeviceID.Left) ? 
            Joycon2Manager.Instance.leftJoycon.buttons : 
            Joycon2Manager.Instance.rightJoycon.buttons;

        bool connected = (deviceID == JoyconDeviceID.Left) ? 
            Joycon2Manager.Instance.leftConnected : 
            Joycon2Manager.Instance.rightConnected;

        if (!connected) return;

        // Use the mouse data directly as screen space or relative movement
        // Joycon2 mouse data seems to be absolute positioning or relative displacement depending on mode
        // For UI Pointer, let's treat it as a mapped position
        float nx = mouseVal.x * mouseScale;
        float ny = mouseVal.y * mouseScale;

        // Mapping to canvas space
        // Assuming mouseVal is in some reasonable range after sensitivity
        myRect.anchoredPosition = new Vector2(nx, -ny);

        // Visual feedback on button press
        Image img = GetComponent<Image>();
        if (img != null) {
            Color normalColor = (deviceID == JoyconDeviceID.Right ? Color.red : Color.blue);
            img.color = (buttons != 0) ? Color.yellow : normalColor;
        }
    }
}
