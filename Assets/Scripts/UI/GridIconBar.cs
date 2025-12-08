using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridIconBar : MonoBehaviour
{
    [Header("Icon Settings")]
    [SerializeField] private Sprite filledIcon;     // Filled grid icon sprite
    [SerializeField] private List<Image> iconImages = new List<Image>(); // Manually placed icon images
    [SerializeField] private Color iconColor = Color.white;

    void Start()
    {
        InitializeIcons();
    }

    private void InitializeIcons()
    {
        // Set all icons to filled and apply color
        foreach (Image img in iconImages)
        {
            if (img != null)
            {
                img.sprite = filledIcon;
                img.color = iconColor;
                img.gameObject.SetActive(true);
            }
        }
    }

    public void UpdateIcons(int currentValue, int maxValue)
    {
        if (iconImages.Count == 0) return;

        // Calculate how many icons should be visible
        float fillRatio = maxValue > 0 ? (float)currentValue / maxValue : 0f;
        int visibleCount = Mathf.RoundToInt(fillRatio * iconImages.Count);
        visibleCount = Mathf.Clamp(visibleCount, 0, iconImages.Count);

        // Show/hide icons based on count
        for (int i = 0; i < iconImages.Count; i++)
        {
            if (iconImages[i] != null)
            {
                // Show icon if index is less than visible count, hide otherwise
                iconImages[i].gameObject.SetActive(i < visibleCount);
            }
        }
    }

    public void SetIcon(Sprite filled)
    {
        filledIcon = filled;
        
        // Update existing icons
        foreach (Image img in iconImages)
        {
            if (img != null && img.gameObject.activeSelf)
            {
                img.sprite = filledIcon;
            }
        }
    }
}
