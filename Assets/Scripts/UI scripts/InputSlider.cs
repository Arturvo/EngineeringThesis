using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;
using TMPro;

public class InputSlider : MonoBehaviour
{
    private Slider slider;
    [SerializeField] private TextMeshProUGUI minText, maxText, valText;
    [SerializeField] private GameObject tooltip;
    private bool isTooltipOpened = false;
    
    private void Awake() {
        slider = GetComponent<Slider>();
        tooltip.SetActive(false);
        isTooltipOpened = false;
    }
    // Start is called before the first frame update
    void Start()
    {
        minText.text = slider.minValue.ToString();
        maxText.text = slider.maxValue.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        valText.text = slider.value.ToString();
    }

    public void OpenTooltip() {
        if(!isTooltipOpened) tooltip.SetActive(true);
        else tooltip.SetActive(false);

        isTooltipOpened = !isTooltipOpened;
    }
    public void CloseTooltip() {
        tooltip.SetActive(false);
        isTooltipOpened = false;
    }
}
