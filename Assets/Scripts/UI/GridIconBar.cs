using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridIconBar : MonoBehaviour
{
    [Header("Icon Settings")]
    [SerializeField] private Sprite filledIcon;     // Filled grid icon sprite
    [SerializeField] private List<GameObject> iconGameObjects = new List<GameObject>(); // Manually placed icon GameObjects
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private Color yellowColor = Color.yellow;
    [SerializeField] private Color redColor = Color.red;

    void Start()
    {
        InitializeIcons();
    }

    private void InitializeIcons()
    {
        // Set all icons to filled and apply color based on initial count
        int visibleCount = iconGameObjects.Count;
        Color currentColor = GetColorForIconCount(visibleCount);

        foreach (GameObject iconObj in iconGameObjects)
        {
            if (iconObj != null)
            {
                Image img = iconObj.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = filledIcon;
                    img.color = currentColor;
                }
                iconObj.SetActive(true);
            }
        }
    }

    public void UpdateIcons(int currentValue, int maxValue)
    {
        if (iconGameObjects.Count == 0) return;

        // Calculate how many icons should be visible
        // Each icon represents (maxValue / iconGameObjects.Count) amount
        int visibleCount = 0;
        if (maxValue > 0 && currentValue > 0)
        {
            float armorPerIcon = (float)maxValue / iconGameObjects.Count;
            visibleCount = Mathf.CeilToInt((float)currentValue / armorPerIcon);
        }
        visibleCount = Mathf.Clamp(visibleCount, 0, iconGameObjects.Count);

        // Get color based on visible count
        Color currentColor = GetColorForIconCount(visibleCount);

        // Show/hide icons based on count and update colors
        for (int i = 0; i < iconGameObjects.Count; i++)
        {
            if (iconGameObjects[i] != null)
            {
                // Show icon if index is less than visible count, hide otherwise
                bool shouldBeActive = i < visibleCount;
                iconGameObjects[i].SetActive(shouldBeActive);

                // Update color for visible icons
                if (shouldBeActive)
                {
                    Image img = iconGameObjects[i].GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = currentColor;
                    }
                }
            }
        }
    }

    private Color GetColorForIconCount(int visibleCount)
    {
        // Red when 4 or fewer icons left
        if (visibleCount <= 4)
        {
            return redColor;
        }
        // Yellow when 7 or fewer icons left
        else if (visibleCount <= 7)
        {
            return yellowColor;
        }
        // Default color when more than 7 icons left
        else
        {
            return iconColor;
        }
    }

    public void SetIcon(Sprite filled)
    {
        filledIcon = filled;

        // Update existing icons
        foreach (GameObject iconObj in iconGameObjects)
        {
            if (iconObj != null && iconObj.activeSelf)
            {
                Image img = iconObj.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = filledIcon;
                }
            }
        }
    }
}
